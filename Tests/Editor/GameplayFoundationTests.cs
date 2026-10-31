using System;
using System.Collections.Generic;
using BaseUnity.Features;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;
namespace BaseUnity.Tests
{
    public sealed class GameplayFoundationTests
    {
        private sealed class Store : ISaveStore
        {
            public bool fail;
            private readonly Dictionary<string,string> data = new Dictionary<string,string>();
            public void Save<T>(string slot,T value,int version=1) where T:class { if(fail) throw new System.IO.IOException("Disk full"); data[slot]=JsonUtility.ToJson(value); }
            public bool TryLoad<T>(string slot,out T value,int version=1) where T:class { value=data.TryGetValue(slot,out var json)?JsonUtility.FromJson<T>(json):null; return value!=null; }
            public void Delete(string slot) { data.Remove(slot); }
        }
        private Store store;
        private GameplaySettings settings;
        private OfflineSettings economy;
        private OfflineService offline;
        private GameplayService service;
        private DateTime now;
        [SetUp] public void Setup() { store=new Store(); settings=new GameplaySettings { enabled=true, energyRefillSeconds=60, experiencePerLevel=50, experiencePerWin=50 }; economy=new OfflineSettings { enabled=false }; now=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc); Reload(); }
        private void Reload() { offline=new OfflineService(economy,store,()=>now,settings); service=new GameplayService(settings,offline,()=>now); }
        [Test] public void EnergyRefillsCapsAndDoesNotGrantOnClockRollback()
        {
            Assert.AreEqual(5,service.Energy); Assert.IsTrue(service.SpendRunEnergy()); Assert.AreEqual(4,service.Energy);
            now=now.AddSeconds(30); Assert.AreEqual(4,service.Energy); Assert.AreEqual(30,service.RefillRemaining,.01);
            now=now.AddSeconds(30); Assert.AreEqual(5,service.Energy); Assert.IsTrue(service.SpendRunEnergy()); Assert.AreEqual(4,service.Energy);
            now=now.AddHours(-1); Assert.AreEqual(4,service.Energy); Reload(); Assert.AreEqual(4,service.Energy);
            now=now.AddHours(2); Assert.AreEqual(5,service.Energy);
        }
        [Test] public void FailedEnergySaveCannotSpendAndZeroEnergyBlocksRun()
        {
            store.fail=true; Assert.IsFalse(service.SpendRunEnergy()); Assert.AreEqual(5,service.Energy);
            store.fail=false; for(int i=0;i<5;i++) Assert.IsTrue(service.SpendRunEnergy()); Assert.IsFalse(service.CanStartRun); Assert.IsFalse(service.SpendRunEnergy()); Reload(); Assert.AreEqual(0,service.Energy);
        }
        [Test] public void WelcomeGiftIsAtomicWithWalletAndCannotBeClaimedAgainAfterReload()
        {
            store.fail=true; Assert.IsFalse(service.Claim("welcome")); Assert.AreEqual(0,offline.Coins); Assert.IsTrue(service.CanClaim("welcome"));
            store.fail=false; Assert.IsTrue(service.Claim("welcome")); Assert.AreEqual(100,offline.Coins); Assert.IsFalse(service.Claim("welcome")); Reload(); Assert.IsFalse(service.CanClaim("welcome")); Assert.AreEqual(100,offline.Coins);
        }
        [Test] public void ReturnAndPeriodicGiftsRespectSavedIntervals()
        {
            Assert.IsTrue(service.Visit()); Assert.IsFalse(service.CanClaim("return")); now=now.AddDays(3); Assert.IsTrue(service.Visit()); Assert.IsTrue(service.Claim("return")); Assert.IsFalse(service.Claim("return"));
            Assert.IsTrue(service.Claim("free")); Assert.IsFalse(service.CanClaim("free")); now=now.AddHours(-1); Assert.IsFalse(service.CanClaim("free")); now=now.AddHours(5); Assert.IsTrue(service.CanClaim("free"));
        }
        [Test] public void PlaytimeFlushAndGiftCannotReenterAndDoubleCount()
        {
            service.TickPlaytime(300); offline.Changed+=()=>service.FlushPlaytime();
            Assert.IsTrue(service.FlushPlaytime()); Assert.AreEqual(300,offline.GameplaySnapshot.playSeconds);
            Assert.IsTrue(service.Claim("playtime")); Assert.IsFalse(service.CanClaim("playtime")); Assert.AreEqual(300,offline.GameplaySnapshot.playSeconds);
        }
        [Test] public void ExperienceAndLevelRewardCommitWithResultOnlyOnce()
        {
            Assert.IsTrue(offline.InitializeProgress(new PlayerProgress())); var session=offline.BeginSession();
            store.fail=true; Assert.IsFalse(offline.FinishSession(session,1,true,10)); Assert.AreEqual(0,service.Experience); Assert.AreEqual(0,offline.Coins);
            store.fail=false; Assert.IsTrue(offline.FinishSession(session,1,true,10)); Assert.AreEqual(50,service.Experience); Assert.AreEqual(2,service.PlayerLevel); Assert.AreEqual(50,offline.Coins);
            Assert.IsFalse(offline.FinishSession(session,1,true,10)); Reload(); Assert.AreEqual(50,service.Experience); Assert.AreEqual(50,offline.Coins);
        }
        [Test] public void HeartbeatRecordsRecentActivitySoActivePlayersDoNotReceiveReturnGifts()
        {
            Assert.IsTrue(service.Visit()); now=now.AddDays(3); Assert.IsTrue(service.Heartbeat()); now=now.AddMinutes(2); Assert.IsTrue(service.Visit()); Assert.IsFalse(service.CanClaim("return"));
        }
        [Test] public void DisabledModulesDoNotSpendOrGrant()
        {
            settings.enabled=false; Reload(); Assert.IsTrue(service.SpendRunEnergy()); Assert.IsFalse(service.Claim("welcome")); service.TickPlaytime(500); Assert.IsTrue(service.FlushPlaytime()); Assert.AreEqual(0,offline.GameplaySnapshot.playSeconds);
        }
        [Test] public void PreferenceSaveFailureDoesNotPublishOrMutateCurrentSettings()
        {
            using(var preferences=new AdvancedSettingsService(new AdvancedSettings { enabled=true },store))
            {
                int changes=0; preferences.Changed+=()=>changes++; var next=preferences.Current; next.frameRate=30; next.reducedMotion=true;
                store.fail=true; Assert.IsFalse(preferences.Save(next)); Assert.AreEqual(60,preferences.Current.frameRate); Assert.AreEqual(0,changes);
                store.fail=false; Assert.IsTrue(preferences.Save(next)); Assert.AreEqual(1,changes);
                preferences.Dispose(); preferences.Dispose(); Assert.IsFalse(preferences.Save(next));
            }
            using(var reloaded=new AdvancedSettingsService(new AdvancedSettings { enabled=true },store)) { Assert.AreEqual(30,reloaded.Current.frameRate); Assert.IsTrue(reloaded.Current.reducedMotion); }
        }
        [Test] public void PlayerLevelIsCappedWithoutOverflowAtMaximumExperience()
        { Assert.AreEqual(settings.maximumPlayerLevel, GameplayService.LevelFor(long.MaxValue, settings)); }
        [Test] public void BadgeCountsAreDerivedAndClearingRemovesTheBadge()
        {
            var notices=new InGameNoticeService(); int changes=0; notices.Changed+=()=>changes++;
            notices.Set("gifts",3); notices.Set("gifts",3); Assert.AreEqual(1,changes); Assert.AreEqual(3,notices.Count("gifts")); notices.Set("gifts",0); Assert.AreEqual(0,notices.Count("gifts")); Assert.AreEqual(2,changes);
        }
    }
}
