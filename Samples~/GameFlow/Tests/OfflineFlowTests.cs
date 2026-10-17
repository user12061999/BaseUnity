using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Core;
using BaseUnity.Features;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BaseUnity.GameFlow.Tests
{
    public sealed class OfflineFlowTests
    {
        public sealed class TestAds : AdsProvider
        {
            public readonly Backend backend = new Backend();
            public override IAdsBackend Create() => backend;
        }
        public sealed class Backend : IAdsBackend
        {
            public AdResult result = AdResult.Closed;
            public TaskCompletionSource<AdResult> pending;
            public Task InitializeAsync(AdsSettings settings, CancellationToken token) => Task.CompletedTask;
            public Task<bool> LoadAsync(AdFormat format, CancellationToken token) => Task.FromResult(true);
            public bool IsReady(AdFormat format) => true;
            public Task<AdResult> ShowAsync(AdFormat format, CancellationToken token) => pending == null ? Task.FromResult(result) : pending.Task;
            public void HideBanner() { }
            public void Dispose() { }
        }
        private GameFlowBootstrap flow;
        private GameFlowConfig config;
        private FeatureConfig features;
        private TestAds provider;
        private string slot;
        private bool hadLanguage;
        private LocalizationService.LanguageSave savedLanguage;
        private IEnumerator WaitFor(FlowPhase phase)
        {
            float until = UnityEngine.Time.realtimeSinceStartup + 15;
            while (flow.Phase != phase && UnityEngine.Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(phase, flow.Phase, flow.ErrorMessage);
            yield return null;
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            Assert.IsNull(GameBootstrap.Instance);
            slot = "offline_test_" + Guid.NewGuid().ToString("N");
            features = ScriptableObject.CreateInstance<FeatureConfig>(); features.offline.enabled = true; features.offline.saveSlot = slot;
            features.offline.startingCoins = 200; features.localization.enabled = true;
            provider = ScriptableObject.CreateInstance<TestAds>(); features.adsProvider = provider; features.ads.enabled = true;
            config = ScriptableObject.CreateInstance<GameFlowConfig>(); config.features = features; config.saveSlot = slot + "_flow";
            config.levels = new[] { new GameLevel { targetScore = 10, timeLimitSeconds = 0 }, new GameLevel { targetScore = 10, timeLimitSeconds = 0 } };
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.EndsWith("/Home.unity")) config.homeScene = path;
                if (path.EndsWith("/InGame.unity")) config.inGameScene = path;
            }
            var root = new GameObject("Offline test bootstrap"); root.SetActive(false);
            flow = root.AddComponent<GameFlowBootstrap>(); flow.Config = config; root.SetActive(true);
            yield return WaitFor(FlowPhase.Home);
            hadLanguage = flow.Services.Get<ISaveStore>().TryLoad("baseunity_language", out savedLanguage);
            flow.Features.Language.SetLanguage("en");
            yield return null;
        }
        private void Click(string name)
        {
            var button = flow.GetComponentsInChildren<Button>().Single(x => x.name == name);
            Assert.IsTrue(button.interactable); button.onClick.Invoke();
        }
        [UnityTest] public IEnumerator MenuPurchasesDailyAndLanguageThenWinNextLose()
        {
            var economy = flow.Features.Offline;
            Click("MENU"); yield return null;
            Click("BUY x1 • 50 Coin"); yield return null;
            Assert.AreEqual(150, economy.Coins); Assert.AreEqual(1, economy.ItemCount("energy"));
            Click("DAILY REWARD"); yield return null; Click("CLAIM"); yield return null;
            Assert.AreEqual(200, economy.Coins); Assert.IsFalse(economy.CanClaimDaily);
            Click("Tiếng Việt"); yield return null;
            Assert.AreEqual("vi", flow.Features.Language.Language);
            // Restore user's global language preference after the assertion.
            flow.Features.Language.SetLanguage("en");
            Click("CLOSE"); yield return null;
            flow.Play(); yield return WaitFor(FlowPhase.Playing); flow.AddScore(10); yield return WaitFor(FlowPhase.Result);
            Assert.AreEqual(350, economy.Coins); Assert.AreEqual(150, economy.LastReward);
            flow.NextLevel(); yield return WaitFor(FlowPhase.Playing); Assert.AreEqual(2, flow.CurrentLevel);
            flow.LoseLevel(); yield return WaitFor(FlowPhase.Result); Assert.AreEqual(350, economy.Coins);
            Assert.AreEqual(2, economy.MissionProgress(economy.Settings.dailyMissions[0]));
        }
        [UnityTest] public IEnumerator RewardedRequiresVerifiedCallbackAndBlocksTransition()
        {
            flow.Play(); yield return WaitFor(FlowPhase.Playing); flow.WinLevel(); yield return WaitFor(FlowPhase.Result);
            var economy = flow.Features.Offline;
            var close = flow.Features.RewardDouble(); while (!close.IsCompleted) yield return null;
            Assert.AreEqual(350, economy.Coins); Assert.IsTrue(economy.CanDouble);
            provider.backend.pending = new TaskCompletionSource<AdResult>();
            var reward = flow.Features.RewardDouble(); Assert.IsTrue(flow.Features.Busy);
            flow.GoHome(); flow.NextLevel(); Assert.AreEqual(FlowPhase.Result, flow.Phase);
            provider.backend.pending.SetResult(AdResult.Rewarded); while (!reward.IsCompleted) yield return null;
            Assert.AreEqual(500, economy.Coins); Assert.IsFalse(economy.CanDouble);
            var duplicate = flow.Features.RewardDouble(); while (!duplicate.IsCompleted) yield return null;
            Assert.AreEqual(500, economy.Coins);
        }
        [UnityTest] public IEnumerator FailedProgressSaveStaysOnResultAndRetryDoesNotDuplicateReward()
        {
            flow.Play(); yield return WaitFor(FlowPhase.Playing);
            string original = config.saveSlot; config.saveSlot = "invalid/slot";
            flow.WinLevel(); yield return WaitFor(FlowPhase.Result);
            Assert.IsTrue(flow.Features.PendingSave); Assert.AreEqual(350, flow.Features.Offline.Coins);
            flow.NextLevel(); Assert.AreEqual(FlowPhase.Result, flow.Phase);
            config.saveSlot = original; Assert.IsTrue(flow.Features.SaveResult());
            Assert.AreEqual(350, flow.Features.Offline.Coins);
            flow.NextLevel(); yield return WaitFor(FlowPhase.Playing); Assert.AreEqual(2, flow.CurrentLevel);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (flow)
            {
                var saves = flow.Services.Get<ISaveStore>();
                if (hadLanguage) saves.Save("baseunity_language", savedLanguage); else saves.Delete("baseunity_language");
                saves.Delete(slot); saves.Delete(slot + "_flow");
                UnityEngine.Object.Destroy(flow.gameObject);
            }
            UnityEngine.Time.timeScale = 1; yield return null;
            UnityEngine.Object.Destroy(config); UnityEngine.Object.Destroy(features); UnityEngine.Object.Destroy(provider);
        }
    }
}
