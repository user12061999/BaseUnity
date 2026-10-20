using System;
using System.IO;
using System.Linq;
using BaseUnity.Features;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BaseUnity.Editor
{
    [Serializable] public sealed class BaseUnitySetupOptions
    {
        public string destination = GameFlowInstaller.Destination;
        public bool offline = true, shop = true, daily = true, missions = true, achievements = true, rewards = true, localization = true;
        public bool assets, tutorial, haptics, notifications, purchasing;
        public bool popups = true, feedback = true;
        public bool generateUIPrefabs = true, generateUITheme = true;
        public bool ads, firebase, generateLevels = true, generateOfflineUI = true, generateHomeHub = true, installUniTask;
    }
    [Serializable] internal sealed class PendingBaseUnitySetup { public BaseUnitySetupOptions options; public string status, error, backup; }
    [InitializeOnLoad]
    public static class BaseUnitySetupWorkflow
    {
        private static double nextPoll;
        private static bool completing;
        static BaseUnitySetupWorkflow() { EditorApplication.update += PollPending; }
        private static void PollPending()
        {
            if (completing || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (!File.Exists(PendingPath)) return;
            try { if (ReadPending().status == "WaitingForCompile") CompletePending(); }
            catch (Exception e) { Debug.LogWarning("BaseUnity pending setup needs review: " + e.Message); nextPoll += 30; }
        }
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        private const string PendingPath = "ProjectSettings/BaseUnity/PendingSetup.json";
        public static SampleUpdateStore Store => new SampleUpdateStore(ProjectRoot);
        public static string InstalledRoot()
        {
            foreach (string guid in AssetDatabase.FindAssets("GameFlowBootstrap t:MonoScript", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script && script.GetClass()?.FullName == "BaseUnity.GameFlow.GameFlowBootstrap")
                    return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace('\\', '/');
            }
            return null;
        }
        public static void EnsureEditable(string destination, bool generatingScenes = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Stop Play Mode and wait for compilation first.");
            Store.ValidateDestination(destination);
            string installed = InstalledRoot();
            if (installed != null && !string.Equals(installed, destination, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A Game Flow installation already exists at " + installed + ". Use that folder to avoid duplicate assemblies.");
            bool missingScene = new[] { "Bootstrap", "Home", "InGame" }.Any(name => !File.Exists(optionsPath(destination, name)));
            if (generatingScenes)
            {
                var flow = AssetDatabase.LoadMainAssetAtPath(destination + "/GameFlowConfig.asset");
                var features = flow ? new SerializedObject(flow).FindProperty("features")?.objectReferenceValue : null;
                if (features && !AssetDatabase.GetAssetPath(features).StartsWith(destination + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("FeatureConfig is shared outside this sample. Configure it in Feature Manager before using setup.");
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (generatingScenes && missingScene && string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save or close untitled scenes before generating Game Flow scenes.");
                if (scene.isDirty && scene.path.StartsWith(destination + "/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Save the open sample scene first: " + scene.path);
            }
            if (AssetDatabase.IsValidFolder(destination)) foreach (var guid in AssetDatabase.FindAssets("", new[] { destination }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset && EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Save asset changes before setup/update/restore: " + path);
            }
        }
        private static string optionsPath(string destination, string name) => destination + "/Scenes/" + name + ".unity";
        public static FeatureConfig ConfigAt(string destination)
        {
            var flow = AssetDatabase.LoadMainAssetAtPath(destination + "/GameFlowConfig.asset");
            return (flow ? new SerializedObject(flow).FindProperty("features")?.objectReferenceValue as FeatureConfig : null) ?? AssetDatabase.LoadAssetAtPath<FeatureConfig>(destination + "/FeatureConfig.asset");
        }
        public static BaseUnitySetupOptions ReadOptions(string destination)
        {
            var result = new BaseUnitySetupOptions { destination = destination };
            var config = AssetDatabase.LoadAssetAtPath<FeatureConfig>(destination + "/FeatureConfig.asset");
            var flow = AssetDatabase.LoadMainAssetAtPath(destination + "/GameFlowConfig.asset");
            if (flow)
            {
                var reference = new SerializedObject(flow).FindProperty("features")?.objectReferenceValue as FeatureConfig;
                if (reference) config = reference;
            }
            if (config)
            {
                result.offline = config.offline.enabled; result.shop = config.offline.shop; result.daily = config.offline.dailyReward;
                result.missions = config.offline.missions; result.rewards = config.offline.levelRewards;
                result.achievements = config.offline.achievements;
                result.localization = config.localization.enabled; result.ads = config.ads.enabled; result.firebase = config.firebase.enabled;
                result.assets = config.assets.enabled; result.tutorial = config.tutorial.enabled; result.haptics = config.haptics.enabled;
                result.notifications = config.notifications.enabled; result.purchasing = config.purchasing.enabled; result.popups = config.popups.enabled; result.feedback = config.feedback.enabled;
            }
            return result;
        }
        public static void Start(BaseUnitySetupOptions options)
        {
            EnsureEditable(options.destination, true);
            if (Type.GetType("UnityEngine.UI.Button, UnityEngine.UI") == null) throw new InvalidOperationException("Install Unity UI (com.unity.ugui) before setup.");
            if (File.Exists(PendingPath))
            {
                var old = ReadPending(); if (old.status == "Running") throw new InvalidOperationException("An interrupted setup needs review. Its original backup is " + old.backup);
            }
            // Install/update source is previewed and applied separately; this step only configures generated game assets.
            WritePending(new PendingBaseUnitySetup { options = options, status = "WaitingForCompile" });
            if (options.installUniTask && FeatureConfigurationUtility.FindType("Cysharp.Threading.Tasks.UniTask") == null) UniTaskSetupMenu.Install();
            AssetDatabase.Refresh(); EditorApplication.delayCall += CompletePending;
        }
        public static string PendingStatus => File.Exists(PendingPath) ? ReadPending().status + ": " + ReadPending().error : "No pending setup.";
        private static PendingBaseUnitySetup ReadPending() => JsonUtility.FromJson<PendingBaseUnitySetup>(File.ReadAllText(PendingPath));
        private static void WritePending(PendingBaseUnitySetup pending)
        { Directory.CreateDirectory(Path.GetDirectoryName(PendingPath)); File.WriteAllText(PendingPath, JsonUtility.ToJson(pending, true)); }
        [DidReloadScripts] private static void AfterReload() { if (File.Exists(PendingPath) && ReadPending().status == "WaitingForCompile") EditorApplication.delayCall += CompletePending; }
        public static void ClearPending()
        { if (File.Exists(PendingPath)) { var pending = ReadPending(); pending.status = "Cancelled"; WritePending(pending); } }
        public static SampleSceneState[] Scenes() => EditorBuildSettings.scenes.Select(x => new SampleSceneState { path = x.path, enabled = x.enabled }).ToArray();
        public static void CompletePending()
        {
            if (!File.Exists(PendingPath)) return;
            if (completing) return;
            var pending = ReadPending();
            if (pending.status != "WaitingForCompile" && pending.status != "Failed") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += CompletePending; return; }
            completing = true;
            try
            {
                EnsureEditable(pending.options.destination, true);
                var type = FeatureConfigurationUtility.FindType("BaseUnity.GameFlow.Editor.GameFlowSceneBuilder");
                var generate = type?.GetMethod("GenerateAt");
                if (generate == null) throw new InvalidOperationException("The installed sample has not compiled, or needs its GameFlowSceneBuilder update. Resolve Console errors/conflicts, then Retry setup.");
                if (pending.options.installUniTask && FeatureConfigurationUtility.FindType("Cysharp.Threading.Tasks.UniTask") == null)
                {
                    if (UniTaskSetupMenu.IsInstalling) { EditorApplication.delayCall += CompletePending; return; }
                    throw new InvalidOperationException("UniTask installation has not completed. Check Package Manager/Console, then Retry setup.");
                }
                pending.backup = Store.BeginSetup(pending.options.destination, Scenes()); pending.status = "Running"; WritePending(pending);
                bool completed = false;
                try
                {
                    generate.Invoke(null, new object[] { pending.options.destination });
                    var flow = AssetDatabase.LoadMainAssetAtPath(pending.options.destination + "/GameFlowConfig.asset");
                    var data = new SerializedObject(flow);
                    var features = data.FindProperty("features").objectReferenceValue as FeatureConfig;
                    // An external shared config is not inside this setup's backup boundary.
                    if (features && !AssetDatabase.GetAssetPath(features).StartsWith(pending.options.destination + "/", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("FeatureConfig is shared outside the sample folder. Edit it through Feature Manager; setup did not modify it.");
                    if (!features)
                    {
                        string path = pending.options.destination + "/FeatureConfig.asset";
                        features = AssetDatabase.LoadAssetAtPath<FeatureConfig>(path);
                        if (!features) { features = ScriptableObject.CreateInstance<FeatureConfig>(); AssetDatabase.CreateAsset(features, path); }
                        data.FindProperty("features").objectReferenceValue = features; data.ApplyModifiedProperties();
                    }
                    ApplyModules(features, pending.options);
                    if (pending.options.generateLevels) InvokeBuilder("LevelAssetBuilder", flow);
                    if (pending.options.generateOfflineUI) InvokeBuilder("OfflineUIBuilder", flow);
                    if (pending.options.generateHomeHub) InvokeBuilder("HomeHubBuilder", flow);
                    if (pending.options.generateUIPrefabs) InvokeBuilder("GameUIPrefabBuilder", flow);
                    if (pending.options.generateUITheme) InvokeBuilder("UIThemeBuilder", flow);
                    if (pending.options.generateHomeHub || pending.options.generateOfflineUI) InvokeBuilder("UIFeedbackBuilder", flow);
                    AssetDatabase.SaveAssetIfDirty(features); AssetDatabase.SaveAssetIfDirty(flow); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); completed = true;
                }
                finally { Store.CompleteSetup(pending.backup, Scenes(), completed); }
                var completedFlow = AssetDatabase.LoadMainAssetAtPath(pending.options.destination + "/GameFlowConfig.asset");
                var completedConfig = ConfigAt(pending.options.destination);
                var issues = FeatureBuildValidator.Validate(completedConfig, EditorUserBuildSettings.activeBuildTarget);
                issues.AddRange(FeatureBuildValidator.ValidateScenes(completedFlow, EditorBuildSettings.scenes));
                var report = new SetupReport { target = EditorUserBuildSettings.activeBuildTarget.ToString(), destination = pending.options.destination,
                    errors = issues.Count(x => x.severity == MessageType.Error), warnings = issues.Count(x => x.severity == MessageType.Warning),
                    messages = issues.Select(x => x.severity + ": " + x.message).ToArray() };
                Directory.CreateDirectory("Library/BaseUnity"); File.WriteAllText("Library/BaseUnity/LastSetupValidation.json", JsonUtility.ToJson(report, true));
                pending.status = report.errors == 0 ? "Completed" : "CompletedWithIssues";
                pending.error = report.errors + " error(s), " + report.warnings + " warning(s). See Library/BaseUnity/LastSetupValidation.json"; WritePending(pending);
                Debug.Log("BaseUnity setup complete in " + pending.options.destination + ". Backup: " + pending.backup);
            }
            catch (Exception e) { pending.status = "Failed"; pending.error = e.GetBaseException().Message; WritePending(pending); Debug.LogWarning("BaseUnity setup: " + pending.error); }
            finally { completing = false; }
        }
        public static void ApplyModules(FeatureConfig config, BaseUnitySetupOptions options)
        {
            Undo.RecordObject(config, "BaseUnity module setup");
            config.offline.enabled = options.offline; config.offline.shop = options.shop; config.offline.dailyReward = options.daily;
            config.offline.missions = options.missions; config.offline.achievements = options.achievements; config.offline.levelRewards = options.rewards;
            config.localization.enabled = options.localization; config.ads.enabled = options.ads; config.firebase.enabled = options.firebase;
            config.assets.enabled = options.assets; config.tutorial.enabled = options.tutorial; config.haptics.enabled = options.haptics;
            config.notifications.enabled = options.notifications; config.purchasing.enabled = options.purchasing; config.popups.enabled = options.popups; config.feedback.enabled = options.feedback;
            EditorUtility.SetDirty(config);
        }
        private static void InvokeBuilder(string name, UnityEngine.Object config)
        {
            var type = FeatureConfigurationUtility.FindType("BaseUnity.GameFlow.Editor." + name);
            var method = type?.GetMethod("Generate");
            if (method == null) throw new InvalidOperationException("Update/merge the missing sample builder: " + name);
            method.Invoke(null, new object[] { config });
        }
        public static void Restore(string id)
        {
            var backup = Store.ReadBackup(id); EnsureEditable(backup.destination);
            if (backup.hasSceneSnapshot && backup.beforeScenes != null && backup.afterScenes != null && JsonUtility.ToJson(new SceneList { scenes = Scenes() }) != JsonUtility.ToJson(new SceneList { scenes = backup.afterScenes }))
                throw new IOException("Build scene list has changed since setup. Restore was stopped to preserve newer changes.");
            Store.Restore(id);
            if (backup.hasSceneSnapshot && backup.beforeScenes != null) EditorBuildSettings.scenes = backup.beforeScenes.Select(x => new EditorBuildSettingsScene(x.path, x.enabled)).ToArray();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        [Serializable] private sealed class SetupReport { public string target, destination; public int errors, warnings; public string[] messages; }
        [Serializable] private sealed class SceneList { public SampleSceneState[] scenes; }
    }
}
