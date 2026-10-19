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
        public sealed class TestConsent : ConsentProvider
        {
            public readonly ConsentBackend backend = new ConsentBackend();
            public override IConsentBackend Create() => backend;
        }
        public sealed class ConsentBackend : IConsentBackend
        {
            public bool allowed = true, required;
            public int forms;
            public TaskCompletionSource<bool> pending;
            public bool CanRequestAds => allowed;
            public bool PrivacyOptionsRequired => required;
            public Task GatherAsync(ConsentSettings settings, CancellationToken token) => Task.CompletedTask;
            public Task ShowPrivacyOptionsAsync(CancellationToken token) { forms++; return pending?.Task ?? Task.CompletedTask; }
            public void ResetForTesting() { allowed = false; }
            public void Dispose() { }
        }
        public sealed class TestAssets : AssetProvider
        {
            public readonly AssetBackend backend = new AssetBackend();
            public override IAssetBackend Create() => backend;
        }
        public sealed class AssetHandle : IAssetHandle
        {
            public GameObject prefab; public int releases;
            public UnityEngine.Object Asset => prefab;
            public void Dispose() { releases++; }
        }
        public sealed class AssetBackend : IAssetBackend
        {
            public int loads; public TaskCompletionSource<IAssetHandle> pending;
            public Task<IAssetHandle> LoadAsync(string address, CancellationToken cancellation) { loads++; return pending.Task; }
            public void Dispose() { }
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
        private TestConsent consentProvider;
        private TestAssets assetProvider;
        private GameObject assetTemplate;
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
            consentProvider = ScriptableObject.CreateInstance<TestConsent>(); features.consentProvider = consentProvider;
            assetProvider = ScriptableObject.CreateInstance<TestAssets>(); features.assets.enabled = true; features.assetProvider = assetProvider;
            features.tutorial.enabled = true; features.tutorial.saveSlot = slot + "_tutorial";
            features.tutorial.tutorials = new[] { new TutorialDefinition { id = "intro", steps = new[] {
                new TutorialStep { id = "play", target = "home.play", message = "Play", waitForTargetAction = true },
                new TutorialStep { id = "collect", target = "gameplay.collect", message = "Collect", waitForTargetAction = true } } } };
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
        [UnityTest] public IEnumerator UIEffectsAreBoundedAndRewardDoesNotRepeatOnRefresh()
        {
            var effects = flow.GetComponent<GameFlowUIFeedback>(); Assert.IsNotNull(effects);
            Assert.AreEqual(0, effects.ActiveParticles);
            Assert.IsTrue(flow.Features.Offline.AddCurrency(Currency.Coin, 10));
            Assert.AreEqual(8, effects.ActiveParticles);
            for (int i = 0; i < 20; i++) flow.Services.Get<FeatureServices>().UI.Play(UICue.Reward);
            Assert.LessOrEqual(effects.ActiveParticles, 32);
            UnityEngine.Time.timeScale = 0; yield return new WaitForSecondsRealtime(1);
            Assert.AreEqual(0, effects.ActiveParticles); yield return null; Assert.AreEqual(0, effects.ActiveParticles);
            UnityEngine.Time.timeScale = 1;
        }
        [UnityTest] public IEnumerator UIButtonFeedbackRestoresAuthoredScaleAndAvoidsDuplicatePointerSound()
        {
            var node = new GameObject("Feedback test button", typeof(RectTransform), typeof(Image), typeof(Button)); node.transform.SetParent(flow.transform, false);
            Vector3 authored = new Vector3(1.2f, .8f, 1); node.transform.localScale = authored;
            var button = node.GetComponent<Button>(); var feedback = node.AddComponent<UIButtonFeedback>(); feedback.Bind(); feedback.Bind();
            var service = flow.Services.Get<FeatureServices>().UI; int clicks = 0;
            Action<UICue, Vector2?> listener = (cue, position) => { if (cue == UICue.Click) clicks++; }; service.Played += listener;
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            feedback.OnPointerDown(pointer); button.onClick.Invoke(); Assert.AreEqual(1, clicks);
            UnityEngine.Time.timeScale = 0; yield return new WaitForSecondsRealtime(.15f);
            Assert.Less(node.transform.localScale.x, authored.x);
            feedback.OnPointerUp(pointer); node.SetActive(false); Assert.AreEqual(authored, node.transform.localScale);
            node.SetActive(true); button.interactable = false; feedback.OnPointerDown(pointer); button.onClick.Invoke(); Assert.AreEqual(1, clicks);
            service.Played -= listener; UnityEngine.Object.Destroy(node); UnityEngine.Time.timeScale = 1;
        }
        [UnityTest] public IEnumerator UIValueFeedbackIgnoresSilentUpdatesAndDisabledControls()
        {
            var node = new GameObject("Feedback slider", typeof(RectTransform), typeof(Slider)); node.transform.SetParent(flow.transform, false);
            var slider = node.GetComponent<Slider>(); var feedback = node.AddComponent<UIValueFeedback>(); feedback.Bind(); feedback.Bind();
            int ticks = 0; var service = flow.Services.Get<FeatureServices>().UI;
            Action<UICue, Vector2?> listener = (cue, position) => { if (cue == UICue.Change) ticks++; }; service.Played += listener;
            slider.SetValueWithoutNotify(.5f); Assert.AreEqual(0, ticks);
            slider.value = .6f; slider.value = .7f; Assert.AreEqual(1, ticks);
            yield return new WaitForSecondsRealtime(.1f); slider.value = .8f; Assert.AreEqual(2, ticks);
            slider.interactable = false; slider.value = .9f; Assert.AreEqual(2, ticks);
            service.Played -= listener; UnityEngine.Object.Destroy(node);
        }
#if UNITY_EDITOR
        private GameFlowHub AddHub()
        {
            config.homeHubPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<HomeHubView>("Assets/BaseUnityGameFlow/HomeHub/HomeHub.prefab");
            Assert.IsNotNull(config.homeHubPrefab, "Generate Missing Home Store Quest Prefabs before running sample tests.");
            config.homeHubCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<HomeHubCatalog>("Assets/BaseUnityGameFlow/HomeHub/Presentation.asset");
            var hub = flow.gameObject.AddComponent<GameFlowHub>(); hub.Initialize(flow); return hub;
        }
        [UnityTest] public IEnumerator HubStoreConfirmsAndPreventsRepeatedPurchase()
        {
            var hub = AddHub(); yield return null;
            Assert.IsTrue(hub.View.dailyDot.activeSelf); Assert.AreEqual("200 Coin    0 Gem", hub.View.wallet.text);
            hub.Open("STORE"); yield return null;
            var buy = hub.View.rows.GetComponentsInChildren<Button>().Single(button => button.name == "Buy_energy");
            buy.onClick.Invoke(); Assert.IsTrue(hub.View.confirmation.activeSelf); Assert.AreEqual(200, flow.Features.Offline.Coins);
            hub.View.confirmCancel.onClick.Invoke(); Assert.AreEqual(200, flow.Features.Offline.Coins);
            buy.onClick.Invoke(); hub.View.confirmAccept.onClick.Invoke(); hub.View.confirmAccept.onClick.Invoke(); yield return null;
            Assert.AreEqual(150, flow.Features.Offline.Coins); Assert.AreEqual(1, flow.Features.Offline.ItemCount("energy"));
            Assert.IsFalse(hub.View.iapTab.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator HubQuestBadgeClaimsAndHidesDisabledModules()
        {
            var hub = AddHub(); yield return null; Assert.IsFalse(hub.View.questDot.activeSelf);
            flow.Play(); yield return WaitFor(FlowPhase.Playing); flow.WinLevel(); yield return WaitFor(FlowPhase.Result);
            flow.GoHome(); yield return WaitFor(FlowPhase.Home); yield return null;
            Assert.IsTrue(hub.View.questDot.activeSelf);
            hub.Open("QUESTS"); yield return null; hub.View.achievementTab.onClick.Invoke(); yield return null;
            Assert.IsFalse(hub.View.questPage.restore.gameObject.activeSelf); Assert.IsFalse(hub.View.questPage.retryPending.gameObject.activeSelf);
            var claim = hub.View.rows.GetComponentsInChildren<Button>().Single(button => button.name == "Claim_first_win");
            Assert.IsTrue(claim.interactable); long coins = flow.Features.Offline.Coins;
            claim.onClick.Invoke(); claim.onClick.Invoke(); yield return null;
            Assert.AreEqual(coins + 200, flow.Features.Offline.Coins); Assert.IsFalse(hub.View.questDot.activeSelf);
            hub.Close(); yield return null;
            Assert.IsTrue(hub.View.home.activeSelf); Assert.IsFalse(hub.View.modal.activeSelf);
        }
        [UnityTest] public IEnumerator HubPurchaseLimitAndInsufficientFundsReflectService()
        {
            var hub = AddHub(); var economy = flow.Features.Offline;
            Assert.IsTrue(economy.Spend(Currency.Coin, 200)); hub.Open("STORE"); yield return null;
            var buy = hub.View.rows.GetComponentsInChildren<Button>().Single(button => button.name == "Buy_energy");
            buy.onClick.Invoke(); yield return null;
            Assert.IsFalse(hub.View.confirmation.activeSelf); Assert.AreEqual(0, economy.ItemCount("energy"));
            StringAssert.Contains("Not enough currency", hub.View.message.text);
            hub.Close(); flow.GetComponent<GameFlowUI>().OpenSettings(); yield return null;
            Assert.IsFalse(hub.View.gameObject.activeSelf);
        }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [UnityTest] public IEnumerator DebugPanelOwnsOnlyItsPauseAndReportsServices()
        {
            var debug = flow.GetComponent<GameFlowDebugPanel>(); Assert.IsNotNull(debug);
            Assert.IsFalse(debug.IsOpen);
            flow.Play(); yield return WaitFor(FlowPhase.Playing);
            debug.Toggle(); yield return null;
            Assert.IsTrue(debug.IsOpen); Assert.AreEqual(FlowPhase.Paused, flow.Phase);
            StringAssert.Contains("Firebase:", debug.Snapshot); StringAssert.Contains("leases=", debug.Snapshot);
            debug.Close(); Assert.AreEqual(FlowPhase.Playing, flow.Phase);
            flow.Pause(); debug.Toggle(); debug.Close();
            Assert.AreEqual(FlowPhase.Paused, flow.Phase);
            flow.Resume(); flow.LoseLevel(); yield return WaitFor(FlowPhase.Result);
        }
        [UnityTest] public IEnumerator DebugWinUsesRealResultAndProgressionFlow()
        {
            flow.Play(); yield return WaitFor(FlowPhase.Playing);
            var debug = flow.GetComponent<GameFlowDebugPanel>(); debug.Toggle(); yield return null;
            var button = debug.GetComponentsInChildren<Button>().Single(item => item.name == "WIN");
            Assert.IsTrue(button.interactable); button.onClick.Invoke();
            yield return WaitFor(FlowPhase.Result);
            Assert.AreEqual(LevelOutcome.Win, flow.Outcome); Assert.IsFalse(debug.IsOpen);
            Assert.IsTrue(flow.CanNextLevel);
        }
#endif
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
        [UnityTest] public IEnumerator ConsentDenialBlocksBonusAndStillAllowsGameplay()
        {
            consentProvider.backend.allowed = false;
            var consent = flow.Services.Get<ConsentService>(); var retry = consent.RetryAsync(); while (!retry.IsCompleted) yield return null;
            flow.Play(); yield return WaitFor(FlowPhase.Playing); flow.WinLevel(); yield return WaitFor(FlowPhase.Result);
            var reward = flow.Features.RewardDouble(); while (!reward.IsCompleted) yield return null;
            Assert.AreEqual(350, flow.Features.Offline.Coins); Assert.IsTrue(flow.Features.Offline.CanDouble);
            Assert.IsFalse(consent.CanRequestAds); flow.NextLevel(); yield return WaitFor(FlowPhase.Playing);
        }
        [UnityTest] public IEnumerator FallbackSettingsShowsPrivacyAndPreventsDuplicateForm()
        {
            var consent = flow.Services.Get<ConsentService>(); consentProvider.backend.required = true;
            var retry = consent.RetryAsync(); while (!retry.IsCompleted) yield return null;
            Click("Settings"); yield return null;
            consentProvider.backend.pending = new TaskCompletionSource<bool>(); Click("Privacy options"); yield return null;
            Assert.IsTrue(consent.IsBusy); Assert.AreEqual(1, consentProvider.backend.forms);
            var button = flow.GetComponentsInChildren<Button>().Single(x => x.name == "Privacy options");
            Assert.IsFalse(button.interactable);
            consentProvider.backend.pending.SetResult(true); yield return null; yield return null;
            Assert.IsFalse(consent.IsBusy);
            // Leave Settings open to cover disposal while its consent listener is subscribed.
        }
        [UnityTest] public IEnumerator PrefabPrivacyButtonHidesWhenNotRequired()
        {
            var parent = new GameObject("Privacy bindings test"); parent.transform.SetParent(flow.transform);
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(Button)); template.transform.SetParent(parent.transform);
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)); text.transform.SetParent(template.transform);
            var consent = flow.Services.Get<ConsentService>();
            GameFlowPrivacyButton.AddToSettings(template.GetComponent<Button>(), consent, null);
            var privacy = parent.GetComponentInChildren<GameFlowPrivacyButton>(true); Assert.IsFalse(privacy.gameObject.activeSelf);
            consentProvider.backend.required = true; var retry = consent.RetryAsync(); while (!retry.IsCompleted) yield return null;
            Assert.IsTrue(privacy.gameObject.activeSelf);
            consentProvider.backend.pending = new TaskCompletionSource<bool>(); privacy.GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(privacy.GetComponent<Button>().interactable);
            consentProvider.backend.required = false; consentProvider.backend.pending.SetResult(true); yield return null; yield return null;
            Assert.IsFalse(privacy.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator SharedConfirmationPausesAndResumesGameplay()
        {
            flow.Play(); yield return WaitFor(FlowPhase.Playing);
            var service = flow.Services.Get<FeedbackService>(); var confirm = service.ConfirmAsync("Continue?", "Confirm once", "test_confirm");
            yield return null; yield return null; Assert.AreEqual(FlowPhase.Paused, flow.Phase);
            Click("OK"); yield return null; yield return null; Assert.IsTrue(confirm.IsCompleted); Assert.IsTrue(confirm.Result); Assert.AreEqual(FlowPhase.Playing, flow.Phase);
            var a = service.Loading("one"); var b = service.Loading("two"); a.Dispose(); Assert.IsTrue(service.IsLoading); b.Dispose(); Assert.IsFalse(service.IsLoading);
        }
        [UnityTest] public IEnumerator TutorialAdvancesThroughRealGameButtons()
        {
            var tutorial = flow.Services.Get<TutorialService>(); Assert.IsTrue(tutorial.StartNext()); yield return null;
            Click("Play"); yield return WaitFor(FlowPhase.Playing); Assert.AreEqual("collect", tutorial.Current.id);
            Click("Collect +10"); yield return WaitFor(FlowPhase.Result); Assert.IsTrue(tutorial.IsCompleted("intro")); Assert.IsNull(tutorial.Current);
        }
        [UnityTest] public IEnumerator AddressableGameplayHoldsAndReleasesItsAssetLease()
        {
            assetTemplate = new GameObject("Addressable gameplay template"); assetTemplate.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(assetTemplate);
            var handle = new AssetHandle { prefab = assetTemplate }; assetProvider.backend.pending = new TaskCompletionSource<IAssetHandle>();
            config.levels[0].addressablePrefab = "gameplay"; flow.Play();
            float until = UnityEngine.Time.realtimeSinceStartup + 10;
            while (assetProvider.backend.loads == 0 && UnityEngine.Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(1, assetProvider.backend.loads); Assert.AreEqual(FlowPhase.Loading, flow.Phase);
            assetProvider.backend.pending.SetResult(handle); yield return WaitFor(FlowPhase.Playing);
            Assert.IsNotNull(flow.Levels.Instance); Assert.AreEqual(1, flow.Services.Get<AssetService>().ActiveLeases);
            flow.WinLevel(); yield return WaitFor(FlowPhase.Result); flow.GoHome(); yield return WaitFor(FlowPhase.Home);
            Assert.AreEqual(0, flow.Services.Get<AssetService>().ActiveLeases); Assert.AreEqual(1, handle.releases);
        }
        [UnityTest] public IEnumerator PurchasedRemoveAdsEntitlementUpdatesAdsPolicyImmediately()
        {
            var ads = flow.Services.Get<AdsService>(); Assert.IsFalse(ads.NoAds);
            Assert.IsTrue(flow.Features.Offline.ApplyStorePurchase("verified_test_remove_ads", new StoreProduct { id = "remove_ads", kind = StoreProductKind.NonConsumable, entitlement = "remove_ads" }));
            Assert.IsTrue(ads.NoAds); Assert.IsFalse(ads.IsReady(AdFormat.Interstitial));
            var load = ads.LoadAsync(AdFormat.Rewarded); while (!load.IsCompleted) yield return null; Assert.IsTrue(load.Result);
        }
        [UnityTest] public IEnumerator UnifiedResultSurvivesRestartAndDoesNotWriteLegacyProgress()
        {
            flow.Play(); yield return WaitFor(FlowPhase.Playing);
            config.saveSlot = "invalid/legacy_slot"; // Once migrated, the old slot is no longer used.
            flow.WinLevel(); yield return WaitFor(FlowPhase.Result);
            Assert.IsFalse(flow.Features.PendingSave); Assert.AreEqual(350, flow.Features.Offline.Coins);
            UnityEngine.Object.Destroy(flow.gameObject); yield return null;
            var root = new GameObject("Restored bootstrap"); root.SetActive(false);
            flow = root.AddComponent<GameFlowBootstrap>(); flow.Config = config; root.SetActive(true);
            yield return WaitFor(FlowPhase.Result);
            Assert.AreEqual(1, flow.CurrentLevel); Assert.AreEqual(LevelOutcome.Win, flow.Outcome);
            Assert.AreEqual(350, flow.Features.Offline.Coins); Assert.AreEqual(2, flow.HomeLevel);
            flow.NextLevel(); yield return WaitFor(FlowPhase.Playing); Assert.AreEqual(2, flow.CurrentLevel);
        }
        [UnityTest] public IEnumerator EditablePrefabMenuKeepsInstanceAcrossTransactions()
        {
#if UNITY_EDITOR
            var ids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BaseUnityGameFlow/OfflineUI" });
            var prefab = ids.Select(id => UnityEditor.AssetDatabase.LoadAssetAtPath<OfflineMenuView>(UnityEditor.AssetDatabase.GUIDToAssetPath(id))).FirstOrDefault(x => x);
            Assert.IsNotNull(prefab, "Generate Missing Offline UI Prefabs before running this sample test.");
            config.offlineMenuPrefab = prefab;
            Click("MENU"); yield return new WaitForSecondsRealtime(.25f);
            var menu = flow.GetComponentInChildren<OfflineMenuView>(); Assert.IsNotNull(menu);
            Assert.IsTrue(menu.tabs.Single(x => x.key == "SHOP").selected.activeSelf);
            Click("BUY x1 • 50 Coin"); yield return null; yield return null; yield return null;
            Assert.AreEqual(150, flow.Features.Offline.Coins); Assert.AreSame(menu, flow.GetComponentInChildren<OfflineMenuView>());
            Assert.IsFalse(menu.loading.activeSelf); Assert.AreEqual("Saved", menu.message.text);
            menu.tabs.Single(x => x.key == "INVENTORY").button.onClick.Invoke(); yield return null;
            Assert.IsTrue(menu.tabs.Single(x => x.key == "INVENTORY").panel.gameObject.activeSelf);
            UnityEngine.Time.timeScale = 0; menu.close.onClick.Invoke(); yield return new WaitForSecondsRealtime(.25f);
            Assert.IsNull(flow.GetComponentInChildren<OfflineMenuView>());
#else
            yield break;
#endif
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (flow)
            {
                var saves = flow.Services.Get<ISaveStore>();
                if (hadLanguage) saves.Save("baseunity_language", savedLanguage); else saves.Delete("baseunity_language");
                saves.Delete(slot); saves.Delete(slot + "_flow"); saves.Delete(slot + "_tutorial");
                UnityEngine.Object.Destroy(flow.gameObject);
            }
            UnityEngine.Time.timeScale = 1; yield return null;
            UnityEngine.Object.Destroy(config); UnityEngine.Object.Destroy(features); UnityEngine.Object.Destroy(provider); UnityEngine.Object.Destroy(consentProvider); UnityEngine.Object.Destroy(assetProvider); if (assetTemplate) UnityEngine.Object.Destroy(assetTemplate);
        }
    }
}
