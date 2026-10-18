using System;
using System.IO;
using System.Linq;
using BaseUnity.Core;
using BaseUnity.Editor;
using BaseUnity.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BaseUnity.Tests
{
    public sealed class AutomaticBuildValidationTests
    {
        private string folder, bootScene;
        private FeatureConfig config;
        private readonly BuildValidationPolicy policy = new BuildValidationPolicy();
        [SetUp] public void Setup()
        {
            folder = "Assets/BaseUnityBuildTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            config = ScriptableObject.CreateInstance<FeatureConfig>(); AssetDatabase.CreateAsset(config, folder + "/Features.asset");
            bootScene = CreateScene("Bootstrap", true);
        }
        [TearDown] public void Cleanup() { AssetDatabase.DeleteAsset(folder); }
        private string CreateScene(string name, bool bootstrap, bool duplicate = false)
        {
            string path = folder + "/" + name + ".unity";
            var script = AssetDatabase.FindAssets("GameBootstrap t:MonoScript").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonoScript>).First(x => x && x.GetClass() == typeof(GameBootstrap));
            string scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
            string configGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
            // Tiny serialized scene fixtures avoid touching the runner's unsaved scene or the user's open scenes.
            var yaml = new System.Text.StringBuilder("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            int count = bootstrap ? (duplicate ? 2 : 1) : 0;
            for (int i = 0; i < count; i++)
            {
                int go = i * 10 + 1, transform = go + 1, component = go + 2;
                yaml.Append($@"--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {transform}}}
  - component: {{fileID: {component}}}
  m_Layer: 0
  m_Name: Bootstrap{i}
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &{transform}
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children: []
  m_Father: {{fileID: 0}}
--- !u!114 &{component}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  featureConfig: {{fileID: 11400000, guid: {configGuid}, type: 2}}
");
            }
            yaml.Append("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:");
            if (count == 0) yaml.Append(" []\n");
            else { yaml.Append("\n"); for (int i = 0; i < count; i++) yaml.Append("  - {fileID: " + (i * 10 + 2) + "}\n"); }
            File.WriteAllText(path, yaml.ToString()); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }
        private BuildPlayerOptions Options(params string[] paths) => new BuildPlayerOptions { scenes = paths, target = BuildTarget.Android, options = BuildOptions.Development };
        [Test] public void UsesActualBuildScenesAndPreservesOpenSceneSetup()
        {
            var before = EditorSceneManager.GetSceneManagerSetup(); var global = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/MissingButNotInBuild.unity", true) };
                Assert.IsEmpty(FeatureBuildProcessor.ValidateBuild(Options(bootScene), policy));
                Assert.AreEqual("Assets/MissingButNotInBuild.unity", EditorBuildSettings.scenes[0].path);
                CollectionAssert.AreEqual(before.Select(x => x.path), EditorSceneManager.GetSceneManagerSetup().Select(x => x.path));
                CollectionAssert.AreEqual(before.Select(x => x.isActive), EditorSceneManager.GetSceneManagerSetup().Select(x => x.isActive));
            }
            finally { EditorBuildSettings.scenes = global; }
        }
        [Test] public void UnusedBrokenConfigsAreIgnoredAndReferencedConfigIsChecked()
        {
            var unused = ScriptableObject.CreateInstance<FeatureConfig>(); unused.offline.enabled = true; unused.offline.startingCoins = -1;
            AssetDatabase.CreateAsset(unused, folder + "/Unused.asset");
            Assert.IsEmpty(FeatureBuildProcessor.ValidateBuild(Options(bootScene), policy));
            config.offline.enabled = true; config.offline.startingCoins = -1;
            Assert.IsTrue(FeatureBuildProcessor.ValidateBuild(Options(bootScene), policy).Any(x => x.message.Contains("starting balances")));
        }
        [Test] public void MissingOrEmptyScenesBlockWithActionableErrors()
        {
            Assert.Throws<BuildFailedException>(() => FeatureBuildProcessor.CheckBuild(Options("Assets/Missing.unity"), policy, false));
            Assert.IsTrue(FeatureBuildProcessor.ValidateBuild(Options(""), policy).Any(x => x.message.Contains("empty scene path")));
            Assert.IsTrue(FeatureBuildProcessor.ValidateBuild(Options(), policy).Any(x => x.message.StartsWith("No scenes")));
        }
        [Test] public void DisabledPolicySkipsChecks()
        {
            Assert.IsEmpty(FeatureBuildProcessor.ValidateBuild(Options("Assets/Missing.unity"), new BuildValidationPolicy { enabled = false }));
        }
        [Test] public void TestAdsBlockReleaseButAllowDevelopmentAndDisabledFormats()
        {
            config.ads.enabled = true;
            Assert.IsTrue(FeatureBuildProcessor.ValidateRelease(config, BuildOptions.None, policy).Any());
            Assert.IsEmpty(FeatureBuildProcessor.ValidateRelease(config, BuildOptions.Development, policy));
            Assert.IsEmpty(FeatureBuildProcessor.ValidateRelease(config, BuildOptions.None, new BuildValidationPolicy { blockTestAdsInRelease = false }));
            config.ads.banner = config.ads.interstitial = config.ads.rewarded = false;
            Assert.IsEmpty(FeatureBuildProcessor.ValidateRelease(config, BuildOptions.None, policy));
        }
        [Test] public void DisabledAdsIgnoreStaleTestModeInRelease()
        { Assert.IsEmpty(FeatureBuildProcessor.ValidateRelease(config, BuildOptions.None, policy)); }
        [Test] public void BootstrapMustBeFirstInThisBuild()
        {
            string home = CreateScene("Home", false);
            Assert.IsTrue(FeatureBuildProcessor.ValidateBuild(Options(home, bootScene), policy).Any(x => x.message.Contains("Bootstrap scene first")));
            Assert.IsEmpty(FeatureBuildProcessor.ValidateBuild(Options(bootScene, home), policy));
        }
        [Test] public void WarningsOnlyBlockWhenPolicyRequiresItAndReportIsWritten()
        {
            string duplicate = CreateScene("Multiple", true, true);
            FeatureBuildProcessor.CheckBuild(Options(duplicate), policy, false);
            Assert.Throws<BuildFailedException>(() => FeatureBuildProcessor.CheckBuild(Options(duplicate), new BuildValidationPolicy { warningsAsErrors = true }, false));
            string report = File.ReadAllText("Library/BaseUnity/LastBuildValidation.json");
            StringAssert.Contains("\"blocked\": true", report); StringAssert.Contains("Multiple BaseUnity bootstraps", report);
        }
        [Test] public void InvalidGameFlowLevelsAndSaveSlotAreReported()
        {
            var type = FeatureConfigurationUtility.FindType("BaseUnity.GameFlow.GameFlowConfig");
            if (type == null) Assert.Ignore("Import the optional Game Flow sample to run this check.");
            var flow = ScriptableObject.CreateInstance(type);
            try
            {
                var data = new SerializedObject(flow); data.FindProperty("saveSlot").stringValue = "../invalid";
                data.FindProperty("levels").GetArrayElementAtIndex(0).FindPropertyRelative("targetScore").intValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                var issues = FeatureBuildValidator.ValidateScenes(flow, new[] { new EditorBuildSettingsScene(bootScene, true) });
                Assert.IsTrue(issues.Any(x => x.message.Contains("save slot"))); Assert.IsTrue(issues.Any(x => x.message.Contains("target must be positive")));
            }
            finally { Object.DestroyImmediate(flow); }
        }
    }
}
