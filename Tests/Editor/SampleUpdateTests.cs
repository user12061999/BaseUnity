using System;
using System.IO;
using System.Linq;
using BaseUnity.Editor;
using BaseUnity.Features;
using NUnit.Framework;
using UnityEngine;

namespace BaseUnity.Tests
{
    public sealed class SampleUpdateTests
    {
        private string temporary, project, source, target;
        private SampleUpdateStore store;
        private const string Destination = "Assets/GameFlow";
        [SetUp] public void Setup()
        {
            temporary = Path.Combine(Path.GetTempPath(), "BaseUnityUpdater_" + Guid.NewGuid().ToString("N"));
            project = Path.Combine(temporary, "Project"); source = Path.Combine(temporary, "Source"); target = Path.Combine(project, Destination);
            Directory.CreateDirectory(source); Directory.CreateDirectory(Path.Combine(project, "Assets")); store = new SampleUpdateStore(project);
        }
        [TearDown] public void Cleanup() { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
        private void Source(string path, string data) { string file = Path.Combine(source, path); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file, data); }
        private void Local(string path, string data) { string file = Path.Combine(target, path); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file, data); }
        private SampleUpdatePlan Plan(string version = "1") => store.Preview(source, Destination, version);
        private string Apply() => store.Apply(Plan());
        [Test] public void InstallsAndTracksWithoutCopyingSourceGuids()
        {
            Source("Runtime/A.cs", "v1"); Source("Runtime/A.cs.meta", "source-guid"); Apply();
            Assert.AreEqual("v1", File.ReadAllText(Path.Combine(target, "Runtime/A.cs"))); Assert.IsFalse(File.Exists(Path.Combine(target, "Runtime/A.cs.meta")));
            Assert.AreEqual("1", store.ReadManifest(Destination).installedVersion); Assert.AreEqual(SampleFileState.Identical, Plan().files.Single().state);
        }
        [Test] public void CleanUpdatePreservesGuidAndGeneratedAssetsThenRestores()
        {
            Source("A.cs", "v1"); Apply(); Local("A.cs.meta", "game-guid"); Local("Scenes/Home.unity", "custom scene");
            Source("A.cs", "v2"); var plan = Plan("2"); Assert.AreEqual(SampleFileState.Update, plan.files.Single().state);
            string backup = store.Apply(plan); Assert.IsFalse(store.ReadBackup(backup).hasSceneSnapshot); Assert.AreEqual("v2", File.ReadAllText(Path.Combine(target, "A.cs")));
            Assert.AreEqual("game-guid", File.ReadAllText(Path.Combine(target, "A.cs.meta"))); store.Restore(backup);
            Assert.AreEqual("v1", File.ReadAllText(Path.Combine(target, "A.cs"))); Assert.AreEqual("1", store.ReadManifest(Destination).installedVersion);
            Assert.AreEqual("custom scene", File.ReadAllText(Path.Combine(target, "Scenes/Home.unity")));
        }
        [Test] public void LocalAndUpstreamChangesBecomeConflictAndAreKeptByDefault()
        {
            Source("A.cs", "v1"); Apply(); Local("A.cs", "my game"); Source("A.cs", "v2"); var plan = Plan("2");
            Assert.AreEqual(SampleFileState.Conflict, plan.files.Single().state); Assert.IsFalse(plan.files.Single().selected);
            store.Apply(plan); Assert.AreEqual("my game", File.ReadAllText(Path.Combine(target, "A.cs"))); Assert.AreEqual("1", store.ReadManifest(Destination).installedVersion);
            plan = Plan("2"); plan.files.Single().selected = true; string id = store.Apply(plan); store.Restore(id);
            Assert.AreEqual("my game", File.ReadAllText(Path.Combine(target, "A.cs")));
        }
        [Test] public void ExistingUntrackedDifferencesAreNeverAssumedToBeStock()
        {
            Source("A.cs", "incoming"); Local("A.cs", "existing"); var plan = Plan(); Assert.AreEqual(SampleFileState.Conflict, plan.files.Single().state);
            store.Apply(plan); Assert.AreEqual("existing", File.ReadAllText(Path.Combine(target, "A.cs"))); Assert.IsEmpty(store.ReadManifest(Destination).files);
        }
        [Test] public void TracksIdenticalExistingFilesIgnoringTextLineEndings()
        {
            Source("A.cs", "a\nb\n"); Local("A.cs", "a\r\nb\r\n"); var plan = Plan();
            Assert.AreEqual(SampleFileState.Identical, plan.files.Single().state); store.Apply(plan);
            Assert.AreEqual("a\r\nb\r\n", File.ReadAllText(Path.Combine(target, "A.cs"))); Assert.AreEqual(1, store.ReadManifest(Destination).files.Count);
        }
        [Test] public void LocalOnlyChangesAreKeptWithoutBlockingVersionWhenSourceIsUnchanged()
        {
            Source("A.cs", "v1"); Apply(); Local("A.cs", "my custom"); var plan = Plan("2"); Assert.AreEqual(SampleFileState.LocalModified, plan.files.Single().state);
            store.Apply(plan); Assert.AreEqual("2", store.ReadManifest(Destination).installedVersion); Assert.AreEqual("my custom", File.ReadAllText(Path.Combine(target, "A.cs")));
        }
        [Test] public void LocalDeletionAndUpstreamRemovalRequireExplicitSelection()
        {
            Source("A.cs", "v1"); Apply(); File.Delete(Path.Combine(target, "A.cs")); Assert.AreEqual(SampleFileState.LocalDeleted, Plan().files.Single().state);
            store.Apply(Plan()); Assert.IsFalse(File.Exists(Path.Combine(target, "A.cs")));
            Local("A.cs", "custom"); Local("A.cs.meta", "guid"); File.Delete(Path.Combine(source, "A.cs")); var plan = Plan("2");
            Assert.AreEqual(SampleFileState.SourceRemoved, plan.files.Single().state); Assert.IsFalse(plan.files.Single().selected);
            plan.files.Single().selected = true; string id = store.Apply(plan); Assert.IsFalse(File.Exists(Path.Combine(target, "A.cs.meta")));
            store.Restore(id); Assert.AreEqual("custom", File.ReadAllText(Path.Combine(target, "A.cs"))); Assert.AreEqual("guid", File.ReadAllText(Path.Combine(target, "A.cs.meta")));
        }
        [Test] public void RestoreAddedFileRetainsMetaThatExistedBeforeUpdate()
        {
            Source("A.cs", "new"); Local("A.cs.meta", "preexisting guid"); string id = Apply(); store.Restore(id);
            Assert.IsFalse(File.Exists(Path.Combine(target, "A.cs"))); Assert.AreEqual("preexisting guid", File.ReadAllText(Path.Combine(target, "A.cs.meta")));
        }
        [Test] public void StalePreviewCannotOverwriteChangedLocalOrSourceFiles()
        {
            Source("A.cs", "v1"); Apply(); Source("A.cs", "v2"); var plan = Plan(); Local("A.cs", "edit after preview");
            Assert.Throws<IOException>(() => store.Apply(plan)); Assert.AreEqual("edit after preview", File.ReadAllText(Path.Combine(target, "A.cs")));
            plan = Plan(); Source("A.cs", "changed source after preview"); Assert.Throws<IOException>(() => store.Apply(plan));
        }
        [Test] public void RestoreStopsBeforeWritingAnyFileIfNewerChangesExist()
        {
            Source("A.cs", "v1"); Source("B.cs", "b1"); Apply(); Source("A.cs", "v2"); Source("B.cs", "b2"); string id = Apply(); Local("B.cs", "newer edit");
            Assert.Throws<IOException>(() => store.Restore(id)); Assert.AreEqual("v2", File.ReadAllText(Path.Combine(target, "A.cs")));
            Assert.AreEqual("newer edit", File.ReadAllText(Path.Combine(target, "B.cs")));
        }
        [Test] public void FailedWriteRollsBackEarlierChangesAndManifest()
        {
            Source("A.cs", "v1"); Apply(); Source("A.cs", "v2"); Source("Z.cs", "new"); Directory.CreateDirectory(Path.Combine(target, "Z.cs"));
            Assert.Throws<IOException>(() => Apply()); Assert.AreEqual("v1", File.ReadAllText(Path.Combine(target, "A.cs")));
            Assert.AreEqual("1", store.ReadManifest(Destination).installedVersion);
        }
        [Test] public void RejectsTraversalFutureManifestsAndForgedBackupPaths()
        {
            Source("A.cs", "v1"); Assert.Throws<IOException>(() => store.ValidateDestination("../outside"));
            Assert.Throws<IOException>(() => store.ValidateDestination("Assets")); Assert.Throws<IOException>(() => store.ValidateDestination("Assets/Game/.. /escape")); Assert.Throws<IOException>(() => store.Preview(source, "../outside", "1"));
            string id = Apply(); string path = Path.Combine(store.BackupDirectory, id + ".json"); var backup = store.ReadBackup(id);
            backup.files[0].path = "../escape"; File.WriteAllText(path, JsonUtility.ToJson(backup)); Assert.Throws<IOException>(() => store.Restore(id));
            File.WriteAllText(store.ManifestPath(Destination), "{\"schema\":99}"); Assert.Throws<IOException>(() => Plan());
        }
        [Test] public void SetupBackupRestoresConfigsAndRemovesOnlyNewGeneratedFiles()
        {
            Local("Config.asset", "old config"); Local("Config.asset.meta", "guid");
            string id = store.BeginSetup(Destination, Array.Empty<SampleSceneState>());
            Local("Config.asset", "new config"); Local("Prefabs/Menu.prefab", "generated"); Local("Prefabs/Menu.prefab.meta", "new guid");
            store.CompleteSetup(id, Array.Empty<SampleSceneState>()); store.Restore(id);
            Assert.AreEqual("old config", File.ReadAllText(Path.Combine(target, "Config.asset"))); Assert.AreEqual("guid", File.ReadAllText(Path.Combine(target, "Config.asset.meta")));
            Assert.IsFalse(File.Exists(Path.Combine(target, "Prefabs/Menu.prefab")));
        }
        [Test] public void ModuleSetupChangesOnlySwitchesAndRetainsIdsAndEconomySettings()
        {
            var config = ScriptableObject.CreateInstance<FeatureConfig>();
            try
            {
                config.ads.androidRewarded = "keep-id"; config.offline.winCoins = 123; config.localization.defaultLanguage = "vi";
                BaseUnitySetupWorkflow.ApplyModules(config, new BaseUnitySetupOptions { offline = false, ads = true, firebase = true });
                Assert.IsFalse(config.offline.enabled); Assert.IsTrue(config.ads.enabled); Assert.AreEqual("keep-id", config.ads.androidRewarded);
                Assert.AreEqual(123, config.offline.winCoins); Assert.AreEqual("vi", config.localization.defaultLanguage);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void TrackingSurvivesMovingTheProjectToAnotherDirectory()
        {
            Source("A.cs", "v1"); Apply(); string moved = Path.Combine(temporary, "MovedProject"); Directory.Move(project, moved);
            var movedStore = new SampleUpdateStore(moved); Source("A.cs", "v2"); var plan = movedStore.Preview(source, Destination, "2");
            Assert.AreEqual(SampleFileState.Update, plan.files.Single().state); movedStore.Apply(plan);
            Assert.AreEqual("2", movedStore.ReadManifest(Destination).installedVersion);
            Assert.AreEqual("v2", File.ReadAllText(Path.Combine(moved, Destination, "A.cs")));
        }
        [Test] public void DiffContainsActualAddedAndRemovedLines()
        {
            string diff = SampleDiffWindow.Difference("a\nb\nc", "a\nnew\nc"); StringAssert.Contains("- b", diff); StringAssert.Contains("+ new", diff);
        }
    }
}
