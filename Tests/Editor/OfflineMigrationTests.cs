using System;
using System.IO;
using BaseUnity.Features;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;

namespace BaseUnity.Tests
{
    public sealed class OfflineMigrationTests
    {
        [Serializable] private sealed class LegacyEconomy { public long coins, gems; }
        private string directory;
        private JsonSaveStore store;
        private OfflineSettings settings;
        [SetUp] public void Setup()
        { directory = Path.Combine(Path.GetTempPath(), "baseunity_v08_" + Guid.NewGuid().ToString("N")); store = new JsonSaveStore(directory); settings = new OfflineSettings { enabled = true }; }
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private OfflineService Load() => new OfflineService(settings, store);
        [Test] public void MigrationPreservesEconomyAndImportsProgressOnlyOnce()
        {
            store.Save(settings.saveSlot, new LegacyEconomy { coins = 900, gems = 12 });
            var service = Load(); Assert.IsTrue(service.InitializeProgress(new PlayerProgress { bestScore = 42, sessions = 7, highestUnlockedLevel = 3 }));
            service = Load(); Assert.AreEqual(900, service.Coins); Assert.AreEqual(12, service.Gems);
            Assert.IsTrue(service.InitializeProgress(new PlayerProgress())); Assert.AreEqual(42, service.Progress.bestScore); Assert.AreEqual(3, service.Progress.highestUnlockedLevel);
            Assert.IsTrue(store.TryLoad<OfflineService.Profile>(settings.saveSlot, out var data)); Assert.AreEqual(2, data.schemaVersion);
        }
        [Test] public void ResultAndProgressAndRewardCommitTogetherAndRestore()
        {
            var service = Load(); service.InitializeProgress(new PlayerProgress());
            string session = service.BeginSession(); Assert.IsTrue(service.FinishSession(session, 1, true, 40, levelCount: 3));
            service = Load(); Assert.AreEqual(150, service.Coins); Assert.AreEqual(2, service.Progress.highestUnlockedLevel);
            Assert.AreEqual(1, service.Progress.sessions); Assert.AreEqual(session, service.PendingResult.session);
            Assert.IsFalse(service.FinishSession(session, 1, true, 40)); Assert.AreEqual(150, service.Coins);
            Assert.IsTrue(service.AcknowledgeResult()); Assert.IsNull(Load().PendingResult);
        }
        [Test] public void DurableVerifiedBonusRecoversExactlyOnce()
        {
            var service = Load(); service.InitializeProgress(new PlayerProgress()); string session = service.BeginSession(); service.FinishSession(session, 1, true, 30);
            Assert.IsTrue(service.RecordRewardedCompletion(session));
            service = Load(); Assert.IsTrue(service.PendingBonus); Assert.IsFalse(service.AcknowledgeResult());
            Assert.IsTrue(service.RecoverPendingBonus()); Assert.AreEqual(300, service.Coins);
            service = Load(); Assert.IsFalse(service.PendingBonus); Assert.IsTrue(service.RecoverPendingBonus()); Assert.AreEqual(300, service.Coins);
        }
        [Test] public void RestartNeverInventsAnUnverifiedAdReward()
        {
            var service = Load(); service.InitializeProgress(new PlayerProgress()); service.FinishSession(service.BeginSession(), 1, true, 30);
            service = Load(); Assert.IsFalse(service.PendingBonus); service.RecoverPendingBonus(); Assert.AreEqual(150, service.Coins); Assert.IsTrue(service.CanDouble);
        }
        [Test] public void DisablingEconomyKeepsUnifiedProgressAndExistingWallet()
        {
            var service = Load(); service.InitializeProgress(new PlayerProgress()); service.FinishSession(service.BeginSession(), 1, true, 30); service.AcknowledgeResult();
            settings.enabled = false; service = Load(); Assert.IsTrue(service.UsesUnifiedProgress);
            Assert.IsTrue(service.FinishSession(service.BeginSession(), 2, true, 60)); Assert.AreEqual(150, service.Coins);
            Assert.AreEqual(3, Load().Progress.highestUnlockedLevel); Assert.IsFalse(service.AddCurrency(Currency.Coin, 1));
        }
        [Test] public void CorruptMainRecoversBackupAndPreservesItOnNextWrite()
        {
            var service = Load(); service.InitializeProgress(new PlayerProgress()); service.AddCurrency(Currency.Coin, 20); service.AddCurrency(Currency.Coin, 30);
            string path = Path.Combine(directory, settings.saveSlot + ".json"); File.WriteAllText(path, "corrupt");
            service = Load(); Assert.IsTrue(service.RecoveredFromBackup); Assert.AreEqual(20, service.Coins);
            Assert.IsTrue(service.AddCurrency(Currency.Coin, 5)); Assert.AreEqual(1, Directory.GetFiles(directory, "*.corrupt-*").Length);
            File.WriteAllText(path, "corrupt again"); Assert.AreEqual(20, Load().Coins);
        }
        [Test] public void SemanticCorruptionAlsoUsesBackup()
        {
            store.Save(settings.saveSlot, new OfflineService.Profile { coins = 12 });
            store.Save(settings.saveSlot, new OfflineService.Profile { coins = -1 });
            var service = Load(); Assert.IsTrue(service.RecoveredFromBackup); Assert.AreEqual(12, service.Coins);
        }
        [Test] public void BothCorruptFilesAreNeverResetOrOverwritten()
        {
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, settings.saveSlot + ".json");
            File.WriteAllText(path, "broken"); File.WriteAllText(path + ".bak", "also broken");
            var service = Load(); Assert.IsNotNull(service.LoadError); Assert.IsFalse(service.AddCurrency(Currency.Coin, 100));
            Assert.IsFalse(service.InitializeProgress(new PlayerProgress())); Assert.AreEqual("broken", File.ReadAllText(path));
        }
        [Test] public void FutureSchemaDoesNotFallBackOrOverwrite()
        {
            store.Save(settings.saveSlot, new OfflineService.Profile { coins = 20 });
            store.Save(settings.saveSlot, new OfflineService.Profile { schemaVersion = 99, coins = 30 });
            var service = Load(); Assert.IsNotNull(service.LoadError); Assert.IsFalse(service.AddCurrency(Currency.Coin, 1));
            Assert.IsTrue(File.ReadAllText(Path.Combine(directory, settings.saveSlot + ".json")).Contains("99"));
        }
    }
}
