using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    public sealed class BaseUnitySetupWindow : EditorWindow
    {
        [SerializeField] private BaseUnitySetupOptions options = new BaseUnitySetupOptions();
        private SampleUpdatePlan plan;
        private SampleBackup[] backups;
        private string notice;
        private bool failed;
        private int tab;
        private Vector2 scroll;
        [MenuItem("Tools/BaseUnity/Setup and Update Wizard")]
        public static void Open()
        {
            var window = GetWindow<BaseUnitySetupWindow>("BaseUnity Setup"); window.minSize = new Vector2(780, 580);
            string installed = BaseUnitySetupWorkflow.InstalledRoot();
            window.options = BaseUnitySetupWorkflow.ReadOptions(installed ?? GameFlowInstaller.Destination);
            window.plan = null; window.backups = null; window.Show();
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("BASE UNITY • SETUP & UPDATE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Install modules, review sample updates, and restore backups.");
            string destination = EditorGUILayout.TextField("Game Flow folder", options.destination).Replace('\\', '/').TrimEnd('/');
            if (destination != options.destination) { options = BaseUnitySetupWorkflow.ReadOptions(destination); plan = null; backups = null; }
            tab = GUILayout.Toolbar(tab, new[] { "Setup", "Update preview", "Backups" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                try { if (tab == 0) SetupGUI(); else if (tab == 1) UpdateGUI(); else BackupsGUI(); }
                catch (Exception e) { notice = e.GetBaseException().Message; failed = true; }
            }
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, failed ? MessageType.Error : MessageType.Info);
        }
        private void SetupGUI()
        {
            EditorGUILayout.HelpBox("One Game Flow code installation per project. New scenes/configs/prefabs are generated without overwriting existing ones. Source files must be reviewed in Update preview first.", MessageType.Info);
            options.offline = EditorGUILayout.Toggle("Offline wallet", options.offline);
            if (options.offline)
            {
                EditorGUI.indentLevel++;
                options.shop = EditorGUILayout.Toggle("Shop", options.shop); options.daily = EditorGUILayout.Toggle("Daily rewards", options.daily);
                options.missions = EditorGUILayout.Toggle("Daily missions", options.missions); options.rewards = EditorGUILayout.Toggle("Level rewards", options.rewards);
                options.achievements = EditorGUILayout.Toggle("Achievements", options.achievements);
                EditorGUI.indentLevel--;
            }
            options.localization = EditorGUILayout.Toggle("English / Vietnamese", options.localization);
            options.ads = EditorGUILayout.Toggle("Enable Ads", options.ads); options.firebase = EditorGUILayout.Toggle("Enable Firebase", options.firebase);
            if (options.ads || options.firebase) EditorGUILayout.HelpBox("Enable selects startup switches. Configure SDK providers, credentials and ad IDs in Feature Manager before building.", MessageType.Info);
            options.popups = EditorGUILayout.Toggle("Popup queue", options.popups); options.feedback = EditorGUILayout.Toggle("Toast / loading / confirmation", options.feedback);
            options.assets = EditorGUILayout.Toggle("Addressables assets", options.assets); options.tutorial = EditorGUILayout.Toggle("Tutorial", options.tutorial);
            options.haptics = EditorGUILayout.Toggle("Haptics", options.haptics); options.notifications = EditorGUILayout.Toggle("Local notifications", options.notifications);
            options.purchasing = EditorGUILayout.Toggle("In-app purchases", options.purchasing);
            if (options.assets || options.notifications || options.purchasing || options.haptics) EditorGUILayout.HelpBox("Configure module packages/providers and store verification in Feature Manager > Modules. Enabling a switch does not install an SDK or activate real purchases.", MessageType.Info);
            options.generateLevels = EditorGUILayout.Toggle("Generate missing LevelData", options.generateLevels);
            options.generateOfflineUI = EditorGUILayout.Toggle("Generate missing offline UI", options.generateOfflineUI);
            options.generateHomeHub = EditorGUILayout.Toggle("Generate Home / Store / Quest UI", options.generateHomeHub);
            options.installUniTask = EditorGUILayout.Toggle("Install UniTask if missing", options.installUniTask);
            if (GUILayout.Button("1. Scan source files / review update")) Run(() => { Scan(); tab = 1; return "Review the file list before applying it."; });
            using (new EditorGUI.DisabledScope(plan == null || plan.destination != options.destination))
                if (GUILayout.Button("2. Apply reviewed files, then configure and generate")) Run(ApplyAndSetup);
            EditorGUILayout.LabelField("Setup state", BaseUnitySetupWorkflow.PendingStatus, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Refresh setup status")) Run(() => BaseUnitySetupWorkflow.PendingStatus);
            if (GUILayout.Button("Retry pending setup after fixing compilation/conflicts")) Run(() => { BaseUnitySetupWorkflow.CompletePending(); return BaseUnitySetupWorkflow.PendingStatus; });
            if (GUILayout.Button("Clear pending setup status (keep all files/backups)")) Run(() => { BaseUnitySetupWorkflow.ClearPending(); return "Pending status cleared. Scan/review files before starting another setup."; });
            if (GUILayout.Button("Open Feature Manager")) FeatureManagerWindow.Open(BaseUnitySetupWorkflow.ConfigAt(options.destination));
            if (GUILayout.Button("Validate generated setup")) FeatureValidationWindow.Open(BaseUnitySetupWorkflow.ConfigAt(options.destination));
        }
        private string ApplyAndSetup()
        {
            if (plan == null) throw new InvalidOperationException("Scan source files first.");
            BaseUnitySetupWorkflow.EnsureEditable(options.destination, true);
            string backup = BaseUnitySetupWorkflow.Store.Apply(plan); plan = null; backups = null;
            BaseUnitySetupWorkflow.Start(options);
            return "Files applied with backup " + backup + ". Setup continues after compilation; see Setup state.";
        }
        private void Scan()
        {
            BaseUnitySetupWorkflow.EnsureEditable(options.destination);
            plan = BaseUnitySetupWorkflow.Store.Preview(GameFlowInstaller.FindSampleSource(), options.destination, GameFlowInstaller.SourceVersion);
            backups = null;
        }
        private void UpdateGUI()
        {
            if (GUILayout.Button("Scan / refresh preview")) Run(() => { Scan(); return "Preview refreshed. Untracked/local changes are kept by default."; });
            if (plan == null) { EditorGUILayout.HelpBox("Scan the destination to compare source, current files and tracked baselines. Existing installations can be tracked without replacing modified files.", MessageType.Info); return; }
            var manifest = BaseUnitySetupWorkflow.Store.ReadManifest(options.destination);
            EditorGUILayout.LabelField("Installed source", string.IsNullOrEmpty(manifest.installedVersion) ? "Not fully tracked" : manifest.installedVersion);
            EditorGUILayout.LabelField("Incoming source", plan.version);
            EditorGUILayout.LabelField(plan.files.Count(x => x.selected) + " file operation(s) selected; " + plan.files.Count(x => x.state == SampleFileState.Conflict || x.state == SampleFileState.LocalDeleted || x.state == SampleFileState.SourceRemoved) + " item(s) need review.");
            EditorGUILayout.HelpBox("Checked = use incoming source, or delete when removed upstream. Untick to keep local content. Generated Scenes/configs/prefabs are not tracked sample code. No automatic text merge is performed.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select safe additions/updates")) foreach (var file in plan.files) file.selected = file.state == SampleFileState.Add || file.state == SampleFileState.Update;
                if (GUILayout.Button("Keep all local files")) foreach (var file in plan.files) file.selected = false;
            }
            foreach (var file in plan.files)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool canSelect = file.state != SampleFileState.Identical && file.state != SampleFileState.AlreadyRemoved;
                    using (new EditorGUI.DisabledScope(!canSelect)) file.selected = EditorGUILayout.Toggle(file.selected, GUILayout.Width(20));
                    EditorGUILayout.LabelField(file.IsRemoval ? "DELETE (optional)" : file.state.ToString(), GUILayout.Width(130));
                    EditorGUILayout.LabelField(file.path);
                    if (GUILayout.Button("Diff", GUILayout.Width(50))) SampleDiffWindow.Open(file);
                }
            }
            if (GUILayout.Button("Apply selected files and continue Setup")) Run(ApplyAndSetup);
            if (GUILayout.Button("Apply selected operations / track identical files")) Run(() => {
                BaseUnitySetupWorkflow.EnsureEditable(options.destination);
                string id = BaseUnitySetupWorkflow.Store.Apply(plan); plan = null; backups = null; AssetDatabase.Refresh();
                return "Update saved. Backup " + id + ". Resolve any remaining conflicts and check Console before building.";
            });
        }
        private void BackupsGUI()
        {
            if (GUILayout.Button("Refresh backup list")) Run(() => { backups = BaseUnitySetupWorkflow.Store.Backups(options.destination); return backups.Length + " backup(s)."; });
            if (backups == null) return;
            EditorGUILayout.HelpBox("Restore checks every file before writing. If newer edits exist, restore stops instead of overwriting them. Copy newer edits elsewhere or resolve them manually before retrying. Restore newly added files removes their generated metadata too.", MessageType.Info);
            foreach (var backup in backups)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(backup.createdUtc + " • " + backup.description, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(backup.status + " / " + backup.files.Count + " file snapshot(s)");
                    foreach (var file in backup.files) EditorGUILayout.LabelField((file.existed ? "Restore " : "Remove added ") + file.path, EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(backup.status == "Restored"))
                        if (GUILayout.Button("Validate and restore this backup")) Run(() => {
                            BaseUnitySetupWorkflow.Restore(backup.id); plan = null; backups = null;
                            return "Backup restored. Generated assets outside this transaction were preserved.";
                        });
                }
            }
        }
        private void Run(Func<string> action)
        { try { notice = action(); failed = false; } catch (Exception e) { notice = e.GetBaseException().Message; failed = true; } }
    }
}
