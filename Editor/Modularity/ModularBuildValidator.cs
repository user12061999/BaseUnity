using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BaseUnity.Modularity
{
    public sealed class ModuleValidationIssue
    {
        public readonly bool error;
        public readonly string message;
        public readonly UnityEngine.Object context;
        public ModuleValidationIssue(bool error,string message,UnityEngine.Object context=null) { this.error=error;this.message=message;this.context=context; }
    }
    /// <summary>Reflection-only, read-only validation. Never initializes a native SDK.</summary>
    public static class ModularBuildValidator
    {
        private static object Get(object target,string field) { if(target==null)return null;var t=target.GetType();return t.GetField(field)?.GetValue(target)??t.GetProperty(field)?.GetValue(target,null); }
        private static bool Kind(object value,string name) { for(var t=value?.GetType();t!=null;t=t.BaseType)if(t.FullName==name)return true;return false; }
        private static bool Id(object value) => value is string id&&!string.IsNullOrWhiteSpace(id)&&id.Length<=64&&id.All(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-');
        private static object[] Items(object value) => value is IEnumerable list?list.Cast<object>().ToArray():new object[0];
        public static List<ModuleValidationIssue> ValidateRoots(GameObject[] roots,BuildTarget target,bool? development=null)
        {
            var issues=new List<ModuleValidationIssue>();
            var components=roots.SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c).ToArray();
            Action<bool,string,UnityEngine.Object> add=(error,message,context)=>issues.Add(new ModuleValidationIssue(error,message,context));
            foreach(var tr in roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)>0)add(true,"Missing script: "+tr.name,tr);
            var bootstraps=components.Where(c=>Kind(c,"BaseUnity.Core.ModuleBootstrap")).ToArray();
            if(bootstraps.Length>1)add(true,"Multiple ModuleBootstrap roots in the same scene",bootstraps[0]);
            foreach(var component in components) {
                string name=component.GetType().FullName;
                bool installer=Kind(component,"BaseUnity.Core.ModuleInstaller");
                if(installer&&component is Behaviour disabled&&!disabled.enabled)continue;
                if(installer&&!component.GetComponents<Component>().Any(c=>Kind(c,"BaseUnity.Core.ModuleBootstrap")))add(true,"Installer must share its root with ModuleBootstrap: "+name,component);
                if(Kind(component,"BaseUnity.Core.ModuleBootstrap")&&component.transform.parent)add(true,"ModuleBootstrap must be a scene root",component);
                using(var serialized=new SerializedObject(component)) {
                    var property=serialized.GetIterator();while(property.NextVisible(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue==null&&property.objectReferenceInstanceIDValue!=0)add(true,"Missing reference: "+name+"."+property.propertyPath,component);
                }
                if(name=="BaseUnity.Flow.GameFlowModule") {
                    if(Convert.ToInt32(Get(component,"levelCount"))<1)add(true,"GameFlow levelCount must be positive",component);
                    foreach(string field in new[]{"homeScene","ingameScene"}) {
                        string scene=Get(component,field) as string;if(string.IsNullOrWhiteSpace(scene))continue;
                        if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&(s.path==scene||Path.GetFileNameWithoutExtension(s.path)==scene)))add(true,"GameFlow scene is not enabled in Build Settings: "+scene,component);
                    }
                }
                if(name=="BaseUnity.FlowUI.GameFlowView")foreach(string field in new[]{"home","ingame","loading","pause","win","lose"})if(!(Get(component,field) is UnityEngine.Object obj)||!obj)add(true,"GameFlowView missing panel: "+field,component);
                if(name.StartsWith("BaseUnity.EconomyModules.UI.",StringComparison.Ordinal)) {
                    string button=name.EndsWith("ShopItemView",StringComparison.Ordinal)?"buyButton":name.EndsWith("DailyRewardView",StringComparison.Ordinal)||name.EndsWith("QuestItemView",StringComparison.Ordinal)||name.EndsWith("AchievementItemView",StringComparison.Ordinal)?"claimButton":null;
                    if(button!=null&&(!(Get(component,button) is UnityEngine.Object linked)||!linked))add(true,"UI view missing button: "+name+"."+button,component);
                }
                if(name=="BaseUnity.EconomyModules.WalletModule") {
                    string slot=Get(component,"saveSlot") as string;
                    if(!System.Text.RegularExpressions.Regex.IsMatch(slot??"",@"\A[a-zA-Z0-9_-]{1,64}\z"))add(true,"Wallet save slot is invalid",component);
                    Require(components,component,"BaseUnity.Save.SaveModule",add);
                }
                if(name=="BaseUnity.Localization.LocalizationModule") {
                    string lang=Get(component,"defaultLanguage") as string;if(lang!="en"&&lang!="vi")add(true,"Localization default language must be en or vi",component);
                    var seen=new HashSet<string>();foreach(var e in Items(Get(Get(component,"table"),"entries")))if(e==null||string.IsNullOrWhiteSpace(Get(e,"key") as string)||!seen.Add(Get(e,"key") as string)||string.IsNullOrEmpty(Get(e,"english") as string)||string.IsNullOrEmpty(Get(e,"vietnamese") as string))add(true,"Localization table contains duplicate/empty entries",component);
                    Require(components,component,"BaseUnity.Save.SaveModule",add);
                }
                if(name=="BaseUnity.FlowEconomy.FlowEconomyModule") {
                    var config=Get(component,"config");if(!(config is UnityEngine.Object o)||!o)add(true,"Assign FlowRewardConfig",component);
                    else { Rewards(Get(config,"winRewards"),component,add,true);Rewards(Get(config,"loseRewards"),component,add,true); }
                    Require(components,component,"BaseUnity.EconomyModules.WalletModule",add);Require(components,component,"BaseUnity.Flow.GameFlowModule",add);
                }
                if(name=="BaseUnity.EconomyModules.ShopModule"||name=="BaseUnity.EconomyModules.QuestModule"||name=="BaseUnity.EconomyModules.AchievementModule") {
                    var catalog=Get(component,"catalog");if(!(catalog is UnityEngine.Object o)||!o)add(true,"Assign catalog: "+name,component);
                    else {
                        bool shop=name.EndsWith("ShopModule",StringComparison.Ordinal);var seen=new HashSet<string>();
                        foreach(var d in Items(Get(catalog,shop?"offers":"objectives"))) {
                            if(d==null||!Id(Get(d,"id"))||!seen.Add(Get(d,"id") as string)) { add(true,"Duplicate/invalid catalog ID: "+name,component);continue; }
                            Rewards(Get(d,"rewards"),component,add,false);
                            if(shop) { if(!Id(Get(d,"currency"))||Convert.ToInt64(Get(d,"price"))<0||Convert.ToInt32(Get(d,"purchaseLimit"))<0)add(true,"Invalid shop price/currency/limit",component); }
                            else if(!Id(Get(d,"eventId"))||Convert.ToInt64(Get(d,"target"))<=0||(name.EndsWith("AchievementModule",StringComparison.Ordinal)&&Convert.ToBoolean(Get(d,"dailyReset"))))add(true,"Invalid objective event/target/reset",component);
                        }
                    }
                    Require(components,component,"BaseUnity.EconomyModules.WalletModule",add);
                }
                if(name=="BaseUnity.EconomyModules.DailyRewardModule") {
                    var schedule=Get(component,"schedule");if(!(schedule is UnityEngine.Object o)||!o)add(true,"Assign DailyRewardSchedule",component);
                    else { var days=Items(Get(schedule,"days"));if(!Id(Get(schedule,"id"))||Convert.ToInt32(Get(schedule,"cooldownSeconds"))<1||days.Length==0)add(true,"Invalid daily schedule ID/cooldown/days",component);foreach(var d in days)Rewards(Get(d,"rewards"),component,add,false); }
                    Require(components,component,"BaseUnity.EconomyModules.WalletModule",add);
                }
                if(name=="BaseUnity.Energy.EnergyModule"||name=="BaseUnity.Progression.PlayerProgressionModule") {
                    var config=Get(component,"config") as UnityEngine.Object;
                    if(!config)add(true,"Assign module config: "+name,component);
                    else try { config.GetType().GetMethod("Validate").Invoke(config,null); }catch(Exception error) { add(true,error.InnerException?.Message??error.Message,component); }
                    Require(components,component,"BaseUnity.EconomyModules.WalletModule",add);
                }
                if(name=="BaseUnity.ProgressionEvents.ProgressionEventsModule") {
                    Require(components,component,"BaseUnity.Progression.PlayerProgressionModule",add);
                    if(Convert.ToInt64(Get(component,"experiencePerWin"))<0||Convert.ToInt64(Get(component,"experiencePerLoss"))<0)add(true,"Progression event XP cannot be negative",component);
                }
                if(name=="BaseUnity.Demo.PlayableDemoController") {
                    var config=Get(component,"config") as UnityEngine.Object;
                    if(!config)add(true,"Assign PlayableDemoConfig",component);
                    else try { config.GetType().GetMethod("Validate").Invoke(config,null); }catch(Exception error) { add(true,error.InnerException?.Message??error.Message,component); }
                    foreach(string field in new[]{"board","target","scoreLabel","timeLabel"})if(!(Get(component,field) is UnityEngine.Object linked)||!linked)add(true,"Demo missing reference: "+field,component);
                    foreach(string dependency in new[]{"BaseUnity.Energy.EnergyModule","BaseUnity.Progression.PlayerProgressionModule","BaseUnity.ProgressionEvents.ProgressionEventsModule","BaseUnity.FlowEconomy.FlowEconomyModule"})Require(components,component,dependency,add);
                    var energyModule=components.FirstOrDefault(c=>c.gameObject==component.gameObject&&c.GetType().FullName=="BaseUnity.Energy.EnergyModule");
                    var energyConfig=Get(energyModule,"config");if(config&&energyConfig!=null&&Convert.ToInt32(Get(config,"energyPerRun"))>Convert.ToInt32(Get(energyConfig,"capacity")))add(true,"Demo run energy cost exceeds energy capacity",component);
                }
                if(name=="BaseUnity.IntegrationModules.AdsModule")Require(components,component,"BaseUnity.IntegrationModules.ConsentModule",add);
                if(name.StartsWith("BaseUnity.IntegrationModules.",StringComparison.Ordinal)&&name.EndsWith("Module",StringComparison.Ordinal))ValidateIntegration(component,target,development??EditorUserBuildSettings.development,add);
            }
            return issues;
        }
        private static void Require(Component[] all,Component owner,string type,Action<bool,string,UnityEngine.Object> add)
        { if(!all.Any(c=>c.gameObject==owner.gameObject&&Kind(c,type)&&(!(c is Behaviour b)||b.enabled)))add(true,owner.GetType().Name+" requires enabled "+type+" on the same bootstrap",owner); }
        private static void Rewards(object value,Component context,Action<bool,string,UnityEngine.Object> add,bool empty)
        { var rewards=Items(value);if(value==null||(!empty&&rewards.Length==0))add(true,"Empty reward definition",context);foreach(var reward in rewards)if(reward==null||!Id(Get(reward,"id"))||Convert.ToInt64(Get(reward,"amount"))<=0||Convert.ToInt32(Get(reward,"kind"))<0||Convert.ToInt32(Get(reward,"kind"))>1)add(true,"Invalid reward ID/amount/kind",context); }
        private static void ValidateIntegration(Component component,BuildTarget target,bool development,Action<bool,string,UnityEngine.Object> add)
        {
            var settings=Get(component,"settings");if(settings==null) { add(true,"Missing integration settings",component);return; }if(!Convert.ToBoolean(Get(settings,"enabled")))return;
            if(component.GetType().Name=="AdsModule"&&!Convert.ToBoolean(Get(settings,"banner"))&&!Convert.ToBoolean(Get(settings,"interstitial"))&&!Convert.ToBoolean(Get(settings,"rewarded")))return;
            if(component.GetType().Name=="FirebaseModule"&&!Convert.ToBoolean(Get(settings,"analytics"))&&!Convert.ToBoolean(Get(settings,"crashlytics"))&&!Convert.ToBoolean(Get(settings,"remoteConfig")))return;
            var provider=Get(component,"provider") as UnityEngine.Object;if(!provider) { add(true,"Assign integration provider: "+component.GetType().Name,component);return; }
            string type=provider.GetType().FullName;
            if(type.Contains("Simulated")) { add(!development,"Simulation provider is development-only: "+type,component);return; }
            string sdk=type=="BaseUnity.Integrations.AdMobProvider"?"GoogleMobileAds.Api.MobileAds":type=="BaseUnity.Integrations.AppLovinMaxProvider"?"MaxSdk":type=="BaseUnity.Integrations.GoogleUmpProvider"?"GoogleMobileAds.Ump.Api.ConsentInformation":type=="BaseUnity.Integrations.FirebaseSdkProvider"?"Firebase.FirebaseApp":null;
            if(sdk!=null&&!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType(sdk)!=null))add(true,"SDK missing: "+sdk,component);
            if(type.EndsWith("FirebaseSdkProvider",StringComparison.Ordinal)) {
                string pattern=target==BuildTarget.iOS?"GoogleService-Info.plist":"google-services.json";
                if(AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(pattern)).Select(AssetDatabase.GUIDToAssetPath).All(p=>Path.GetFileName(p)!=pattern))add(true,"Firebase app configuration missing: "+pattern,component);
                if(target==BuildTarget.Android&&!Directory.GetFiles(Application.dataPath,"AndroidManifest.xml",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p).Contains("firebase_analytics_collection_enabled")))add(false,"Verify Firebase automatic collection is disabled by the adapter's Android library manifest",component);
            }
            if((type.EndsWith("AdMobProvider",StringComparison.Ordinal)||type.EndsWith("GoogleUmpProvider",StringComparison.Ordinal))&&(target==BuildTarget.Android||target==BuildTarget.iOS)) {
                string field=target==BuildTarget.iOS?"adMobIOSAppId":"adMobAndroidAppId";
                var settingsAssets=AssetDatabase.FindAssets("t:GoogleMobileAdsSettings").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadMainAssetAtPath).Where(o=>o).ToArray();
                if(settingsAssets.Length==0)add(true,"Configure an official GoogleMobileAdsSettings asset and AdMob App ID for ads/UMP",component);
                foreach(var asset in settingsAssets)using(var data=new SerializedObject(asset)) {
                    string actual=data.FindProperty(field)?.stringValue;
                    if(string.IsNullOrWhiteSpace(actual))add(true,"Official Google Mobile Ads App ID is empty",component);
                    else if(component.GetType().Name=="AdsModule"&&actual!=(Get(settings,target==BuildTarget.iOS?"iosAppId":"androidAppId") as string))add(true,"Module App ID differs from official Google Mobile Ads settings",component);
                }
            }
            if(type.EndsWith("AppLovinMaxProvider",StringComparison.Ordinal)&&(target==BuildTarget.Android||target==BuildTarget.iOS)) {
                var settingsAssets=AssetDatabase.FindAssets("t:AppLovinSettings").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadMainAssetAtPath).Where(o=>o).ToArray();
                if(settingsAssets.Length==0)add(true,"Configure the official AppLovinSettings asset and MAX SDK key",component);
                foreach(var asset in settingsAssets)using(var data=new SerializedObject(asset)) {
                    string actual=data.FindProperty("sdkKey")?.stringValue;
                    if(string.IsNullOrWhiteSpace(actual)||actual!=(Get(settings,"maxSdkKey") as string))add(true,"Module MAX key is empty or differs from official AppLovin settings",component);
                }
            }
            if(component.GetType().Name=="AdsModule"&&(target==BuildTarget.Android||target==BuildTarget.iOS)) {
                bool ios=target==BuildTarget.iOS;
                if(type.EndsWith("AppLovinMaxProvider",StringComparison.Ordinal)&&Convert.ToInt32(Get(settings,"network"))!=1)add(true,"Select AppLovinMax network for MAX provider",component);
                if(type.EndsWith("AdMobProvider",StringComparison.Ordinal)&&Convert.ToInt32(Get(settings,"network"))!=0)add(true,"Select AdMob network for AdMob provider",component);
                if(type.EndsWith("AdMobProvider",StringComparison.Ordinal)&&string.IsNullOrWhiteSpace(Get(settings,ios?"iosAppId":"androidAppId") as string))add(true,"Ads App ID is missing; configure the official SDK settings too",component);
                if(type.EndsWith("AppLovinMaxProvider",StringComparison.Ordinal)&&string.IsNullOrWhiteSpace(Get(settings,"maxSdkKey") as string))add(true,"MAX SDK key is missing; configure official MAX settings too",component);
                if(!Convert.ToBoolean(Get(settings,"testMode")))foreach(string format in new[]{"Banner","Interstitial","Rewarded"})if(Convert.ToBoolean(Get(settings,format.ToLowerInvariant()))&&string.IsNullOrWhiteSpace(Get(settings,(ios?"ios":"android")+format) as string))add(true,"Ad unit ID missing: "+format,component);
            }
        }
        public static List<ModuleValidationIssue> ValidateBuildScenes(BuildTarget target,bool? development=null)
        {
            var issues=new List<ModuleValidationIssue>();foreach(var scene in EditorBuildSettings.scenes.Where(s=>s.enabled)) {
                if(!File.Exists(scene.path)) { issues.Add(new ModuleValidationIssue(true,"Build scene does not exist: "+scene.path));continue; }
                var preview=EditorSceneManager.OpenPreviewScene(scene.path);
                try { issues.AddRange(ValidateRoots(preview.GetRootGameObjects(),target,development).Select(i=>new ModuleValidationIssue(i.error,scene.path+": "+i.message))); }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }return issues;
        }
        [MenuItem("Tools/BaseUnity/Validate Modular Build")]
        public static void ValidateMenu()
        { var issues=ValidateBuildScenes(EditorUserBuildSettings.activeBuildTarget);foreach(var i in issues)if(i.error)Debug.LogError(i.message);else Debug.LogWarning(i.message);if(issues.Count==0)Debug.Log("BaseUnity modular configuration: no issues found"); }
    }
    public sealed class ModularBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -900;
        public void OnPreprocessBuild(BuildReport report)
        { var issues=ModularBuildValidator.ValidateBuildScenes(report.summary.platform,(report.summary.options&BuildOptions.Development)!=0);foreach(var i in issues.Where(i=>!i.error))Debug.LogWarning(i.message);var errors=issues.Where(i=>i.error).ToArray();if(errors.Length>0)throw new BuildFailedException(string.Join("\n",errors.Select(i=>i.message))); }
    }
}
