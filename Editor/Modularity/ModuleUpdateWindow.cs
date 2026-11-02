using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
namespace BaseUnity.Modularity
{
    public sealed class ModuleUpdateWindow : EditorWindow
    {
        [SerializeField] private string repository;
        private ModuleVersionReport[] reports;
        private readonly HashSet<string> replace=new HashSet<string>();
        private bool allowDowngrade;
        private Vector2 scroll;
        private ModuleFileReview selected;
        private string notice;
        private string Project => ModulePackageStore.Root(Path.Combine(Application.dataPath,".."));
        [MenuItem("Tools/BaseUnity/Review Module Updates")]
        public static void OpenMenu() { Open(null); }
        public static void Open(string source)
        { var window=GetWindow<ModuleUpdateWindow>("Module Updates");if(!string.IsNullOrEmpty(source))window.repository=source;window.Reload();window.Show(); }
        private void OnEnable()
        { minSize=new Vector2(760,620);if(string.IsNullOrEmpty(repository))repository=File.Exists(Path.Combine(Project,"Modules~/catalog.json"))?Project:Path.Combine(Project,"Tools/BaseUnity"); }
        private void Reload()
        {
            selected=null;replace.Clear();allowDowngrade=false;reports=null;
            try { reports=ModulePackageStore.ReviewInstalled(repository,Project);notice="Review ready. Source is the currently checked-out repository; comparison copies are under Library."; }
            catch(Exception error) { notice=error.Message; }
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Installed modules → checked-out repository",EditorStyles.boldLabel);
            repository=EditorGUILayout.TextField("Repository",repository);
            using(new EditorGUILayout.HorizontalScope()) {
                if(GUILayout.Button("Refresh review"))Reload();
                if(GUILayout.Button("Show repository changelog")&&File.Exists(Path.Combine(repository,"CHANGELOG.md")))EditorUtility.RevealInFinder(Path.Combine(repository,"CHANGELOG.md"));
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            if(reports!=null)foreach(var report in reports) {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(report.id+"  "+report.installedVersion+" → "+report.sourceVersion,EditorStyles.boldLabel);
                EditorGUILayout.LabelField(report.status+" • "+report.files.Count(f=>f.sourceChanged)+" repository changes / "+report.files.Count(f=>f.localChanged)+" local edits",EditorStyles.wordWrappedLabel);
                if(report.error!=null)EditorGUILayout.HelpBox(report.error,MessageType.Error);
                foreach(var file in report.files)if(GUILayout.Button((file.conflict?"CONFLICT ":file.localChanged?"LOCAL ":"SOURCE ")+file.path,EditorStyles.miniButton))selected=file;
                if(report.HasLocalChanges) {
                    bool value=replace.Contains(report.id);bool next=EditorGUILayout.ToggleLeft("Replace this module with repository files; preserve my current files in backup",value);
                    if(next!=value) { if(next)replace.Add(report.id);else replace.Remove(report.id); }
                }
                EditorGUILayout.EndVertical();
            }
            if(reports!=null&&reports.Length==0)EditorGUILayout.HelpBox("No selective modules are installed. Use Module Manager in a modular project.",MessageType.Info);
            if(selected!=null) {
                EditorGUILayout.LabelField(selected.path,EditorStyles.boldLabel);
                using(new EditorGUILayout.HorizontalScope()) {
                    using(new EditorGUILayout.VerticalScope()) { EditorGUILayout.LabelField("Current project");EditorGUILayout.SelectableLabel(Read(selected.installedPath),EditorStyles.textArea,GUILayout.Height(250)); }
                    using(new EditorGUILayout.VerticalScope()) { EditorGUILayout.LabelField("Repository result");EditorGUILayout.SelectableLabel(Read(selected.sourcePath),EditorStyles.textArea,GUILayout.Height(250)); }
                }
            }
            EditorGUILayout.EndScrollView();
            bool downgrade=reports!=null&&reports.Any(r=>ModulePackageStore.CompareVersions(r.sourceVersion,r.installedVersion).GetValueOrDefault()<0);
            if(downgrade)allowDowngrade=EditorGUILayout.ToggleLeft("Allow explicit downgrade to this repository version",allowDowngrade);
            bool blocked=reports==null||reports.Length==0||!reports.Any(r=>r.files.Length>0||r.installedVersion!=r.sourceVersion)||reports.Any(r=>r.error!=null||!ModulePackageStore.CompareVersions(r.sourceVersion,r.installedVersion).HasValue||(r.HasLocalChanges&&!replace.Contains(r.id)))||(downgrade&&!allowDowngrade);
            using(new EditorGUI.DisabledScope(blocked||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode))if(GUILayout.Button("Update installed modules (backup first)"))UpdateInstalled();
            if(!string.IsNullOrEmpty(notice))EditorGUILayout.HelpBox(notice,MessageType.Info);
        }
        private static string Read(string path)
        {
            if(!File.Exists(path))return "(file absent)";
            try { using(var reader=new StreamReader(path)) { var chars=new char[32000];int count=reader.ReadBlock(chars,0,chars.Length);return new string(chars,0,count)+(count==chars.Length?"\n… truncated":""); } }catch(Exception error) { return error.Message; }
        }
        private void UpdateInstalled()
        {
            try {
                var fresh=ModulePackageStore.ReviewInstalled(repository,Project);
                if(fresh.Length!=reports.Length||fresh.Any(r=>r.error!=null||!reports.Any(old=>old.id==r.id&&old.signature==r.signature))) { reports=fresh;replace.Clear();allowDowngrade=false;selected=null;notice="Files changed since review. Review the refreshed changes before applying.";return; }
                string backup=ModulePackageStore.Apply(repository,Project,ModulePackageStore.Installed(repository,Project),null,replace.ToArray());
                EditorPrefs.SetString("BaseUnity.Modules.LastBackup."+Project,backup);notice="Updated. Full previous state is backed up at "+backup;
                reports=null;selected=null;replace.Clear();AssetDatabase.Refresh();Client.Resolve();
            } catch(Exception error) { notice=error.Message;Debug.LogWarning("BaseUnity update: "+error.Message); }
        }
    }
}
