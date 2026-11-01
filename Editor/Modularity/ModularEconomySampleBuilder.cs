using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static BaseUnity.Modularity.ModularGameSampleBuilder;
namespace BaseUnity.Modularity
{
    public static class ModularEconomySampleBuilder
    {
        private const string Ns="BaseUnity.EconomyModules.";
        [MenuItem("Tools/BaseUnity/Create Modular Economy Sample")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Stop Play mode and wait for compilation");
            if(Find("BaseUnity.Features.FeatureServices")!=null)throw new InvalidOperationException("Use a clean modular project, not the legacy demo");
            Need(Ns+"UI.WalletBarView");
            string folder=EditorUtility.SaveFilePanel("Name a NEW economy sample folder under Assets",Application.dataPath,"EconomySample","");
            if(folder.Length>0)CreateAt(folder);
        }
        private static object Object(string type,params object[] pairs)
        { var value=Activator.CreateInstance(Need(Ns+type));for(int i=0;i<pairs.Length;i+=2)Set(value,(string)pairs[i],pairs[i+1]);return value; }
        private static Array ArrayOf(string type,params object[] values)
        { var array=Array.CreateInstance(Need(Ns+type),values.Length);for(int i=0;i<values.Length;i++)array.SetValue(values[i],i);return array; }
        private static object Reward(string id,long amount,bool item=false) => Object("EconomyReward","id",id,"amount",amount,"kind",Enum.ToObject(Need(Ns+"RewardKind"),item?1:0));
        private static ScriptableObject Asset(string type,string path)
        { var asset=ScriptableObject.CreateInstance(Need(Ns+type));AssetDatabase.CreateAsset(asset,path);return asset; }
        private static void RowRect(Component label,float y,float height=.19f)
        { Rect(label.gameObject,new Vector2(.06f,y),new Vector2(.94f,y+height),Vector2.zero,Vector2.zero); }
        public static void CreateAt(string absoluteFolder)
        {
            Need(Ns+"UI.WalletBarView");
            string folder=Path.GetFullPath(absoluteFolder).TrimEnd(Path.DirectorySeparatorChar),assets=Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar);
            if(!folder.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Directory.Exists(folder)||File.Exists(folder))throw new IOException("Choose a new folder inside Assets; existing content is never overwritten");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene first");
            string previous=EditorSceneManager.GetActiveScene().path,output="Assets/"+folder.Substring(assets.Length+1).Replace('\\','/');
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            try {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var theme=AssetTheme(output);
                var root=new GameObject("Economy Sample");root.SetActive(false);
                Add(root,"BaseUnity.Save.SaveModule");var wallet=Add(root,Ns+"WalletModule");Set(wallet,"saveSlot","baseunity_modular_economy_demo");Add(root,"BaseUnity.UI.UIModule");Add(root,"BaseUnity.Core.ModuleBootstrap");
                var canvas=Node("Economy UI",root.transform);var c=Add(canvas,"UnityEngine.Canvas");Set(c,"renderMode",RenderMode.ScreenSpaceOverlay);
                var scaler=Add(canvas,"UnityEngine.UI.CanvasScaler");Set(scaler,"uiScaleMode",Enum.ToObject(Need("UnityEngine.UI.CanvasScaler+ScaleMode"),1));Set(scaler,"referenceResolution",new Vector2(720,1280));Add(canvas,"UnityEngine.UI.GraphicRaycaster");
                var menu=Add(canvas,Ns+"UI.EconomyMenuView");
                var home=Panel("EconomyHome",canvas.transform,theme);Set(menu,"home",home);
                Label("ECONOMY SAMPLE",home.transform,.85f,theme);
                var bar=Node("WalletBar",home.transform);Rect(bar,new Vector2(.05f,.73f),new Vector2(.95f,.84f),Vector2.zero,Vector2.zero);
                var walletView=Add(bar,Ns+"UI.WalletBarView");var coin=Label("coin",bar.transform,0,theme);Rect(coin.gameObject,Vector2.zero,new Vector2(.5f,1),Vector2.zero,Vector2.zero);
                var gem=Label("gem",bar.transform,0,theme);Rect(gem.gameObject,new Vector2(.5f,0),Vector2.one,Vector2.zero,Vector2.zero);Set(walletView,"coinLabel",coin);Set(walletView,"gemLabel",gem);
                Set(walletView,"message",Label("",home.transform,.03f,theme));Button("Demo starter (once)",home.transform,.63f,theme,walletView,"GrantDemoStarter");
                PrefabUtility.SaveAsPrefabAssetAndConnect(bar,output+"/WalletBar.prefab",InteractionMode.AutomatedAction);
                float y=.50f;
                if(Find(Ns+"UI.ShopItemView")!=null) {
                    var catalog=Asset("ShopCatalog",output+"/ShopCatalog.asset");
                    Set(catalog,"offers",ArrayOf("ShopOffer",Object("ShopOffer","id","red_skin","title","Red skin","price",100L,"rewards",ArrayOf("EconomyReward",Reward("red_skin",1,true))),Object("ShopOffer","id","gem_pack","title","10 Gems","price",250L,"purchaseLimit",0,"rewards",ArrayOf("EconomyReward",Reward("gem",10)))));
                    var module=Add(root,Ns+"ShopModule");Set(module,"catalog",catalog);
                    var panel=Panel("ShopPopup",canvas.transform,theme);Set(menu,"shop",panel);Label("SHOP",panel.transform,.85f,theme);
                    var shopMessage=Label("",panel.transform,.11f,theme);
                    for(int i=0;i<2;i++) { var row=Row("ShopItem"+i,panel.transform,i);var view=Add(row,Ns+"UI.ShopItemView");Set(view,"offerId",i==0?"red_skin":"gem_pack");Set(view,"titleLabel",RowLabel("Item",row.transform,.72f,theme));Set(view,"priceLabel",RowLabel("Price",row.transform,.49f,theme));Set(view,"ownedLabel",RowLabel("Owned",row.transform,.26f,theme));var b=Button("Buy",row.transform,0,theme,view,"Buy");RowRect(b,0,.23f);Set(view,"buyButton",b);Set(view,"message",shopMessage); }
                    Finish(panel,menu,theme,output);Button("Shop",home.transform,y,theme,menu,"Shop");y-=.11f;
                }
                if(Find(Ns+"UI.DailyRewardView")!=null) {
                    var config=Asset("DailyRewardSchedule",output+"/DailyRewardSchedule.asset");
                    Set(config,"days",ArrayOf("DailyRewardDay",Enumerable.Range(1,7).Select(i=>Object("DailyRewardDay","rewards",ArrayOf("EconomyReward",Reward("coin",50L*i)))).ToArray()));
                    var module=Add(root,Ns+"DailyRewardModule");Set(module,"schedule",config);
                    var panel=Panel("DailyRewardPopup",canvas.transform,theme);Set(menu,"daily",panel);Label("DAILY REWARD",panel.transform,.85f,theme);var view=Add(panel.gameObject,Ns+"UI.DailyRewardView");
                    Set(view,"dayLabel",Label("Day",panel.transform,.68f,theme));Set(view,"rewardLabel",Label("Reward",panel.transform,.56f,theme));Set(view,"timerLabel",Label("Ready",panel.transform,.44f,theme));Set(view,"claimButton",Button("Claim",panel.transform,.30f,theme,view,"Claim"));Set(view,"message",Label("",panel.transform,.18f,theme));
                    Finish(panel,menu,theme,output);Button("Daily Reward",home.transform,y,theme,menu,"Daily");y-=.11f;
                }
                foreach(string kind in new[]{"Quest","Achievement"})if(Find(Ns+"UI."+kind+"ItemView")!=null) {
                    var config=Asset(kind+"Catalog",output+"/"+kind+"Catalog.asset");bool quest=kind=="Quest";Set(config,"objectives",ArrayOf("ObjectiveDefinition",Object("ObjectiveDefinition","id","win_3","title",quest?"Win 3 games (daily UTC)":"Win 3 games total","target",3L,"dailyReset",quest,"rewards",ArrayOf("EconomyReward",Reward("coin",quest?100:200)))));
                    var module=Add(root,Ns+kind+"Module");Set(module,"catalog",config);
                    var panel=Panel(kind+"Popup",canvas.transform,theme);Set(menu,quest?"quests":"achievements",panel);Label(kind.ToUpperInvariant(),panel.transform,.85f,theme);
                    var row=Row(kind+"Item",panel.transform,0);var view=Add(row,Ns+"UI."+kind+"ItemView");Set(view,"titleLabel",RowLabel("Title",row.transform,.72f,theme));Set(view,"progressLabel",RowLabel("Progress",row.transform,.49f,theme));Set(view,"rewardLabel",RowLabel("Reward",row.transform,.26f,theme));var b=Button("Claim",row.transform,0,theme,view,"Claim");RowRect(b,0,.23f);Set(view,"claimButton",b);Set(view,"message",Label("",panel.transform,.16f,theme));Button("Demo win +1",panel.transform,.27f,theme,view,"RecordDemoWin");
                    Finish(panel,menu,theme,output);Button(kind+"s",home.transform,y,theme,menu,quest?"Quests":"Achievements");y-=.11f;
                }
                AttachLocalization(root,canvas,theme,output);
                var events=new GameObject("EventSystem");events.transform.SetParent(root.transform,false);Add(events,"UnityEngine.EventSystems.EventSystem");
#if ENABLE_INPUT_SYSTEM
                Add(events,"UnityEngine.InputSystem.UI.InputSystemUIInputModule");
#else
                Add(events,"UnityEngine.EventSystems.StandaloneInputModule");
#endif
                root.SetActive(true);PrefabUtility.SaveAsPrefabAssetAndConnect(root,output+"/EconomyRoot.prefab",InteractionMode.AutomatedAction);
                EditorSceneManager.SaveScene(scene,output+"/EconomySample.unity");var entries=EditorBuildSettings.scenes.ToList();entries.Add(new EditorBuildSettingsScene(output+"/EconomySample.unity",true));EditorBuildSettings.scenes=entries.ToArray();
                AssetDatabase.SaveAssets();Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(output+"/EconomyRoot.prefab");
            } catch { if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);throw; }
        }
        private static ScriptableObject AssetTheme(string output)
        { var t=ScriptableObject.CreateInstance(Need("BaseUnity.UI.UITheme"));Set(t,"font",Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));AssetDatabase.CreateAsset(t,output+"/Theme.asset");return t; }
        private static Component Panel(string name,Transform parent,ScriptableObject theme)
        { var go=Node(name,parent);Full(go);Add(go,"UnityEngine.UI.Image");BindTheme(go,theme,true);return Add(go,"BaseUnity.UI.UIPanel"); }
        private static GameObject Row(string name,Transform parent,int i)
        { var row=Node(name,parent);float y=.51f-i*.29f;Rect(row,new Vector2(.06f,y),new Vector2(.94f,y+.27f),Vector2.zero,Vector2.zero);return row; }
        private static Component RowLabel(string text,Transform parent,float y,ScriptableObject theme)
        { var label=Label(text,parent,0,theme);RowRect(label,y);return label; }
        private static void Finish(Component panel,Component menu,ScriptableObject theme,string output)
        { Button("Close",panel.transform,.03f,theme,panel,"Hide");panel.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject,output+"/"+panel.name+".prefab",InteractionMode.AutomatedAction); }
    }
}
