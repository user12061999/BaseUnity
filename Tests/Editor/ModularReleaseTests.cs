using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BaseUnity.Core;
using BaseUnity.Save;
using BaseUnity.Flow;
using BaseUnity.FlowEconomy;
using BaseUnity.EconomyModules;
using BaseUnity.Features;
using BaseUnity.IntegrationModules;
using NUnit.Framework;
using UnityEngine;
using L10n=BaseUnity.Localization.LocalizationService;
namespace BaseUnity.Tests
{
    public sealed class ModularReleaseTests
    {
        private sealed class MemorySave : ISaveStore
        {
            private readonly Dictionary<string,string> files=new Dictionary<string,string>();
            public bool fail;public int writes,failAt=-1;
            public void Save<T>(string key,T value,int version=1) where T:class { writes++;if(fail||writes==failAt)throw new IOException("disk full");files[key]=JsonUtility.ToJson(value); }
            public bool TryLoad<T>(string key,out T value,int version=1) where T:class { value=files.TryGetValue(key,out var json)?JsonUtility.FromJson<T>(json):null;return value!=null; }
            public void Delete(string key) { files.Remove(key); }
        }
        [Test] public void LanguagePersistsAndFormatsNumbersWithFallback()
        {
            var save=new MemorySave();var service=new L10n(save);Assert.AreEqual("Play",service.Text("Play"));Assert.IsTrue(service.SetLanguage("vi"));
            Assert.AreEqual("Chơi",service.Text("Play"));Assert.AreEqual("Xu: 1.000",service.Text("coin: 1000"));Assert.AreEqual("Màn 2 / 10",service.Text("Level 2 / 10"));Assert.AreEqual("1.234",service.Number(1234));
            Assert.AreEqual("unknown",service.Text("unknown"));Assert.AreEqual("vi",new L10n(save).Language);Assert.IsFalse(service.SetLanguage("xx"));service.SetLanguage("en");Assert.AreEqual("Play",service.Text("Play"));
        }
        [Test] public void LanguageSaveFailureKeepsLanguageAndDoesNotNotify()
        {
            var save=new MemorySave();var service=new L10n(save);int changes=0;service.Changed+=()=>changes++;save.fail=true;
            Assert.IsFalse(service.SetLanguage("vi"));Assert.AreEqual("en",service.Language);Assert.AreEqual(0,changes);Assert.IsNotNull(service.LastError);
        }
        [Test] public void CustomLocalizationOverridesAndRejectsDuplicateKeys()
        {
            var entries=new[]{new BaseUnity.Localization.LocalizedEntry { key="play",english="Start",vietnamese="Bắt đầu" }};
            var service=new L10n(new MemorySave(),entries,"vi");Assert.AreEqual("Bắt đầu",service.Text("play"));Assert.AreEqual("Bắt đầu",service.Text("Start"));
            Assert.Throws<ArgumentException>(()=>new L10n(new MemorySave(),new[]{entries[0],entries[0]}));
        }
        [Test] public async Task SessionIdsRemainStableOnPauseAndChangeOnRetry()
        {
            var flow=new GameFlowService();await flow.HomeAsync();await flow.PlayAsync();string first=flow.RunId;Assert.IsNotEmpty(first);flow.Pause();flow.Resume();Assert.AreEqual(first,flow.RunId);flow.Win();await flow.RetryAsync();Assert.AreNotEqual(first,flow.RunId);
        }
        [Test] public async Task BridgeWinGrantsOnceAndUpdatesInstalledObjectives()
        {
            var save=new MemorySave();var store=new EconomyStore(save);var flow=new GameFlowService();var events=new EventBus();var config=ScriptableObject.CreateInstance<FlowRewardConfig>();
            var quest=new QuestService(store,new[]{new ObjectiveDefinition { target=1 }},events);var achievement=new AchievementService(store,new[]{new ObjectiveDefinition { target=1 }},events);
            try { using(var bridge=new FlowEconomyBridge(flow,store,events,config)) {
                await flow.HomeAsync();await flow.PlayAsync();flow.Win();Assert.AreEqual(100,store.Balance("coin"));Assert.AreEqual(1,quest.Statuses[0].progress);Assert.AreEqual(1,achievement.Statuses[0].progress);Assert.IsFalse(flow.Win());
                bridge.RetryPending();Assert.AreEqual(100,store.Balance("coin"));Assert.IsFalse(bridge.HasPending);Assert.IsTrue(flow.CanNext);
            } } finally { quest.Dispose();achievement.Dispose();UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public async Task FailedOutcomeSaveBlocksNavigationUntilRetry()
        {
            var save=new MemorySave();var store=new EconomyStore(save);var flow=new GameFlowService();var config=ScriptableObject.CreateInstance<FlowRewardConfig>();
            try { using(var bridge=new FlowEconomyBridge(flow,store,new EventBus(),config)) {
                await flow.HomeAsync();await flow.PlayAsync();save.fail=true;flow.Win();Assert.AreEqual(0,store.Balance("coin"));Assert.IsTrue(bridge.HasPending);Assert.IsFalse(flow.CanNext);Assert.IsFalse(await flow.HomeAsync());Assert.IsFalse(await flow.RetryAsync());
                save.fail=false;Assert.IsTrue(bridge.RetryPending());Assert.AreEqual(100,store.Balance("coin"));Assert.IsTrue(flow.CanNext);Assert.IsTrue(await flow.NextAsync());
            } } finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public async Task PersistedOutboxSurvivesRestartAfterProgressSaveFails()
        {
            var save=new MemorySave();var store=new EconomyStore(save);var events=new EventBus();var flow=new GameFlowService();var config=ScriptableObject.CreateInstance<FlowRewardConfig>();
            var quest=new QuestService(store,new[]{new ObjectiveDefinition { target=2 }},events);
            try {
                using(var bridge=new FlowEconomyBridge(flow,store,events,config)) {
                    await flow.HomeAsync();await flow.PlayAsync();save.failAt=save.writes+2;flow.Win();Assert.IsTrue(bridge.HasPending);Assert.AreEqual(100,store.Balance("coin"));
                }
                quest.Dispose();save.failAt=-1;var loaded=new EconomyStore(save);var resumedEvents=new EventBus();using(var resumedQuest=new QuestService(loaded,new[]{new ObjectiveDefinition { target=2 }},resumedEvents))
                using(var bridge=new FlowEconomyBridge(new GameFlowService(),loaded,resumedEvents,config)) {
                    Assert.IsTrue(bridge.HasPending);Assert.IsTrue(bridge.RetryPending());Assert.AreEqual(100,loaded.Balance("coin"));Assert.AreEqual(1,resumedQuest.Statuses[0].progress);bridge.RetryPending();Assert.AreEqual(1,resumedQuest.Statuses[0].progress);
                }
            } finally { quest.Dispose();UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public async Task AcknowledgementFailureReplaysWithoutDuplicatingProgress()
        {
            var save=new MemorySave();var store=new EconomyStore(save);var flow=new GameFlowService();var events=new EventBus();var config=ScriptableObject.CreateInstance<FlowRewardConfig>();
            using(var quest=new QuestService(store,new[]{new ObjectiveDefinition { target=3 }},events))
            using(var achievement=new AchievementService(store,new[]{new ObjectiveDefinition { target=3 }},events))
            try { using(var bridge=new FlowEconomyBridge(flow,store,events,config)) {
                await flow.HomeAsync();await flow.PlayAsync();save.failAt=save.writes+4;flow.Win();Assert.IsTrue(bridge.HasPending);Assert.AreEqual(1,quest.Statuses[0].progress);Assert.AreEqual(1,achievement.Statuses[0].progress);
                save.failAt=-1;Assert.IsTrue(bridge.RetryPending());Assert.AreEqual(100,store.Balance("coin"));Assert.AreEqual(1,quest.Statuses[0].progress);Assert.AreEqual(1,achievement.Statuses[0].progress);
            } } finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public async Task OutcomeObserversCannotLeaveBeforeAllResultHandlersFinish()
        {
            var flow=new GameFlowService();bool left=true;flow.Completed+=outcome=>left=flow.NextAsync().GetAwaiter().GetResult();
            await flow.HomeAsync();await flow.PlayAsync();flow.Win();Assert.IsFalse(left);Assert.AreEqual(GamePhase.Won,flow.Phase);Assert.IsTrue(await flow.NextAsync());
        }
        [Test] public async Task LostRunDoesNotReceiveDefaultWinReward()
        {
            var flow=new GameFlowService();var store=new EconomyStore(new MemorySave());var config=ScriptableObject.CreateInstance<FlowRewardConfig>();
            try { using(var bridge=new FlowEconomyBridge(flow,store,new EventBus(),config)) { await flow.HomeAsync();await flow.PlayAsync();flow.Lose();Assert.AreEqual(0,store.Balance("coin"));Assert.IsFalse(bridge.HasPending); } }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public async Task SimulatedAdsRequireConsentAndRewardOnlyWhenExplicitlyConfigured()
        {
            var consentProvider=ScriptableObject.CreateInstance<SimulatedConsentProvider>();var adsProvider=ScriptableObject.CreateInstance<SimulatedAdsProvider>();
            try { using(var consent=new ConsentService(new ConsentSettings(),consentProvider.Create))using(var ads=new AdsService(new AdsSettings { enabled=true },adsProvider.Create,consent)) {
                Assert.IsTrue(await ads.LoadAsync(AdFormat.Rewarded));Assert.AreEqual(AdResult.Closed,await ads.ShowAsync(AdFormat.Rewarded));Assert.IsFalse(ads.IsReady(AdFormat.Rewarded));
            }
            adsProvider.rewardedResult=AdResult.Rewarded;
            using(var consent=new ConsentService(new ConsentSettings(),consentProvider.Create))using(var ads=new AdsService(new AdsSettings { enabled=true },adsProvider.Create,consent)) { Assert.IsTrue(await ads.LoadAsync(AdFormat.Rewarded));Assert.AreEqual(AdResult.Rewarded,await ads.ShowAsync(AdFormat.Rewarded)); }
            consentProvider.allowRequests=false;
            using(var consent=new ConsentService(new ConsentSettings(),consentProvider.Create))using(var ads=new AdsService(new AdsSettings { enabled=true },adsProvider.Create,consent))Assert.IsFalse(await ads.LoadAsync(AdFormat.Rewarded));
            } finally { UnityEngine.Object.DestroyImmediate(consentProvider);UnityEngine.Object.DestroyImmediate(adsProvider); }
        }
        [Test] public async Task SimulatedFirebaseUsesDefaultsAndHonorsFeatureFlags()
        {
            var provider=ScriptableObject.CreateInstance<SimulatedFirebaseProvider>();provider.remoteValues=new[]{new RemoteDefault { key="level",value="3" }};
            try { using(var firebase=new FirebaseService(new FirebaseSettings { enabled=true,analytics=false,crashlytics=false,defaults=new[]{new RemoteDefault { key="level",value="1" }} },provider.Create)) {
                Assert.AreEqual(1,firebase.GetLong("level"));Assert.IsTrue(await firebase.InitializeAsync());Assert.IsFalse(firebase.LogEvent("play"));Assert.IsFalse(firebase.RecordException(new Exception()));Assert.IsTrue(await firebase.FetchRemoteConfigAsync());Assert.AreEqual(3,firebase.GetLong("level"));Assert.IsEmpty(provider.events);
            } } finally { UnityEngine.Object.DestroyImmediate(provider); }
        }
    }
}
