using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
namespace BaseUnity.Modularity
{
    /// <summary>Reflection keeps the standalone manager free of runtime/UGUI references.</summary>
    public static class ModularGameSampleBuilder
    {
        internal static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
        internal static Type Need(string name) => Find(name) ?? throw new InvalidOperationException("Install the required module and wait for compilation: "+name);
        internal static Component Add(GameObject go,string name) => go.AddComponent(Need(name));
        internal static void Set(object target,string name,object value)
        { var type=target.GetType(); var field=type.GetField(name); if(field!=null)field.SetValue(target,value); else type.GetProperty(name).SetValue(target,value,null); if(target is UnityEngine.Object asset && EditorUtility.IsPersistent(asset))EditorUtility.SetDirty(asset); }
        internal static GameObject Node(string name,Transform parent)
        { var go=new GameObject(name,typeof(RectTransform)); if(parent)go.transform.SetParent(parent,false); return go; }
        internal static void Rect(GameObject go,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        { var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax; }
        internal static void Full(GameObject go) { Rect(go,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero); }
        internal static void BindTheme(GameObject go,ScriptableObject theme,bool background=false)
        { var binding=Add(go,"BaseUnity.UI.UIThemeBinding"); Set(binding,"theme",theme);Set(binding,"background",background);binding.GetType().GetMethod("Apply").Invoke(binding,null); }
        internal static Component Label(string text,Transform parent,float y,ScriptableObject theme)
        {
            var go=Node(text,parent); Rect(go,new Vector2(.08f,y),new Vector2(.92f,y+.08f),Vector2.zero,Vector2.zero);
            var label=Add(go,"UnityEngine.UI.Text");Set(label,"text",text);Set(label,"alignment",TextAnchor.MiddleCenter);Set(label,"raycastTarget",false);BindTheme(go,theme); return label;
        }
        internal static Component Button(string text,Transform parent,float y,ScriptableObject theme,Component target,string method)
        {
            var go=Node(text,parent);Rect(go,new Vector2(.15f,y),new Vector2(.85f,y+.08f),Vector2.zero,Vector2.zero);
            var image=Add(go,"UnityEngine.UI.Image");var button=Add(go,"UnityEngine.UI.Button");Set(button,"targetGraphic",image);BindTheme(go,theme);
            var label=Label(text,go.transform,0,theme);Full(label.gameObject);
            Add(go,"BaseUnity.UI.UIButtonFeedback");
            var evt=(UnityEventBase)button.GetType().GetProperty("onClick").GetValue(button,null);
            var action=(UnityAction)Delegate.CreateDelegate(typeof(UnityAction),target,method);
            UnityEventTools.AddVoidPersistentListener(evt,action); return button;
        }
        internal static void AttachLocalization(GameObject root,GameObject canvas,ScriptableObject theme,string output)
        {
            var module=Find("BaseUnity.Localization.LocalizationModule");if(module==null||Find("BaseUnity.Localization.UI.LocalizedCanvas")==null)return;
            var save=Need("BaseUnity.Save.SaveModule");if(!root.GetComponent(save))root.AddComponent(save);
            root.AddComponent(module);var localizer=Add(canvas,"BaseUnity.Localization.UI.LocalizedCanvas");
            var en=Button("EN",canvas.transform,.01f,theme,localizer,"English");Rect(en.gameObject,new Vector2(.02f,.005f),new Vector2(.15f,.05f),Vector2.zero,Vector2.zero);
            var vi=Button("VI",canvas.transform,.01f,theme,localizer,"Vietnamese");Rect(vi.gameObject,new Vector2(.85f,.005f),new Vector2(.98f,.05f),Vector2.zero,Vector2.zero);
        }
        [MenuItem("Tools/BaseUnity/Create Modular Game Sample")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Stop Play mode and wait for compilation.");
            Need("BaseUnity.FlowUI.GameFlowView");
            if(Find("BaseUnity.Features.FeatureServices")!=null)throw new InvalidOperationException("Create the sample in a clean modular project, not the legacy demo.");
            string selected=EditorUtility.SaveFilePanel("Name a NEW sample folder under Assets",Application.dataPath,"ModularGame","");
            if(string.IsNullOrEmpty(selected))return;
            CreateAt(selected);
        }
        public static void CreateAt(string absoluteFolder)
        {
            Need("BaseUnity.FlowUI.GameFlowView");
            string folder=Path.GetFullPath(absoluteFolder).TrimEnd(Path.DirectorySeparatorChar);
            string assets=Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar);
            if(!folder.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Directory.Exists(folder)||File.Exists(folder))throw new IOException("Choose a new folder inside Assets. Existing content is never overwritten.");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene first.");
            string previous=EditorSceneManager.GetActiveScene().path;
            string output="Assets/"+folder.Substring(assets.Length+1).Replace('\\','/');
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            try {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var theme=ScriptableObject.CreateInstance(Need("BaseUnity.UI.UITheme"));
                Set(theme,"font",Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));AssetDatabase.CreateAsset(theme,output+"/Theme.asset");
                // Persistent root retains the flow and UI while the content scenes change.
                var root=new GameObject("Modular Game");root.SetActive(false);
                var flow=Add(root,"BaseUnity.Flow.GameFlowModule");Set(flow,"homeScene",output+"/Home.unity");Set(flow,"ingameScene",output+"/Ingame.unity");
                Add(root,"BaseUnity.UI.UIModule");Add(root,"BaseUnity.Core.ModuleBootstrap");
                var canvas=Node("Game UI",root.transform);var c=Add(canvas,"UnityEngine.Canvas");Set(c,"renderMode",RenderMode.ScreenSpaceOverlay);
                var scaler=Add(canvas,"UnityEngine.UI.CanvasScaler");Set(scaler,"uiScaleMode",Enum.ToObject(Need("UnityEngine.UI.CanvasScaler+ScaleMode"),1));Set(scaler,"referenceResolution",new Vector2(720,1280));
                Add(canvas,"UnityEngine.UI.GraphicRaycaster");
                var view=Add(canvas,"BaseUnity.FlowUI.GameFlowView");
                if(Find("BaseUnity.FlowEconomy.FlowEconomyModule")!=null) {
                    Add(root,"BaseUnity.Save.SaveModule");var wallet=Add(root,"BaseUnity.EconomyModules.WalletModule");Set(wallet,"saveSlot","baseunity_modular_game_demo");
                    var config=ScriptableObject.CreateInstance(Need("BaseUnity.FlowEconomy.FlowRewardConfig"));AssetDatabase.CreateAsset(config,output+"/FlowRewardConfig.asset");
                    Set(Add(root,"BaseUnity.FlowEconomy.FlowEconomyModule"),"config",config);
                    foreach(string kind in new[]{"Quest","Achievement"})if(Find("BaseUnity.EconomyModules."+kind+"Module")!=null) {
                        var catalog=ScriptableObject.CreateInstance(Need("BaseUnity.EconomyModules."+kind+"Catalog"));
                        var definitions=Array.CreateInstance(Need("BaseUnity.EconomyModules.ObjectiveDefinition"),1);definitions.SetValue(Activator.CreateInstance(Need("BaseUnity.EconomyModules.ObjectiveDefinition")),0);
                        AssetDatabase.CreateAsset(catalog,output+"/"+kind+"Catalog.asset");Set(catalog,"objectives",definitions);Set(Add(root,"BaseUnity.EconomyModules."+kind+"Module"),"catalog",catalog);
                    }
                }
                string[] names={"Home","Ingame","Loading","Pause","Win","Lose"};
                foreach(string name in names) {
                    var panel=Node(name,canvas.transform);Full(panel);Add(panel,"UnityEngine.UI.Image");BindTheme(panel,theme,true);
                    var component=Add(panel,"BaseUnity.UI.UIPanel");Set(view,name.ToLowerInvariant(),component);
                    Label(name, panel.transform,.8f,theme);
                    if(name=="Home") {
                        Button("Play",panel.transform,.45f,theme,flow,"Play");
                        var walletType=Find("BaseUnity.EconomyModules.WalletModule");
                        if(walletType!=null&&root.GetComponent(walletType)&&Find("BaseUnity.EconomyModules.UI.WalletBarView")!=null) {
                            var bar=Add(panel,"BaseUnity.EconomyModules.UI.WalletBarView");Set(bar,"coinLabel",Label("coin: 0",panel.transform,.68f,theme));Set(bar,"gemLabel",Label("gem: 0",panel.transform,.58f,theme));
                        }
                    }
                    if(name=="Ingame") {
                        Set(view,"levelLabel",Label("Level",panel.transform,.7f,theme));
                        Button("Pause",panel.transform,.54f,theme,flow,"Pause");
                        Button("Demo Win",panel.transform,.4f,theme,flow,"Win");
                        Button("Demo Lose",panel.transform,.26f,theme,flow,"Lose");
                    }
                    if(name=="Pause")Button("Resume",panel.transform,.57f,theme,flow,"Resume");
                    if(name=="Win")Set(view,"nextButton",Button("Next Level",panel.transform,.57f,theme,flow,"Next"));
                    if(name=="Pause"||name=="Win"||name=="Lose") {
                        Button("Retry",panel.transform,.43f,theme,flow,"Retry");Button("Home",panel.transform,.29f,theme,flow,"Home");
                    }
                    panel.SetActive(false);
                    // Save each screen as a nested prefab for independent theme/layout edits.
                    PrefabUtility.SaveAsPrefabAssetAndConnect(panel,output+"/"+name+".prefab",InteractionMode.AutomatedAction);
                }
                if(Find("BaseUnity.FlowEconomy.FlowEconomyModule")!=null)Set(view,"resultStatus",Label("",canvas.transform,.92f,theme));
                AttachLocalization(root,canvas,theme,output);
                var events=new GameObject("EventSystem");events.transform.SetParent(root.transform,false);Add(events,"UnityEngine.EventSystems.EventSystem");
                // Prefer the active input backend when the Input System package is installed.
#if ENABLE_INPUT_SYSTEM
                Add(events,"UnityEngine.InputSystem.UI.InputSystemUIInputModule");
#else
                Add(events,"UnityEngine.EventSystems.StandaloneInputModule");
#endif
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAssetAndConnect(root,output+"/GameRoot.prefab",InteractionMode.AutomatedAction);
                EditorSceneManager.SaveScene(scene,output+"/Bootstrap.unity");
                foreach(string name in new[]{"Home","Ingame"}) { scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject(name+" Content - add your game here");EditorSceneManager.SaveScene(scene,output+"/"+name+".unity"); }
                // Build configuration is explicit: preserve existing entries and add the three new scenes.
                var entries=EditorBuildSettings.scenes.ToList();foreach(string name in new[]{"Bootstrap","Home","Ingame"})entries.Add(new EditorBuildSettingsScene(output+"/"+name+".unity",true));EditorBuildSettings.scenes=entries.ToArray();
                AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(output+"/Bootstrap.unity");Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(output+"/GameRoot.prefab");
            } catch { if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);throw; }
        }
    }
}
