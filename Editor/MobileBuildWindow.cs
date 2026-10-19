using System;
using System.IO;
using System.Linq;
using BaseUnity.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BaseUnity.Editor
{
    public enum MobileBuildKind { Development, Release }
    [Serializable]
    public sealed class MobileBuildProfile
    {
        public string[] scenes = Array.Empty<string>();
        public bool scriptDebugging;
        public bool autoConnectProfiler;
        public bool androidAppBundle;
    }
    [FilePath("ProjectSettings/BaseUnityMobileBuilds.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MobileBuildSettings : ScriptableSingleton<MobileBuildSettings>
    {
        public MobileBuildProfile development = new MobileBuildProfile();
        public MobileBuildProfile release = new MobileBuildProfile { androidAppBundle = true };
        public void Persist() => Save(true);
    }
    /// <summary>Portable presets over Unity's build pipeline; scene lists are independent per preset.</summary>
    public static class MobileBuildProfiles
    {
        public static BuildPlayerOptions CreateOptions(MobileBuildKind kind, MobileBuildProfile profile, BuildTarget target, string output)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!Enum.IsDefined(typeof(MobileBuildKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (target != BuildTarget.Android && target != BuildTarget.iOS) throw new ArgumentException("Select Android or iOS.", nameof(target));
            var flags = BuildOptions.StrictMode;
            if (kind == MobileBuildKind.Development)
            {
                flags |= BuildOptions.Development;
                if (profile.scriptDebugging) flags |= BuildOptions.AllowDebugging;
                if (profile.autoConnectProfiler) flags |= BuildOptions.ConnectWithProfiler;
            }
            return new BuildPlayerOptions {
                scenes = (profile.scenes ?? Array.Empty<string>()).ToArray(),
                target = target, targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                locationPathName = output, options = flags
            };
        }
        public static string Extension(MobileBuildKind kind, MobileBuildProfile profile) =>
            kind == MobileBuildKind.Development || !profile.androidAppBundle ? "apk" : "aab";

        public static void Validate(BuildPlayerOptions options)
        {
            // This tool always checks configuration even if the global automatic check was disabled.
            var policy = BaseUnityBuildSettings.instance.Snapshot; policy.enabled = true; policy.blockTestAdsInRelease = true;
            FeatureBuildProcessor.CheckBuild(options, policy, false);
        }
        public static BuildReport Build(MobileBuildKind kind, MobileBuildProfile profile, string output)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new BuildFailedException("Wait for Unity to finish compiling and stop Play Mode before building.");
            if (EditorSceneManager.GetSceneManagerSetup().Any(scene => !string.IsNullOrEmpty(scene.path) && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty))
                throw new BuildFailedException("Save modified scenes before building. Validation inspects the saved files.");
            var options = CreateOptions(kind, profile, target, output);
            if (string.IsNullOrWhiteSpace(output)) throw new BuildFailedException("Choose a build output path.");
            string fullOutput = Path.GetFullPath(output);
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullOutput.StartsWith(assets, StringComparison.OrdinalIgnoreCase)) throw new BuildFailedException("Store player builds outside Assets.");
            if (target == BuildTarget.Android && !fullOutput.EndsWith("." + Extension(kind, profile), StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Output extension does not match this profile.");
            Validate(options);
            bool bundle = EditorUserBuildSettings.buildAppBundle;
            bool export = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                if (target == BuildTarget.Android)
                {
                    AndroidJavaEnvironment.EnsureJavaHome();
                    EditorUserBuildSettings.buildAppBundle = kind == MobileBuildKind.Release && profile.androidAppBundle;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                }
                var result = BuildPipeline.BuildPlayer(options);
                WriteSummary(kind, options, result);
                return result;
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = bundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = export;
            }
        }
        [Serializable] private sealed class Summary
        {
            public string utc, profile, target, output, result;
            public string[] scenes;
            public ulong bytes;
            public double seconds;
            public int errors, warnings;
        }
        private static void WriteSummary(MobileBuildKind kind, BuildPlayerOptions options, BuildReport report)
        {
            if (!report) return;
            var summary = report.summary;
            var data = new Summary { utc = DateTime.UtcNow.ToString("O"), profile = kind.ToString(), target = options.target.ToString(),
                output = options.locationPathName, result = summary.result.ToString(), scenes = options.scenes,
                bytes = summary.totalSize, seconds = summary.totalTime.TotalSeconds, errors = summary.totalErrors, warnings = summary.totalWarnings };
            try { Directory.CreateDirectory("Library/BaseUnity"); File.WriteAllText("Library/BaseUnity/LastMobileBuild.json", JsonUtility.ToJson(data, true)); }
            catch (IOException error) { Debug.LogWarning("Could not save build summary: " + error.Message); }
            catch (UnauthorizedAccessException error) { Debug.LogWarning("Could not save build summary: " + error.Message); }
        }
    }
    public sealed class MobileBuildWindow : EditorWindow
    {
        private MobileBuildKind kind;
        private Vector2 scroll;
        private string message;
        private MessageType messageType;
        [MenuItem("Tools/BaseUnity/Mobile Build Profiles")]
        public static void Open() => GetWindow<MobileBuildWindow>("BaseUnity Builds");
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            kind = (MobileBuildKind)GUILayout.Toolbar((int)kind, new[] { "Development", "Release" });
            var settings = MobileBuildSettings.instance;
            var profile = kind == MobileBuildKind.Development ? settings.development : settings.release;
            EditorGUILayout.HelpBox(kind == MobileBuildKind.Development ?
                "Test APK / iOS export. Includes DEBUG panel and isolated Development saves. Ads must still use test configuration on test devices." :
                "Production APK/AAB / iOS export. No DEBUG panel, debugger or profiler connection. Test Ads are blocked.", MessageType.Info);
            EditorGUILayout.LabelField("Active target", EditorUserBuildSettings.activeBuildTarget.ToString());
            EditorGUILayout.LabelField("Version", PlayerSettings.bundleVersion);
            EditorGUILayout.HelpBox("Profiles keep separate scene lists and build flags. FeatureConfig, vendor IDs, signing, application ID and native SDK settings remain project-owned. Review these in Feature Manager / Player Settings before each build. No IDs are replaced automatically.", MessageType.None);
            EditorGUI.BeginChangeCheck();
            if (kind == MobileBuildKind.Development)
            {
                profile.scriptDebugging = EditorGUILayout.Toggle("Script debugging", profile.scriptDebugging);
                profile.autoConnectProfiler = EditorGUILayout.Toggle("Connect profiler", profile.autoConnectProfiler);
            }
            else profile.androidAppBundle = EditorGUILayout.Toggle("Android App Bundle", profile.androidAppBundle);
            EditorGUILayout.Space();
            if (GUILayout.Button("Copy scenes from active Unity Build Profile"))
            {
                profile.scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                settings.Persist(); message = null;
            }
            var scenes = profile.scenes ?? Array.Empty<string>();
            EditorGUILayout.LabelField("Included scenes (Bootstrap first)", EditorStyles.boldLabel);
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[i]);
                var chosen = (SceneAsset)EditorGUILayout.ObjectField(i.ToString(), scene, typeof(SceneAsset), false);
                if (chosen != scene) scenes[i] = chosen ? AssetDatabase.GetAssetPath(chosen) : "";
                if (GUILayout.Button("↑", GUILayout.Width(30)) && i > 0) { var prior = scenes[i - 1]; scenes[i - 1] = scenes[i]; scenes[i] = prior; }
                if (GUILayout.Button("−", GUILayout.Width(30))) { profile.scenes = scenes.Where((_, index) => index != i).ToArray(); GUI.changed = true; EditorGUILayout.EndHorizontal(); break; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add scene")) { profile.scenes = scenes.Concat(new[] { "" }).ToArray(); GUI.changed = true; }
            if (EditorGUI.EndChangeCheck()) { settings.Persist(); message = null; }
            bool ready = !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("Validate configuration")) Run(() => {
                    MobileBuildProfiles.Validate(MobileBuildProfiles.CreateOptions(kind, profile, EditorUserBuildSettings.activeBuildTarget, ""));
                    message = "Validation passed. Review warnings in LastBuildValidation.json; native SDK/device checks still require a real build."; messageType = MessageType.Info;
                });
                if (GUILayout.Button(kind == MobileBuildKind.Development ? "Build test player" : "Build Release player")) Run(() => {
                    string output = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? EditorUtility.SaveFolderPanel("Export iOS project", "", "") :
                        EditorUtility.SaveFilePanel("Build " + kind, "", PlayerSettings.productName + "-" + kind, MobileBuildProfiles.Extension(kind, profile));
                    if (string.IsNullOrEmpty(output)) return;
                    var report = MobileBuildProfiles.Build(kind, profile, output);
                    message = "Build " + report.summary.result + ": " + output;
                    messageType = report.summary.result == BuildResult.Succeeded ? MessageType.Info : MessageType.Error;
                });
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
            ReportButton("Show configuration report", "Library/BaseUnity/LastBuildValidation.json");
            ReportButton("Show build summary", "Library/BaseUnity/LastMobileBuild.json");
            EditorGUILayout.EndScrollView();
        }
        private void Run(Action action)
        { try { action(); } catch (Exception error) { message = error.Message; messageType = MessageType.Error; } }
        private static void ReportButton(string label, string path)
        { using (new EditorGUI.DisabledScope(!File.Exists(path))) if (GUILayout.Button(label)) EditorUtility.RevealInFinder(Path.GetFullPath(path)); }
    }
}
