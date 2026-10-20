using System;
using System.IO;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;
namespace BaseUnity.Tests
{
    public sealed class SaveVersionTests
    {
        [Serializable] public sealed class Data { public int coins; public string label; }
        private string folder;
        private JsonSaveStore store;
        [SetUp] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "BaseUnitySaveVersion-" + Guid.NewGuid().ToString("N")); store = new JsonSaveStore(folder); }
        [TearDown] public void Cleanup() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        [Test] public void MigrationChainPersistsOnlyValidatedResultAndKeepsPreviousGeneration()
        {
            store.Save("slot", new Data { coins = 4 });
            store.Migrations.Register<Data>(1, json => { var data = JsonUtility.FromJson<Data>(json); data.coins *= 2; return JsonUtility.ToJson(data); });
            store.Migrations.Register<Data>(2, json => { var data = JsonUtility.FromJson<Data>(json); data.label = "v3"; return JsonUtility.ToJson(data); });
            Assert.AreEqual(SaveLoadState.Migrated, store.LoadVersioned<Data>("slot", out var upgraded, 3, data => data.coins >= 0));
            Assert.AreEqual(8, upgraded.coins); Assert.AreEqual("v3", upgraded.label);
            Assert.AreEqual(3, store.Inspect("slot").primaryVersion); Assert.AreEqual(1, store.Inspect("slot").backupVersion);
            Assert.AreEqual(SaveLoadState.Loaded, store.LoadVersioned<Data>("slot", out upgraded, 3, data => data.coins >= 0));
            Assert.AreEqual(8, upgraded.coins);
        }
        [Test] public void MissingStepAndFailedValidationPreserveFilesExactly()
        {
            store.Save("slot", new Data { coins = 3 }); store.Save("slot", new Data { coins = 5 });
            string main = File.ReadAllText(Path.Combine(folder, "slot.json")), backup = File.ReadAllText(Path.Combine(folder, "slot.json.bak"));
            int calls = 0;
            store.Migrations.Register<Data>(1, json => { calls++; return "{\"coins\":-1}"; });
            Assert.Throws<NotSupportedException>(() => store.LoadVersioned<Data>("slot", out _, 3, data => data.coins >= 0)); Assert.AreEqual(0, calls);
            Assert.Throws<SaveMigrationException>(() => store.LoadVersioned<Data>("slot", out _, 2, data => data.coins >= 0));
            Assert.AreEqual(main, File.ReadAllText(Path.Combine(folder, "slot.json"))); Assert.AreEqual(backup, File.ReadAllText(Path.Combine(folder, "slot.json.bak")));
        }
        [Test] public void ChecksumDetectsValidJsonTamperAndRecoveryPreservesGoodBackup()
        {
            store.Save("slot", new Data { coins = 3 }); store.Save("slot", new Data { coins = 9 });
            string path = Path.Combine(folder, "slot.json");
            // Edit the payload without updating its checksum, retaining a valid JSON envelope.
            var envelope = JsonUtility.FromJson<TestEnvelope>(File.ReadAllText(path)); envelope.payload = "{\"coins\":7}"; File.WriteAllText(path, JsonUtility.ToJson(envelope));
            Assert.IsFalse(store.Inspect("slot").primaryValid);
            Assert.IsTrue(store.TryLoad<Data>("slot", out var recovered)); Assert.AreEqual(3, recovered.coins); Assert.AreEqual(SaveLoadState.RecoveredFromBackup, store.LastLoadState);
            string backup = File.ReadAllText(path + ".bak"); store.Save("slot", new Data { coins = 4 });
            Assert.AreEqual(backup, File.ReadAllText(path + ".bak")); Assert.AreEqual(1, Directory.GetFiles(folder, "slot.json.corrupt-*").Length);
        }
        [Serializable] private sealed class TestEnvelope { public int version; public string payload, checksum; }
        [Test] public void FutureVersionCannotBeOverwrittenOrRestored()
        {
            store.Save("slot", new Data { coins = 3 }); store.Save("slot", new Data { coins = 9 }, 2);
            string path = Path.Combine(folder, "slot.json"), before = File.ReadAllText(path);
            Assert.IsFalse(store.TryLoad<Data>("slot", out _));
            Assert.Throws<NotSupportedException>(() => store.Save("slot", new Data()));
            Assert.Throws<NotSupportedException>(() => store.RestoreBackup<Data>("slot", data => true)); Assert.AreEqual(before, File.ReadAllText(path));
        }
        [Test] public void ExplicitRestoreKeepsBackupAndArchivesPreviousPrimary()
        {
            store.Save("slot", new Data { coins = 3 }); store.Save("slot", new Data { coins = 9 });
            store.RestoreBackup<Data>("slot", data => data.coins >= 0);
            Assert.IsTrue(store.TryLoad<Data>("slot", out var data)); Assert.AreEqual(3, data.coins);
            Assert.AreEqual(1, Directory.GetFiles(folder, "slot.json.corrupt-*").Length);
        }
    }
}
