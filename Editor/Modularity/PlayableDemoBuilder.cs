using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using static BaseUnity.Modularity.ModularGameSampleBuilder;
namespace BaseUnity.Modularity
{
    /// <summary>Composes the existing screens into one persistent root and one shared save profile.</summary>
    public static class PlayableDemoBuilder
    {
        private static object Field(object value,string name) => value.GetType().GetField(name).GetValue(value);
        private static Component Get(GameObject go,string type) => go.GetComponent(Need(type));
        private static Component Ensure(GameObject go,string type) => Get(go,type)??Add(go,type);
        private static bool IsKind(Component value,string type) { for(var t=value.GetType();t!=null;t=t.BaseType)if(t.FullName==type)return true;return false; }
        private static void Rewire(Component button,Component target,string method)
        {
            var evt=(UnityEventBase)button.GetType().GetProperty("onClick").GetValue(button,null);
            for(int i=evt.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(evt,i);
            UnityEventTools.AddVoidPersistentListener(evt,(UnityAction)Delegate.CreateDelegate(typeof(UnityAction),target,method));
        }
        private static void Position(Component c,float y,float height=.08f) { Rect(c.gameObject,new Vector2(.1f,y),new Vector2(.9f,y+height),Vector2.zero,Vector2.zero); }
        private static ScriptableObject Config(string type,string path)
        { var asset=ScriptableObject.CreateInstance(Need(type));AssetDatabase.CreateAsset(asset,path);return asset; }
        [MenuItem("Tools/BaseUnity/Create Complete Playable Demo")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Stop Play mode and wait for compilation");
            if(Find("BaseUnity.Features.FeatureServices")!=null)throw new InvalidOperationException("Use a clean modular project");
            Need("BaseUnity.Demo.PlayableDemoController");string folder=EditorUtility.SaveFilePanel("Name a NEW complete demo folder under Assets",Application.dataPath,"PlayableDemo","");if(folder.Length>0)CreateAt(folder);
        }
        public static void CreateAt(string absoluteFolder)
        {
            Need("BaseUnity.Demo.PlayableDemoController");
            string folder=Path.GetFullPath(absoluteFolder).TrimEnd(Path.DirectorySeparatorChar),assets=Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar);
            if(!folder.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Directory.Exists(folder)||File.Exists(folder))throw new IOException("Choose a new folder inside Assets; existing content is never overwritten");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene first");
            string output="Assets/"+folder.Substring(assets.Length+1).Replace('\\','/');
            ModularGameSampleBuilder.CreateAt(Path.Combine(folder,"Flow"));EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());ModularEconomySampleBuilder.CreateAt(Path.Combine(folder,"Economy"));
            var scene=EditorSceneManager.OpenScene(output+"/Flow/Bootstrap.unity");
            var root=scene.GetRootGameObjects().Single(go=>go.GetComponent(Need("BaseUnity.Core.ModuleBootstrap")));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name="Complete Playable Demo";root.SetActive(false);
            var flow=Get(root,"BaseUnity.Flow.GameFlowModule");Set(flow,"homeScene",output+"/Home.unity");Set(flow,"ingameScene",output+"/Ingame.unity");
            Set(Get(root,"BaseUnity.EconomyModules.WalletModule"),"saveSlot","baseunity_complete_demo");
            var flowView=root.GetComponentsInChildren<Component>(true).Single(c=>c&&c.GetType().FullName=="BaseUnity.FlowUI.GameFlowView");
            var gameCanvas=flowView.gameObject;var oldHome=(Component)Field(flowView,"home");UnityEngine.Object.DestroyImmediate(oldHome.gameObject);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(output+"/Economy/EconomyRoot.prefab");var economy=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(economy,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);economy.SetActive(false);
            foreach(var component in economy.GetComponents<Component>().Where(c=>c&&(IsKind(c,"BaseUnity.Core.ModuleInstaller")||IsKind(c,"BaseUnity.Core.ModuleBootstrap"))).ToArray())UnityEngine.Object.DestroyImmediate(component);
            foreach(var tr in economy.GetComponentsInChildren<Transform>(true).Where(t=>t.GetComponent(Need("UnityEngine.EventSystems.EventSystem"))).ToArray())UnityEngine.Object.DestroyImmediate(tr.gameObject);
            var menu=economy.GetComponentsInChildren<Component>(true).Single(c=>c&&c.GetType().FullName=="BaseUnity.EconomyModules.UI.EconomyMenuView");
            var economyCanvas=menu.gameObject;economyCanvas.transform.SetParent(root.transform,false);var home=(Component)Field(menu,"home");home.gameObject.name="Home";Set(flowView,"home",home);UnityEngine.Object.DestroyImmediate(economy);
            foreach(string name in new[]{"Shop","DailyReward","Quest","Achievement"}) {
                string type="BaseUnity.EconomyModules."+name+"Module";var component=Ensure(root,type);string field=name=="DailyReward"?"schedule":"catalog";
                string asset=output+"/Economy/"+(name=="DailyReward"?"DailyRewardSchedule":name+"Catalog")+".asset";Set(component,field,AssetDatabase.LoadMainAssetAtPath(asset));
            }
            var energyConfig=Config("BaseUnity.Energy.EnergyConfig",output+"/EnergyConfig.asset");Set(Ensure(root,"BaseUnity.Energy.EnergyModule"),"config",energyConfig);
            var progressionConfig=Config("BaseUnity.Progression.PlayerProgressionConfig",output+"/PlayerProgressionConfig.asset");Set(Ensure(root,"BaseUnity.Progression.PlayerProgressionModule"),"config",progressionConfig);Ensure(root,"BaseUnity.ProgressionEvents.ProgressionEventsModule");
            var controller=Add(root,"BaseUnity.Demo.PlayableDemoController");Set(controller,"config",Config("BaseUnity.Demo.PlayableDemoConfig",output+"/PlayableDemoConfig.asset"));
            var theme=AssetDatabase.LoadAssetAtPath<ScriptableObject>(output+"/Flow/Theme.asset");
            foreach(var binding in root.GetComponentsInChildren<Component>(true).Where(c=>c&&c.GetType().FullName=="BaseUnity.UI.UIThemeBinding")) { Set(binding,"theme",theme);binding.GetType().GetMethod("Apply").Invoke(binding,null); }
            // Only one pair of language buttons is needed; both canvases share the same LocalizationService.
            foreach(var tr in gameCanvas.transform.Cast<Transform>().Where(t=>t.name=="EN"||t.name=="VI").ToArray())UnityEngine.Object.DestroyImmediate(tr.gameObject);
            foreach(var button in home.GetComponentsInChildren<Component>(true).Where(c=>c&&c.GetType().FullName=="UnityEngine.UI.Button"&&c.name=="Demo starter (once)").ToArray())UnityEngine.Object.DestroyImmediate(button.gameObject);
            foreach(var button in economyCanvas.GetComponentsInChildren<Component>(true).Where(c=>c&&c.GetType().FullName=="UnityEngine.UI.Button"&&c.name=="Demo win +1").ToArray())UnityEngine.Object.DestroyImmediate(button.gameObject);
            var title=home.GetComponentsInChildren<Component>(true).FirstOrDefault(c=>c&&c.GetType().FullName=="UnityEngine.UI.Text"&&(string)c.GetType().GetProperty("text").GetValue(c,null)=="ECONOMY SAMPLE");if(title) { Set(title,"text","MOBILE DEMO");Position(title,.9f,.07f); }
            var wallet=home.transform.Find("WalletBar");Rect(wallet.gameObject,new Vector2(.05f,.81f),new Vector2(.95f,.89f),Vector2.zero,Vector2.zero);
            var energyBar=Node("EnergyBar",home.transform);Rect(energyBar,new Vector2(.05f,.74f),new Vector2(.54f,.81f),Vector2.zero,Vector2.zero);
            var energyView=Add(energyBar,"BaseUnity.Energy.UI.EnergyBarView");var energyLabel=Label("Energy",energyBar.transform,0,theme);Rect(energyLabel.gameObject,new Vector2(0,.42f),Vector2.one,Vector2.zero,Vector2.zero);Set(energyLabel,"fontSize",24);Set(energyView,"energyLabel",energyLabel);
            var refillTimer=Label("Energy full",energyBar.transform,0,theme);Rect(refillTimer.gameObject,Vector2.zero,new Vector2(1,.42f),Vector2.zero,Vector2.zero);Set(energyView,"timerLabel",refillTimer);
            var refill=Button("Refill 50 coin",home.transform,.74f,theme,controller,"BuyEnergy");Rect(refill.gameObject,new Vector2(.55f,.75f),new Vector2(.95f,.81f),Vector2.zero,Vector2.zero);Set(controller,"refillButton",refill);
            var progressBar=Node("PlayerProgressionBar",home.transform);Rect(progressBar,new Vector2(.05f,.67f),new Vector2(.95f,.74f),Vector2.zero,Vector2.zero);
            var progressView=Add(progressBar,"BaseUnity.Progression.UI.PlayerProgressionBarView");var level=Label("Level",progressBar.transform,0,theme);Rect(level.gameObject,Vector2.zero,new Vector2(.5f,1),Vector2.zero,Vector2.zero);Set(level,"fontSize",24);Set(progressView,"levelLabel",level);
            var xp=Label("EXP",progressBar.transform,0,theme);Rect(xp.gameObject,new Vector2(.5f,0),Vector2.one,Vector2.zero,Vector2.zero);Set(xp,"fontSize",24);Set(progressView,"experienceLabel",xp);
            Button("Play",home.transform,.56f,theme,controller,"Play");
            foreach(var item in new[]{Tuple.Create("Shop",.45f),Tuple.Create("Daily Reward",.34f),Tuple.Create("Quests",.23f),Tuple.Create("Achievements",.12f)}) {
                var button=home.GetComponentsInChildren<Component>(true).First(c=>c&&c.GetType().FullName=="UnityEngine.UI.Button"&&c.name==item.Item1);Position(button,item.Item2);
            }
            var ingame=(Component)Field(flowView,"ingame");
            foreach(var button in ingame.GetComponentsInChildren<Component>(true).Where(c=>c&&c.GetType().FullName=="UnityEngine.UI.Button").ToArray())if(button.name=="Demo Win"||button.name=="Demo Lose")UnityEngine.Object.DestroyImmediate(button.gameObject);else if(button.name=="Pause")Position(button,.06f,.07f);
            Position((Component)Field(flowView,"levelLabel"),.88f,.06f);
            Set(controller,"scoreLabel",Label("Targets",ingame.transform,.71f,theme));Set(controller,"timeLabel",Label("Time",ingame.transform,.63f,theme));
            var board=Node("TargetBoard",ingame.transform);Rect(board,new Vector2(.08f,.14f),new Vector2(.92f,.6f),Vector2.zero,Vector2.zero);Add(board,"UnityEngine.UI.Image");BindTheme(board,theme,true);Set(controller,"board",board.transform);
            var target=Node("TapTarget",board.transform);var targetView=Add(target,"BaseUnity.Demo.TapTargetView");var image=Add(target,"UnityEngine.UI.Image");BindTheme(target,theme);var targetButton=Add(target,"UnityEngine.UI.Button");Set(targetButton,"targetGraphic",image);Rewire(targetButton,targetView,"Hit");Add(target,"BaseUnity.UI.UIButtonFeedback");
            var targetRect=(RectTransform)target.transform;targetRect.anchorMin=targetRect.anchorMax=new Vector2(.5f,.5f);targetRect.sizeDelta=new Vector2(128,128);targetRect.anchoredPosition=Vector2.zero;var tap=Label("Tap",target.transform,0,theme);Full(tap.gameObject);Set(controller,"target",targetRect);
            foreach(var button in gameCanvas.GetComponentsInChildren<Component>(true).Where(c=>c&&c.GetType().FullName=="UnityEngine.UI.Button")) {
                string method=button.name=="Next Level"?"Next":button.name=="Retry"?"Retry":button.name=="Home"?"Home":button.name=="Resume"?"Resume":null;if(method!=null)Rewire(button,controller,method);
            }
            var status=Label("",gameCanvas.transform,.94f,theme);Position(status,.94f,.05f);Set(controller,"statusLabel",status);
            Set(controller,"winSummary",Label("",((Component)Field(flowView,"win")).transform,.68f,theme));Set(controller,"loseSummary",Label("",((Component)Field(flowView,"lose")).transform,.68f,theme));
            var resultStatus=(Component)Field(flowView,"resultStatus");if(resultStatus)Position(resultStatus,.03f);
            PrefabUtility.SaveAsPrefabAssetAndConnect(wallet.gameObject,output+"/WalletBar.prefab",InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(energyBar,output+"/EnergyBar.prefab",InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(progressBar,output+"/PlayerProgressionBar.prefab",InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(target,output+"/TapTarget.prefab",InteractionMode.AutomatedAction);
            foreach(var panel in root.GetComponentsInChildren<Component>(true).Where(c=>c&&c.GetType().FullName=="BaseUnity.UI.UIPanel").ToArray())PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject,output+"/"+panel.name+".prefab",InteractionMode.AutomatedAction);
            root.SetActive(true);PrefabUtility.SaveAsPrefabAssetAndConnect(root,output+"/CompleteGameRoot.prefab",InteractionMode.AutomatedAction);EditorSceneManager.SaveScene(scene,output+"/Bootstrap.unity");
            foreach(string name in new[]{"Home","Ingame"}) { var content=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject(name+" Content");EditorSceneManager.SaveScene(content,output+"/"+name+".unity"); }
            var entries=EditorBuildSettings.scenes.Where(s=>!s.path.StartsWith(output+"/Flow/",StringComparison.Ordinal)&&!s.path.StartsWith(output+"/Economy/",StringComparison.Ordinal)).ToList();foreach(string name in new[]{"Bootstrap","Home","Ingame"})entries.Add(new EditorBuildSettingsScene(output+"/"+name+".unity",true));EditorBuildSettings.scenes=entries.ToArray();
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(output+"/Bootstrap.unity");Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(output+"/CompleteGameRoot.prefab");
        }
    }
}
