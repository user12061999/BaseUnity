using System;
using System.IO;
using BaseUnity.Core;
using BaseUnity.Editor;
using NUnit.Framework;
using UnityEditor;

namespace BaseUnity.Tests
{
    public sealed class MobileBuildProfileTests
    {
        [Test] public void DevelopmentFlagsAndSceneCopyAreIndependent()
        {
            var profile = new MobileBuildProfile { scenes = new[] { "Assets/Bootstrap.unity" }, scriptDebugging = true, autoConnectProfiler = true, androidAppBundle = true };
            var result = MobileBuildProfiles.CreateOptions(MobileBuildKind.Development, profile, BuildTarget.Android, "test.apk");
            Assert.IsTrue((result.options & BuildOptions.Development) != 0);
            Assert.IsTrue((result.options & BuildOptions.AllowDebugging) != 0);
            Assert.IsTrue((result.options & BuildOptions.ConnectWithProfiler) != 0);
            Assert.IsFalse((result.options & BuildOptions.AutoRunPlayer) != 0);
            Assert.AreEqual("apk", MobileBuildProfiles.Extension(MobileBuildKind.Development, profile));
            result.scenes[0] = "Other";
            Assert.AreEqual("Assets/Bootstrap.unity", profile.scenes[0]);
        }
        [Test] public void ReleaseNeverInheritsDevelopmentFlags()
        {
            var profile = new MobileBuildProfile { scriptDebugging = true, autoConnectProfiler = true, androidAppBundle = true };
            var result = MobileBuildProfiles.CreateOptions(MobileBuildKind.Release, profile, BuildTarget.Android, "release.aab");
            Assert.AreEqual(BuildOptions.StrictMode, result.options);
            Assert.AreEqual("aab", MobileBuildProfiles.Extension(MobileBuildKind.Release, profile));
            profile.androidAppBundle = false;
            Assert.AreEqual("apk", MobileBuildProfiles.Extension(MobileBuildKind.Release, profile));
            Assert.Throws<ArgumentException>(() => MobileBuildProfiles.CreateOptions(MobileBuildKind.Release, profile, BuildTarget.StandaloneWindows64, "test"));
        }
        [Test] public void SaveEnvironmentsPreserveProductionAndSeparateTestData()
        {
            var root = Path.Combine(Path.GetTempPath(), "BaseUnityPaths");
            Assert.AreEqual(Path.Combine(root, "BaseUnity"), BuildEnvironment.ResolveSaveDirectory(root, "Release"));
            Assert.AreEqual(Path.Combine(root, "BaseUnity_Editor"), BuildEnvironment.ResolveSaveDirectory(root, "Editor"));
            Assert.AreEqual(Path.Combine(root, "BaseUnity_Development"), BuildEnvironment.ResolveSaveDirectory(root, "Development"));
            Assert.Throws<ArgumentException>(() => BuildEnvironment.ResolveSaveDirectory(root, "../Release"));
            Assert.IsTrue(BuildEnvironment.IsDevelopment);
        }
        [Test] public void TestResetArchivesBackupsAndRefusesProduction()
        {
            string root = Path.Combine(Path.GetTempPath(), "BaseUnityReset_" + Guid.NewGuid().ToString("N"));
            try
            {
                string development = BuildEnvironment.ResolveSaveDirectory(root, "Development");
                string production = BuildEnvironment.ResolveSaveDirectory(root, "Release");
                Directory.CreateDirectory(development); Directory.CreateDirectory(production);
                File.WriteAllText(Path.Combine(development, "progress.json"), "test");
                File.WriteAllText(Path.Combine(development, "progress.json.bak"), "previous test");
                File.WriteAllText(Path.Combine(production, "progress.json"), "real player");
                Assert.Throws<InvalidOperationException>(() => BuildEnvironment.ArchiveTestSaves(root, "Release"));
                string backup = BuildEnvironment.ArchiveTestSaves(root, "Development");
                Assert.IsFalse(Directory.Exists(development));
                Assert.AreEqual("test", File.ReadAllText(Path.Combine(backup, "progress.json")));
                Assert.AreEqual("previous test", File.ReadAllText(Path.Combine(backup, "progress.json.bak")));
                Assert.AreEqual("real player", File.ReadAllText(Path.Combine(production, "progress.json")));
                Assert.IsNull(BuildEnvironment.ArchiveTestSaves(root, "Development"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
