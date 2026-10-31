using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
namespace BaseUnity.Modularity
{
    public sealed class ModuleManagerWindow : EditorWindow
    {
        [SerializeField] private string repository;
        private readonly HashSet<string> selected=new HashSet<string>();
        private ModuleCatalog catalog;
        private ModulePlan plan;
        private string notice;
        private Vector2 scroll;
        private string Project => ModulePackageStore.Root(Path.Combine(Application.dataPath,".."));
        private string BackupKey => "BaseUnity.Modules.LastBackup."+Project;
        [MenuItem("Tools/BaseUnity/Module Manager")]
        public static void Open() { GetWindow<ModuleManagerWindow>("BaseUnity Modules").Show(); }
        private void OnEnable()
        {
            minSize=new Vector2(600,640);
            if(string.IsNullOrEmpty(repository)) repository=File.Exists(Path.Combine(Project,"Modules~/catalog.json"))?Project:Path.Combine(Project,"Tools/BaseUnity");
            Reload();
        }
        private void Reload()
        {
            catalog=null; plan=null;
            try { catalog=ModulePackageStore.ReadCatalog(repository); selected.Clear(); foreach(var id in ModulePackageStore.Installed(repository,Project)) selected.Add(id); if(selected.Count==0)selected.Add("core"); notice=null; }
            catch(Exception error) { notice=error.Message; }
        }
        private void Run(Action action) { try { action(); } catch(Exception error) { notice=error.Message; Debug.LogWarning("BaseUnity Module Manager: "+error.Message); } Repaint(); }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("BASE UNITY • SELECTIVE MODULES",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Keep the repository outside Assets/Packages. Only selected source files and required modules are copied into embedded UPM packages. This manager has no Runtime dependency.",MessageType.Info);
            using(new EditorGUILayout.HorizontalScope()) {
                string value=EditorGUILayout.TextField("Repository",repository); if(value!=repository) { repository=value; catalog=null; plan=null; }
                if(GUILayout.Button("Browse",GUILayout.Width(70))) { string folder=EditorUtility.OpenFolderPanel("BaseUnity source repository",repository,""); if(folder.Length>0) { repository=folder; Reload(); } }
                if(GUILayout.Button("Load",GUILayout.Width(55))) Reload();
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            if(catalog!=null) {
                var closure=ModulePackageStore.Resolve(catalog,selected).Select(x=>x.id).ToArray();
                foreach(var module in catalog.modules) {
                    bool required=selected.Where(x=>x!=module.id).Any(x=>ModulePackageStore.Resolve(catalog,new[]{x}).Any(m=>m.id==module.id));
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    using(new EditorGUI.DisabledScope(required)) {
                        bool was=selected.Contains(module.id)||required; bool chosen=EditorGUILayout.ToggleLeft(module.name+(required?" (dependency)":""),was,EditorStyles.boldLabel);
                        if(chosen!=was) { if(chosen)selected.Add(module.id); else selected.Remove(module.id); plan=null; }
                    }
                    EditorGUILayout.LabelField(module.description,EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField(module.files.Length+" scripts • "+module.package,EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.LabelField("Resolved modules: "+string.Join(", ",closure),EditorStyles.wordWrappedLabel);
                EditorGUILayout.HelpBox("GameFlow, Economy, UI/theme, Ads/Firebase and their sample prefabs remain in the legacy distribution in this first modular release. They are not imported by these five modules.",MessageType.None);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)) {
                    using(new EditorGUILayout.HorizontalScope()) {
                        if(GUILayout.Button("Preview changes"))Run(()=> { plan=ModulePackageStore.Preview(repository,Project,selected); notice=plan.Summary; });
                        if(GUILayout.Button("Select none")) { selected.Clear(); plan=null; }
                        if(GUILayout.Button("Export only"))Run(()=> { string folder=EditorUtility.OpenFolderPanel("Export packages into an EMPTY folder",Project,""); if(folder.Length>0) { ModulePackageStore.Export(repository,folder,selected); notice="Exported selected packages to "+folder; } });
                    }
                    if(plan!=null) {
                        EditorGUILayout.LabelField(plan.Summary,EditorStyles.wordWrappedLabel);
                        if(GUILayout.Button("Apply install / update / removal"))Run(()=> { string backup=ModulePackageStore.Apply(repository,Project,selected); EditorPrefs.SetString(BackupKey,backup); notice="Applied. Backup: "+backup; plan=null; AssetDatabase.Refresh(); Client.Resolve(); });
                    }
                    string last=EditorPrefs.GetString(BackupKey,"");
                    if(Directory.Exists(last)) {
                        if(GUILayout.Button("Restore last module backup"))Run(()=> { string backup=ModulePackageStore.Apply(repository,Project,new string[0],last); EditorPrefs.SetString(BackupKey,backup); notice="Restored. Undo backup: "+backup; AssetDatabase.Refresh(); Client.Resolve(); Reload(); });
                        if(GUILayout.Button("Show last backup"))EditorUtility.RevealInFinder(last);
                    }
                    if(GUILayout.Button("Create bootstrap with installed modules"))Run(CreateBootstrap);
                }
            }
            EditorGUILayout.EndScrollView();
            if(!string.IsNullOrEmpty(notice))EditorGUILayout.HelpBox(notice,MessageType.Info);
        }
        private static Type Find(string fullName) => AppDomain.CurrentDomain.GetAssemblies().Select(x=>x.GetType(fullName)).FirstOrDefault(x=>x!=null);
        private void CreateBootstrap()
        {
            var core=Find("BaseUnity.Core.ModuleBootstrap"); if(core==null)throw new InvalidOperationException("Install Core and wait for compilation first");
            if(AppDomain.CurrentDomain.GetAssemblies().Any(x=>x.GetName().Name=="BaseUnity.Runtime"))throw new InvalidOperationException("Use the clean modular project, not the legacy sample");
            var root=new GameObject("BaseUnity Module Bootstrap"); Undo.RegisterCreatedObjectUndo(root,"Create modular bootstrap");
            foreach(var name in new[]{"BaseUnity.Save.SaveModule","BaseUnity.Audio.AudioModule","BaseUnity.MobileInput.MobileInputModule"}) { var type=Find(name); if(type!=null)root.AddComponent(type); }
            root.AddComponent(core); Selection.activeGameObject=root; notice="Bootstrap created. Save the scene; add your gameplay without installing unrelated services.";
        }
    }
}
