using System;
using System.Collections.Generic;
using BaseUnity.Features;
using BaseUnity.Save;
using BaseUnity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace BaseUnity.Tests
{
    public sealed class OfflineTests
    {
        private sealed class Store : ISaveStore
        {
            public bool fail;
            public int writes;
            private readonly Dictionary<string, string> data = new Dictionary<string, string>();
            public void Save<T>(string slot, T value, int version = 1) where T : class
            { if (fail) throw new System.IO.IOException("Disk full"); data[slot] = JsonUtility.ToJson(value); writes++; }
            public bool TryLoad<T>(string slot, out T value, int version = 1) where T : class
            { value = data.TryGetValue(slot, out var json) ? JsonUtility.FromJson<T>(json) : null; return value != null; }
            public void Delete(string slot) { data.Remove(slot); }
        }
        private Store store;
        private OfflineSettings config;
        private DateTime today;
        private OfflineService NewService() => new OfflineService(config, store, () => today);
        [Test] public void BalanceFeedbackOnlyFollowsCommittedTransactionsAndDoesNotReplayOnLoad()
        {
            var service = NewService(); int events = 0; long delta = 0;
            service.BalanceChanged += (coins, gems) => { events++; delta += coins; };
            store.fail = true; Assert.IsFalse(service.AddCurrency(Currency.Coin, 50)); Assert.AreEqual(0, events);
            store.fail = false; Assert.IsTrue(service.AddCurrency(Currency.Coin, 50)); Assert.AreEqual(1, events); Assert.AreEqual(50, delta);
            Assert.IsTrue(service.Buy("energy")); Assert.AreEqual(2, events); Assert.AreEqual(0, delta);
            var reloaded = NewService(); reloaded.BalanceChanged += (coins, gems) => events++;
            Assert.AreEqual(2, events); Assert.AreEqual(200, reloaded.Coins);
        }
        [Test] public void AchievementClaimsAreAtomicAndSurviveDailyResetAndReload()
        {
            var service = NewService(); service.InitializeProgress(new PlayerProgress());
            Assert.IsFalse(service.ClaimAchievement("first_win"));
            service.FinishSession(service.BeginSession(), 1, true, 25);
            long balance = service.Coins;
            store.fail = true; Assert.IsFalse(service.ClaimAchievement("first_win")); Assert.AreEqual(balance, service.Coins);
            Assert.IsFalse(service.AchievementClaimed("first_win"));
            store.fail = false; Assert.IsTrue(service.ClaimAchievement("first_win")); Assert.AreEqual(balance + 200, service.Coins);
            today = today.AddDays(1); service = NewService();
            Assert.IsTrue(service.AchievementClaimed("first_win")); Assert.IsFalse(service.ClaimAchievement("first_win"));
            Assert.AreEqual(1, service.AchievementProgress(config.achievementGoals[0]));
        }
        [Test] public void VersionThreeMigratesWithoutInventingHistoricalScore()
        {
            store.Save(config.saveSlot, new OfflineService.Profile { schemaVersion = 3, coins = 900, progress = new PlayerProgress { sessions = 12, wins = 5 } });
            var service = NewService(); Assert.IsNull(service.LoadError);
            Assert.AreEqual(5, service.AchievementProgress(config.achievementGoals[0]));
            var score = new DailyMission { metric = MissionMetric.Score }; Assert.AreEqual(0, service.AchievementProgress(score));
            Assert.IsTrue(service.ClaimAchievement("first_win"));
            OfflineService.Profile saved; Assert.IsTrue(store.TryLoad(config.saveSlot, out saved)); Assert.AreEqual(4, saved.schemaVersion);
            config.achievements = false; Assert.IsFalse(NewService().ClaimAchievement("ten_games"));
        }
        [SetUp] public void Setup()
        { store = new Store(); config = new OfflineSettings { enabled = true, startingCoins = 200 }; today = new DateTime(2026, 10, 17, 23, 59, 0, DateTimeKind.Utc); }
        [Test] public void FailedUnifiedSaveDoesNotUnlockOrPayAndCanRetryOnce()
        {
            var service = NewService(); service.InitializeProgress(new PlayerProgress()); string session = service.BeginSession();
            store.fail = true; Assert.IsFalse(service.FinishSession(session, 1, true, 20));
            Assert.AreEqual(1, service.Progress.highestUnlockedLevel); Assert.AreEqual(0, service.Progress.sessions); Assert.AreEqual(200, service.Coins); Assert.IsNull(service.PendingResult);
            store.fail = false; Assert.IsTrue(service.FinishSession(session, 1, true, 20));
            service = NewService(); Assert.AreEqual(2, service.Progress.highestUnlockedLevel); Assert.AreEqual(1, service.Progress.sessions); Assert.AreEqual(350, service.Coins);
        }
        [Test] public void ReceiptSurvivesFailedPayoutAndResultAcknowledgement()
        {
            var service = NewService(); service.InitializeProgress(new PlayerProgress()); string session = service.BeginSession(); service.FinishSession(session, 1, true, 20);
            Assert.IsTrue(service.RecordRewardedCompletion(session)); store.fail = true; Assert.IsFalse(service.RecoverPendingBonus());
            store.fail = false; service = NewService(); Assert.IsTrue(service.RecoverPendingBonus()); Assert.AreEqual(500, service.Coins);
            store.fail = true; Assert.IsFalse(service.AcknowledgeResult()); Assert.IsNotNull(NewService().PendingResult);
            store.fail = false; Assert.IsTrue(service.AcknowledgeResult()); Assert.IsNull(NewService().PendingResult);
        }
        [Test] public void ShopIsAtomicAndPersistsInventoryAndLimits()
        {
            config.items[0].purchaseLimit = 1; var service = NewService();
            store.fail = true; Assert.IsFalse(service.Buy("energy")); Assert.AreEqual(200, service.Coins); Assert.AreEqual(0, service.ItemCount("energy"));
            store.fail = false; Assert.IsTrue(service.Buy("energy")); service = NewService();
            Assert.AreEqual(150, service.Coins); Assert.AreEqual(1, service.ItemCount("energy")); Assert.IsFalse(service.Buy("energy"));
            Assert.IsTrue(service.ConsumeItem("energy")); Assert.IsFalse(service.ConsumeItem("energy"));
        }
        [Test] public void RewardRetryAndReentrantCompletionCannotDuplicate()
        {
            var service = NewService(); string token = service.BeginSession();
            store.fail = true; Assert.IsFalse(service.FinishSession(token, 1, true, 30)); Assert.AreEqual(200, service.Coins);
            store.fail = false; bool duplicate = true;
            service.Changed += () => duplicate = service.FinishSession(token, 1, true, 30);
            Assert.IsTrue(service.FinishSession(token, 1, true, 30)); Assert.IsFalse(duplicate); Assert.AreEqual(350, service.Coins);
            Assert.IsTrue(service.DoubleReward(token)); Assert.IsFalse(service.DoubleReward(token)); Assert.AreEqual(500, service.Coins);
        }
        [Test] public void FirstWinBonusIsPersistentAndExpiredRewardRejected()
        {
            var service = NewService(); string old = service.BeginSession(); service.FinishSession(old, 1, true, 30);
            service = NewService(); string token = service.BeginSession(); Assert.IsFalse(service.DoubleReward(old));
            service.FinishSession(token, 1, true, 30); Assert.AreEqual(50, service.LastReward); Assert.AreEqual(400, service.Coins);
            service.BeginSession(); Assert.IsFalse(service.DoubleReward(token));
        }
        [Test] public void LoseAddsMissionProgressButNoWinReward()
        {
            var service = NewService(); service.FinishSession(service.BeginSession(), 1, false, 25);
            Assert.AreEqual(200, service.Coins); Assert.AreEqual(1, service.MissionProgress(config.dailyMissions[0]));
            Assert.AreEqual(0, service.MissionProgress(config.dailyMissions[1])); Assert.AreEqual(25, service.MissionProgress(config.dailyMissions[2]));
        }
        [Test] public void DailyClaimSurvivesRestartAndClockRollback()
        {
            var service = NewService(); Assert.IsTrue(service.ClaimDaily()); service = NewService(); Assert.IsFalse(service.ClaimDaily());
            today = today.AddMinutes(2); Assert.IsTrue(service.ClaimDaily()); Assert.AreEqual(325, service.Coins);
            today = today.AddDays(-1); Assert.IsFalse(service.ClaimDaily());
        }
        [Test] public void DailyFailedSaveDoesNotConsumeClaim()
        {
            var service = NewService(); store.fail = true; Assert.IsFalse(service.ClaimDaily()); Assert.IsTrue(service.CanClaimDaily);
            store.fail = false; Assert.IsTrue(service.ClaimDaily()); Assert.AreEqual(250, service.Coins);
        }
        [Test] public void MissionsClaimOnceThenResetAtUtcMidnight()
        {
            var service = NewService(); for (int i = 0; i < 3; i++) service.FinishSession(service.BeginSession(), 1, false, 10);
            Assert.IsTrue(service.ClaimMission("play")); Assert.IsFalse(service.ClaimMission("play"));
            today = today.AddMinutes(2); Assert.AreEqual(0, service.MissionProgress(config.dailyMissions[0])); Assert.IsFalse(service.MissionClaimed("play"));
            Assert.IsFalse(service.ClaimMission("play")); service.FinishSession(service.BeginSession(), 1, false, 0);
            Assert.AreEqual(1, service.MissionProgress(config.dailyMissions[0]));
        }
        [Test] public void DisabledFeaturesDoNotWriteOrGrant()
        {
            config.enabled = false; var service = NewService();
            Assert.IsFalse(service.AddCurrency(Currency.Coin, 1)); Assert.IsFalse(service.Buy("energy")); Assert.IsFalse(service.ClaimDaily());
            Assert.IsFalse(service.FinishSession(service.BeginSession(), 1, true, 10)); Assert.AreEqual(0, store.writes);
            config.enabled = true; config.shop = config.dailyReward = config.missions = config.levelRewards = false; service = NewService();
            Assert.IsFalse(service.Buy("energy")); Assert.IsFalse(service.ClaimDaily()); Assert.IsFalse(service.ClaimMission("play"));
            service.FinishSession(service.BeginSession(), 1, true, 100); Assert.AreEqual(200, service.Coins);
        }
        [Test] public void InvalidAmountAndOverflowDoNotChangeBalance()
        {
            var service = NewService(); Assert.IsFalse(service.Spend(Currency.Coin, -100)); Assert.IsFalse(service.Spend(Currency.Coin, 201));
            Assert.IsFalse(service.AddCurrency(Currency.Coin, long.MaxValue)); Assert.AreEqual(200, service.Coins);
        }
        [Test] public void LanguageSupportsPatternsOverridesFallbackAndSaveFailure()
        {
            var settings = new LocalizationSettings { enabled = true, defaultLanguage = "vi", translations = new[] { new Translation { english = "Energy", vietnamese = "Sức lực" } } };
            var service = new LocalizationService(settings, store);
            Assert.AreEqual("Sức lực", service.Text("Energy")); Assert.AreEqual("CHƠI MÀN 12", service.Text("PLAY LEVEL 12"));
            Assert.AreEqual("CAO NHẤT 42    /    LƯỢT CHƠI 8", service.Text("BEST 42    /    SESSIONS 8"));
            Assert.AreEqual("Custom title", service.Text("Custom title"));
            store.fail = true; Assert.IsFalse(service.SetLanguage("en")); Assert.AreEqual("vi", service.Language);
            store.fail = false; Assert.IsTrue(service.SetLanguage("en")); Assert.AreEqual("en", new LocalizationService(settings, store).Language);
        }
        [Test] public void ValidatorRejectsDuplicateIdsAndInvalidRewards()
        {
            var asset = ScriptableObject.CreateInstance<FeatureConfig>();
            try {
                asset.offline.enabled = true; asset.offline.items[1].id = asset.offline.items[0].id;
                asset.offline.dailyCoins = new[] { -1 }; asset.localization.enabled = true; asset.localization.defaultLanguage = "bad";
                var issues = FeatureBuildValidator.Validate(asset, BuildTarget.Android);
                Assert.IsTrue(issues.Exists(x => x.message.Contains("unique"))); Assert.IsTrue(issues.Exists(x => x.message.StartsWith("Daily reward:")));
                Assert.IsTrue(issues.Exists(x => x.message.StartsWith("Localization:")));
            } finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
