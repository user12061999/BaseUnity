using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseUnity.Core;
using BaseUnity.Features;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BaseUnity.Editor
{
    /// <summary>Uses the actual build options, including custom scene lists and Build Profiles.</summary>
    public sealed class FeatureBuildProcessor : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext context) => CheckBuild(context.BuildPlayerOptions);

        public static List<FeatureValidationIssue> ValidateBuild(BuildPlayerOptions options, BuildValidationPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var issues = new List<FeatureValidationIssue>();
            if (!policy.enabled) return issues;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { issues.Add(new FeatureValidationIssue(MessageType.Error, "Stop Play Mode before validating a player build.", null)); return issues; }
            var scenes = options.scenes ?? Array.Empty<string>();
            var sceneList = scenes.Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            if (scenes.Any(string.IsNullOrWhiteSpace)) issues.Add(new FeatureValidationIssue(MessageType.Error, "This build contains an empty scene path.", null));
            issues.AddRange(FeatureBuildValidator.ValidateScenes(null, sceneList).Where(x => x.severity == MessageType.Error));
            var configs = new HashSet<FeatureConfig>(); var flows = new HashSet<UnityEngine.Object>();
            int bootstrapCount = 0; bool firstHasBootstrap = false;
            for (int i = 0; i < scenes.Length; i++)
            {
                string path = scenes[i]; if (string.IsNullOrWhiteSpace(path)) continue; var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (!asset) continue;
                Scene preview = default;
                try
                {
                    preview = EditorSceneManager.OpenPreviewScene(path);
                    foreach (var root in preview.GetRootGameObjects())
                    {
                        foreach (var node in root.GetComponentsInChildren<Transform>(true))
                            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0)
                                issues.Add(new FeatureValidationIssue(MessageType.Error, "Missing script in scene " + path + ": " + node.name, asset));
                        foreach (var bootstrap in root.GetComponentsInChildren<GameBootstrap>(true))
                        {
                            bootstrapCount++; if (i == 0) firstHasBootstrap = true;
                            if (bootstrap.transform.parent) issues.Add(new FeatureValidationIssue(MessageType.Error, "Bootstrap must be a scene root: " + path, asset));
                            var config = bootstrap.FeatureConfig;
                            var data = new SerializedObject(bootstrap);
                            var flow = data.FindProperty("config")?.objectReferenceValue;
                            if (flow && flow.GetType().FullName == "BaseUnity.GameFlow.GameFlowConfig")
                            {
                                var flowData = new SerializedObject(flow);
                                var overrideConfig = flowData.FindProperty("features").objectReferenceValue as FeatureConfig;
                                if (overrideConfig) config = overrideConfig;
                                if (flows.Add(flow)) issues.AddRange(FeatureBuildValidator.ValidateScenes(flow, sceneList));
                                if (config && config.offline != null && config.offline.enabled && config.offline.saveSlot == flowData.FindProperty("saveSlot").stringValue && !overrideConfig)
                                    issues.Add(new FeatureValidationIssue(MessageType.Error, "Offline and GameFlow progress must use different save slots.", flow));
                            }
                            else if (bootstrap.GetType().FullName == "BaseUnity.GameFlow.GameFlowBootstrap")
                                issues.Add(new FeatureValidationIssue(MessageType.Error, "Assign a GameFlowConfig to Bootstrap in " + path, asset));
                            if (config && configs.Add(config))
                            {
                                issues.AddRange(FeatureBuildValidator.Validate(config, options.target));
                                issues.AddRange(ValidateRelease(config, options.options, policy));
                            }
                        }
                    }
                }
                catch (Exception e) { issues.Add(new FeatureValidationIssue(MessageType.Error, "Could not inspect " + path + ": " + e.Message, asset)); }
                finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
            }
            if (bootstrapCount > 0 && !firstHasBootstrap)
                issues.Add(new FeatureValidationIssue(MessageType.Error, "Put the Bootstrap scene first in this build's scene list.", null));
            if (bootstrapCount > 1)
                issues.Add(new FeatureValidationIssue(MessageType.Warning, "Multiple BaseUnity bootstraps are included. Verify that only the intended bootstrap initializes the game.", null));
            return issues;
        }

        public static IEnumerable<FeatureValidationIssue> ValidateRelease(FeatureConfig config, BuildOptions options, BuildValidationPolicy policy)
        {
            var ads = config ? config.ads : null;
            if (policy.enabled && policy.blockTestAdsInRelease && (options & BuildOptions.Development) == 0 && ads != null && ads.enabled &&
                ads.testMode && (ads.banner || ads.interstitial || ads.rewarded))
                yield return new FeatureValidationIssue(MessageType.Error, "Release build has Ads test mode enabled. Disable test ads in Feature Manager > Ads, or use a Development Build for testing.", config);
        }

        public static void CheckBuild(BuildPlayerOptions options, BuildValidationPolicy policy = null, bool log = true)
        {
            policy = policy ?? BaseUnityBuildSettings.instance.Snapshot;
            var issues = ValidateBuild(options, policy);
            var errors = issues.Where(x => x.severity == MessageType.Error || (policy.warningsAsErrors && x.severity == MessageType.Warning)).ToArray();
            var report = new ValidationReport {
                utc = DateTime.UtcNow.ToString("O"), target = options.target.ToString(), development = (options.options & BuildOptions.Development) != 0,
                enabled = policy.enabled, warningsAsErrors = policy.warningsAsErrors, scenes = options.scenes ?? Array.Empty<string>(),
                blocked = errors.Length > 0,
                issues = issues.Select(x => new ReportIssue { severity = x.severity.ToString(), message = x.message, asset = x.context ? AssetDatabase.GetAssetPath(x.context) : "" }).ToArray()
            };
            try { Directory.CreateDirectory("Library/BaseUnity"); File.WriteAllText("Library/BaseUnity/LastBuildValidation.json", JsonUtility.ToJson(report, true)); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { if (log) Debug.LogWarning("BaseUnity could not write the validation report: " + e.Message); }
            if (log) foreach (var issue in issues)
                if (issue.severity == MessageType.Warning) Debug.LogWarning("BaseUnity: " + issue.message, issue.context);
            if (errors.Length > 0)
                throw new BuildFailedException("BaseUnity blocked this build (" + errors.Length + " issue(s)).\n" +
                    string.Join("\n", errors.Select(x => "• " + x.message)) + "\nSee Library/BaseUnity/LastBuildValidation.json and Tools > BaseUnity > Feature Manager.");
        }
        [Serializable] private sealed class ValidationReport
        {
            public string utc, target;
            public bool enabled, development, warningsAsErrors, blocked;
            public string[] scenes;
            public ReportIssue[] issues;
        }
        [Serializable] private sealed class ReportIssue { public string severity, message, asset; }
    }
}
