using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using BaseUnity.Save;
using BaseUnity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Tests
{
    public sealed class MobileModuleTests
    {
        private sealed class MemoryStore : ISaveStore
        {
            public bool fail; public int writes;
            private readonly Dictionary<string, string> data = new Dictionary<string, string>();
            public void Save<T>(string id, T value, int version = 1) where T : class { if (fail) throw new System.IO.IOException("Disk full"); data[id] = JsonUtility.ToJson(value); writes++; }
            public bool TryLoad<T>(string id, out T value, int version = 1) where T : class { value = data.TryGetValue(id, out var json) ? JsonUtility.FromJson<T>(json) : null; return value != null; }
            public void Delete(string id) { data.Remove(id); }
        }
        private sealed class Handle : IAssetHandle { public UnityEngine.Object asset; public int disposed; public UnityEngine.Object Asset => asset; public void Dispose() { disposed++; } }
        private sealed class Assets : IAssetBackend
        {
            public Handle handle; public TaskCompletionSource<IAssetHandle> pending;
            public Task<IAssetHandle> LoadAsync(string address, CancellationToken token) => pending?.Task ?? Task.FromResult<IAssetHandle>(handle);
            public void Dispose() { }
        }
        private sealed class Haptic : IHapticBackend { public int plays; public bool Play(HapticKind kind) { plays++; return true; } public void Dispose() { } }
        private sealed class Notifications : ILocalNotificationBackend
        {
            public NotificationPermission permission = NotificationPermission.Granted; public int requests, scheduled;
            public List<int> cancelled = new List<int>(); public TaskCompletionSource<NotificationPermission> pending;
            public NotificationPermission Permission => permission;
            public void Initialize(LocalNotificationSettings settings) { }
            public Task<NotificationPermission> RequestPermissionAsync(CancellationToken token) { requests++; return pending?.Task ?? Task.FromResult(permission); }
            public void Schedule(LocalNotification value, LocalNotificationSettings settings) { scheduled++; }
            public void Cancel(int id) { cancelled.Add(id); }
            public void Dispose() { }
        }
        private sealed class Store : IPurchaseBackend
        {
            public int confirms, buys; public bool confirmFail, initFail; public Action onConfirm, onRestore;
            public TaskCompletionSource<bool> pendingInit;
            public event Action<StorePurchase> Pending; public event Action<string> Failed;
            public IReadOnlyList<StoreProductInfo> Products => new[] { new StoreProductInfo { id = "coins", available = true }, new StoreProductInfo { id = "noads", available = true } };
            public Task InitializeAsync(PurchaseSettings settings, CancellationToken token) => initFail ? Task.FromException(new InvalidOperationException("Offline")) : pendingInit?.Task ?? Task.CompletedTask;
            public void Purchase(string id) { buys++; }
            public void Confirm(StorePurchase purchase) { if (confirmFail) throw new InvalidOperationException("Store offline"); onConfirm?.Invoke(); confirms++; }
            public Task<bool> RestoreAsync(CancellationToken token) { onRestore?.Invoke(); return Task.FromResult(true); }
            public void Emit(StorePurchase purchase) { Pending?.Invoke(purchase); }
            public void Fail(string error) { Failed?.Invoke(error); }
            public void Dispose() { }
        }
        private sealed class Verifier : IPurchaseVerifier
        {
            public bool valid = true; public int calls; public TaskCompletionSource<bool> pending;
            public Task<bool> VerifyAsync(StorePurchase purchase, CancellationToken token) { calls++; return pending?.Task ?? Task.FromResult(valid); }
            public void Dispose() { }
        }
        private static OfflineService Wallet(MemoryStore saves) => new OfflineService(new OfflineSettings { enabled = true }, saves);
        private static StoreProduct Coins => new StoreProduct { id = "coins", androidId = "coins", iosId = "coins", coins = 100 };
        private static PurchaseSettings Catalog => new PurchaseSettings { enabled = true, products = new[] { Coins, new StoreProduct { id = "noads", kind = StoreProductKind.NonConsumable, entitlement = "remove_ads", coins = 10 } } };
        private static StorePurchase Receipt(string id = "tx1", string product = "coins") => new StorePurchase { transactionId = id, productId = product, receipt = "store-signed-test-receipt" };
        private static TutorialSettings Tutorial => new TutorialSettings { enabled = true, tutorials = new[] { new TutorialDefinition { id = "intro", steps = new[] { new TutorialStep { id = "play", target = "play", waitForTargetAction = true }, new TutorialStep { id = "done" } } } } };
        [Test] public async Task PopupPriorityIsStableAndDuplicateRequestsShareCompletion()
        {
            using (var queue = new PopupService(new PopupSettings())) using (queue.AttachPresenter())
            {
                var a = queue.Enqueue("a", new PopupContent()); var b = queue.Enqueue("b", new PopupContent(), 5); var c = queue.Enqueue("c", new PopupContent(), 5);
                Assert.AreSame(b, queue.Enqueue("b", new PopupContent(), 99)); Assert.AreEqual("a", queue.Active.Id);
                queue.Resolve("a", true); Assert.AreEqual("b", queue.Active.Id); queue.Resolve("b", false); Assert.AreEqual("c", queue.Active.Id); queue.Resolve("c", true);
                Assert.AreEqual(PopupResult.Accepted, await a); Assert.AreEqual(PopupResult.Dismissed, await b); Assert.AreEqual(PopupResult.Accepted, await c);
            }
        }
        [Test] public async Task PopupSuspensionCapacityAndSceneCancellationAreSafe()
        {
            using (var queue = new PopupService(new PopupSettings { capacity = 1 })) using (queue.AttachPresenter())
            {
                var task = queue.Enqueue("a", new PopupContent(), scope: "scene");
                using (queue.Suspend()) { Assert.IsFalse(queue.Resolve("a", true)); Assert.AreEqual(PopupResult.Unavailable, await queue.Enqueue("b", new PopupContent())); }
                queue.CancelScope("scene"); Assert.AreEqual(PopupResult.Cancelled, await task); Assert.IsNull(queue.Active);
            }
        }
        [Test] public async Task PopupCancellationAndPresenterLossCompleteTasks()
        {
            using (var queue = new PopupService(new PopupSettings())) using (var cancel = new CancellationTokenSource())
            {
                var presenter = queue.AttachPresenter(); var a = queue.Enqueue("a", new PopupContent(), cancellation: cancel.Token); cancel.Cancel();
                Assert.AreEqual(PopupResult.Cancelled, await a); var b = queue.Enqueue("b", new PopupContent()); presenter.Dispose(); Assert.AreEqual(PopupResult.Cancelled, await b);
                Assert.AreEqual(PopupResult.Unavailable, await queue.Enqueue("c", new PopupContent()));
            }
        }
        [Test] public async Task FeedbackLoadingScopesAndConfirmationDoNotCloseEachOther()
        {
            using (var popups = new PopupService(new PopupSettings())) using (popups.AttachPresenter()) using (var feedback = new FeedbackService(new FeedbackSettings(), popups))
            {
                var a = feedback.Loading("a"); var b = feedback.Loading("b"); a.Dispose(); Assert.IsTrue(feedback.IsLoading); Assert.AreEqual("b", feedback.LoadingMessage); b.Dispose(); b.Dispose(); Assert.IsFalse(feedback.IsLoading);
                var confirmed = feedback.ConfirmAsync("Title", "Message", "confirm"); popups.Resolve("confirm", true); Assert.IsTrue(await confirmed);
            }
        }
        [Test] public void TutorialResumesAndRejectsWrongActionOrFailedSave()
        {
            var save = new MemoryStore(); var tutorial = new TutorialService(Tutorial, save); Assert.IsTrue(tutorial.Start("intro"));
            Assert.IsFalse(tutorial.Advance("wrong")); save.fail = true; Assert.IsFalse(tutorial.Advance("play")); Assert.AreEqual("play", tutorial.Current.id);
            save.fail = false; Assert.IsTrue(tutorial.Advance("play")); tutorial = new TutorialService(Tutorial, save); Assert.IsTrue(tutorial.Start("intro")); Assert.AreEqual("done", tutorial.Current.id);
            Assert.IsTrue(tutorial.Advance()); Assert.IsTrue(tutorial.IsCompleted("intro")); Assert.IsFalse(tutorial.Start("intro"));
        }
        [Test] public void TutorialSkipResetAndFutureSaveAreDurable()
        {
            var save = new MemoryStore(); var tutorial = new TutorialService(Tutorial, save); tutorial.StartNext(); Assert.IsTrue(tutorial.Skip()); Assert.IsTrue(new TutorialService(Tutorial, save).IsCompleted("intro"));
            Assert.IsTrue(tutorial.Reset("intro")); Assert.IsTrue(tutorial.StartNext());
            save.Save("baseunity_tutorial", new TutorialService.Data { schema = 99 }); tutorial = new TutorialService(Tutorial, save); int writes = save.writes;
            Assert.IsNotNull(tutorial.LoadError); Assert.IsFalse(tutorial.StartNext()); Assert.AreEqual(writes, save.writes);
        }
        [Test] public async Task AssetCancellationReleasesLateNativeHandleExactlyOnce()
        {
            var asset = new TextAsset("asset"); var handle = new Handle { asset = asset }; var backend = new Assets { pending = new TaskCompletionSource<IAssetHandle>() };
            try
            {
                using (var assets = new AssetService(new AssetSettings { enabled = true }, () => backend)) using (var cancel = new CancellationTokenSource())
                { var task = assets.LoadAsync<TextAsset>("key", cancel.Token); cancel.Cancel(); Assert.IsNull(await task); backend.pending.SetResult(handle); await Task.Yield(); Assert.AreEqual(1, handle.disposed); Assert.AreEqual(0, assets.ActiveLeases); }
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public async Task AssetLeasesReleaseOnDisposeAndRejectWrongTypes()
        {
            var asset = new TextAsset("asset"); var handle = new Handle { asset = asset };
            try
            {
                var assets = new AssetService(new AssetSettings { enabled = true }, () => new Assets { handle = handle }); var lease = await assets.LoadAsync<TextAsset>("key"); Assert.AreSame(asset, lease.Asset); Assert.AreEqual(1, assets.ActiveLeases);
                assets.Dispose(); lease.Dispose(); Assert.AreEqual(1, handle.disposed); Assert.IsNull(lease.Asset);
                handle = new Handle { asset = asset }; using (assets = new AssetService(new AssetSettings { enabled = true }, () => new Assets { handle = handle })) Assert.IsNull(await assets.LoadAsync<GameObject>("wrong")); Assert.AreEqual(1, handle.disposed);
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public async Task DestroyedUnityAssetIsRejectedAndReleased()
        {
            var asset = new GameObject("Already unloaded"); UnityEngine.Object.DestroyImmediate(asset); var handle = new Handle { asset = asset };
            using (var assets = new AssetService(new AssetSettings { enabled = true }, () => new Assets { handle = handle }))
            { Assert.IsNull(await assets.LoadAsync<GameObject>("unloaded")); Assert.AreEqual(1, handle.disposed); Assert.AreEqual(0, assets.ActiveLeases); }
        }
        [Test] public async Task DisabledAssetsDoNotCreateProvider()
        { int created = 0; using (var assets = new AssetService(new AssetSettings(), () => { created++; return new Assets(); })) Assert.IsNull(await assets.LoadAsync<TextAsset>("key")); Assert.AreEqual(0, created); }
        [Test] public void HapticsRespectOptOutSaveFailureAndThrottle()
        {
            var save = new MemoryStore(); var backend = new Haptic();
            using (var haptic = new HapticService(new HapticSettings { enabled = true, minimumInterval = 60 }, save, () => backend))
            { Assert.IsTrue(haptic.Play()); Assert.IsFalse(haptic.Play()); Assert.AreEqual(1, backend.plays); save.fail = true; Assert.IsFalse(haptic.SetUserEnabled(false)); Assert.IsTrue(haptic.Enabled); save.fail = false; Assert.IsTrue(haptic.SetUserEnabled(false)); Assert.IsFalse(haptic.Play()); }
            using (var restored = new HapticService(new HapticSettings { enabled = true }, save)) Assert.IsFalse(restored.Enabled);
        }
        [Test] public async Task NotificationPermissionIsExplicitAndOptOutCancelsOwnedIdsOnly()
        {
            var save = new MemoryStore(); var backend = new Notifications();
            using (var service = new LocalNotificationService(new LocalNotificationSettings { enabled = true }, save, () => backend))
            {
                service.Initialize(); Assert.AreEqual(0, backend.requests); Assert.IsFalse(service.ScheduleDailyReminder(DateTime.UtcNow.AddDays(1)));
                Assert.IsTrue(await service.EnableAsync()); Assert.IsTrue(service.ScheduleDailyReminder(DateTime.UtcNow.AddDays(1))); Assert.IsTrue(service.Disable());
                CollectionAssert.AreEqual(new[] { 1001 }, backend.cancelled); Assert.IsFalse(service.OptedIn);
            }
        }
        [Test] public async Task NotificationDeniedOrFailedSaveNeverSchedules()
        {
            var save = new MemoryStore(); var backend = new Notifications { permission = NotificationPermission.Denied };
            using (var service = new LocalNotificationService(new LocalNotificationSettings { enabled = true }, save, () => backend))
            { Assert.IsFalse(await service.EnableAsync()); backend.permission = NotificationPermission.Granted; Assert.IsTrue(await service.EnableAsync()); save.fail = true; Assert.IsFalse(service.ScheduleDailyReminder(DateTime.UtcNow.AddDays(1))); Assert.AreEqual(0, backend.scheduled); }
        }
        [Test] public async Task NotificationOptOutCancelsPendingPermissionWithoutRevivingIt()
        {
            var backend = new Notifications { pending = new TaskCompletionSource<NotificationPermission>() };
            using (var service = new LocalNotificationService(new LocalNotificationSettings { enabled = true }, new MemoryStore(), () => backend))
            { var task = service.EnableAsync(); Assert.IsTrue(service.Disable()); backend.pending.SetResult(NotificationPermission.Granted); Assert.IsFalse(await task); Assert.IsFalse(service.OptedIn); }
        }
        [Test] public async Task DisabledNotificationModuleCleansUpItsSavedScheduleWithoutPrompt()
        {
            var save = new MemoryStore(); var backend = new Notifications(); save.Save("baseunity_notifications", new LocalNotificationService.Data { optedIn = true, owned = new List<int> { 4, 5 } });
            using (var service = new LocalNotificationService(new LocalNotificationSettings(), save, () => backend)) { service.Initialize(); CollectionAssert.AreEquivalent(new[] { 4, 5 }, backend.cancelled); Assert.AreEqual(0, backend.requests); Assert.IsFalse(await service.EnableAsync()); }
        }
        [Test] public async Task NotificationScheduleRequiresFutureUtcAndPositiveId()
        {
            var backend = new Notifications(); using (var service = new LocalNotificationService(new LocalNotificationSettings { enabled = true }, new MemoryStore(), () => backend))
            { await service.EnableAsync(); Assert.IsFalse(service.Schedule(new LocalNotification { id = -1, title = "x", fireUtc = DateTime.UtcNow.AddDays(1) })); Assert.IsFalse(service.ScheduleDailyReminder(DateTime.UtcNow.AddMinutes(-1))); Assert.IsFalse(service.ScheduleDailyReminder(DateTime.Now.AddDays(1))); Assert.AreEqual(0, backend.scheduled); }
        }
        [Test] public async Task PurchaseCommitsWalletAndReceiptBeforeStoreConfirmation()
        {
            var save = new MemoryStore(); var wallet = Wallet(save); var store = new Store(); var verifier = new Verifier();
            store.onConfirm = () => { var restored = Wallet(save); Assert.AreEqual(100, restored.Coins); Assert.IsTrue(restored.HasStoreTransaction("tx1", "coins")); };
            using (var purchases = new PurchaseService(Catalog, wallet, () => store, () => verifier)) { await purchases.InitializeAsync(); store.Emit(Receipt()); await purchases.RetryPendingAsync(); Assert.AreEqual(1, store.confirms); Assert.AreEqual(100, wallet.Coins); }
        }
        [Test] public async Task PurchaseRedeliveryAfterRestartNeverPaysTwice()
        {
            var save = new MemoryStore(); var wallet = Wallet(save); var store = new Store(); var verifier = new Verifier();
            using (var service = new PurchaseService(Catalog, wallet, () => store, () => verifier)) { await service.InitializeAsync(); store.Emit(Receipt()); await service.RetryPendingAsync(); }
            store = new Store(); verifier = new Verifier(); wallet = Wallet(save);
            using (var service = new PurchaseService(Catalog, wallet, () => store, () => verifier)) { await service.InitializeAsync(); store.Emit(Receipt()); await service.RetryPendingAsync(); Assert.AreEqual(100, wallet.Coins); Assert.AreEqual(0, verifier.calls); Assert.AreEqual(1, store.confirms); }
        }
        [Test] public async Task PurchaseSaveFailureRetainsPendingForSingleRetry()
        {
            var save = new MemoryStore(); var wallet = Wallet(save); var store = new Store();
            using (var service = new PurchaseService(Catalog, wallet, () => store, () => new Verifier()))
            {
                await service.InitializeAsync(); save.fail = true; store.Emit(Receipt()); await service.RetryPendingAsync(); Assert.AreEqual(0, wallet.Coins); Assert.AreEqual(0, store.confirms); Assert.AreEqual(1, service.PendingCount);
                save.fail = false; await service.RetryPendingAsync(); Assert.AreEqual(100, wallet.Coins); Assert.AreEqual(1, store.confirms); await service.RetryPendingAsync(); Assert.AreEqual(100, wallet.Coins);
            }
        }
        [Test] public async Task ConfirmationFailureRetriesWithoutRegrantOrReverification()
        {
            var save = new MemoryStore(); var wallet = Wallet(save); var store = new Store { confirmFail = true }; var verifier = new Verifier();
            using (var service = new PurchaseService(Catalog, wallet, () => store, () => verifier))
            {
                await service.InitializeAsync(); store.Emit(Receipt()); await service.RetryPendingAsync(); Assert.AreEqual(100, wallet.Coins); Assert.AreEqual(1, service.PendingCount); Assert.AreEqual(1, verifier.calls);
                store.confirmFail = false; await service.RetryPendingAsync(); Assert.AreEqual(1, store.confirms); Assert.AreEqual(100, wallet.Coins); Assert.AreEqual(1, verifier.calls);
            }
        }
        [Test] public async Task InvalidReceiptAndUnknownProductAreNeverGrantedOrConfirmed()
        {
            var save = new MemoryStore(); var wallet = Wallet(save); var store = new Store();
            using (var service = new PurchaseService(Catalog, wallet, () => store, () => new Verifier { valid = false }))
            { await service.InitializeAsync(); store.Emit(Receipt()); store.Emit(Receipt("tx2", "unknown")); await service.RetryPendingAsync(); Assert.AreEqual(0, wallet.Coins); Assert.AreEqual(0, store.confirms); Assert.AreEqual(2, service.PendingCount); }
        }
        [Test] public void NonConsumableRestoreWithNewTransactionDoesNotRepeatItsBonus()
        {
            var save = new MemoryStore(); var wallet = Wallet(save); var product = Catalog.products[1]; Assert.IsTrue(wallet.ApplyStorePurchase("original", product)); Assert.IsTrue(wallet.ApplyStorePurchase("restore", product));
            Assert.AreEqual(10, wallet.Coins); Assert.IsTrue(wallet.HasEntitlement("remove_ads")); Assert.IsTrue(Wallet(save).HasStoreProduct("noads")); Assert.IsFalse(wallet.ApplyStorePurchase("original", Coins));
        }
        [Test] public async Task RestoreWaitsForReceiptVerificationAndDurableEntitlement()
        {
            var wallet = Wallet(new MemoryStore()); var store = new Store(); var verifier = new Verifier { pending = new TaskCompletionSource<bool>() };
            store.onRestore = () => { var receipt = Receipt("restore", "noads"); receipt.requiresConfirmation = false; store.Emit(receipt); };
            using (var service = new PurchaseService(Catalog, wallet, () => store, () => verifier))
            {
                await service.InitializeAsync(); var restore = service.RestoreAsync(); Assert.IsFalse(restore.IsCompleted); Assert.IsTrue(service.IsBusy);
                verifier.pending.SetResult(true); Assert.IsTrue(await restore); Assert.IsTrue(wallet.HasEntitlement("remove_ads")); Assert.AreEqual(0, store.confirms);
            }
        }
        [Test] public async Task DisposedPurchaseServiceSuppressesLateVerification()
        {
            var wallet = Wallet(new MemoryStore()); var store = new Store(); var verifier = new Verifier { pending = new TaskCompletionSource<bool>() }; var service = new PurchaseService(Catalog, wallet, () => store, () => verifier);
            await service.InitializeAsync(); store.Emit(Receipt()); service.Dispose(); verifier.pending.SetResult(true); await Task.Yield(); Assert.AreEqual(0, wallet.Coins); Assert.AreEqual(0, store.confirms);
        }
        [Test] public async Task ConcurrentPurchaseRequestsOnlyOpenOneStoreFlow()
        {
            var store = new Store { pendingInit = new TaskCompletionSource<bool>() };
            using (var service = new PurchaseService(Catalog, Wallet(new MemoryStore()), () => store, () => new Verifier()))
            { var a = service.BuyAsync("coins"); var b = service.BuyAsync("coins"); store.pendingInit.SetResult(true); await Task.WhenAll(a, b); Assert.AreEqual(1, store.buys); store.Fail("cancelled"); Assert.IsFalse(service.IsBusy); }
        }
        [Test] public async Task FailedStoreConnectionCanRetryWhenNetworkReturns()
        {
            var failed = new Store { initFail = true }; var ready = new Store(); int created = 0;
            using (var service = new PurchaseService(Catalog, Wallet(new MemoryStore()), () => created++ == 0 ? failed : ready, () => new Verifier()))
            { Assert.IsFalse(await service.InitializeAsync()); Assert.IsTrue(await service.BuyAsync("coins")); Assert.AreEqual(1, ready.buys); Assert.AreEqual(2, created); }
        }
        [Test] public async Task MissingVerifierFailsClosedBeforeCreatingStore()
        { int created = 0; using (var service = new PurchaseService(Catalog, Wallet(new MemoryStore()), () => { created++; return new Store(); })) Assert.IsFalse(await service.InitializeAsync()); Assert.AreEqual(0, created); }
        [Test] public void WizardModuleSwitchesPreserveCatalogAndTutorialDefinitions()
        {
            var config = ScriptableObject.CreateInstance<FeatureConfig>();
            try
            {
                config.purchasing.products = new[] { Coins }; config.tutorial.tutorials = Tutorial.tutorials;
                BaseUnitySetupWorkflow.ApplyModules(config, new BaseUnitySetupOptions { assets = true, haptics = true, notifications = true, purchasing = true, tutorial = true, popups = false, feedback = false });
                Assert.IsTrue(config.assets.enabled && config.haptics.enabled && config.notifications.enabled && config.purchasing.enabled && config.tutorial.enabled);
                Assert.IsFalse(config.popups.enabled || config.feedback.enabled); Assert.AreEqual("coins", config.purchasing.products[0].id); Assert.AreEqual("intro", config.tutorial.tutorials[0].id);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void ValidationIgnoresDisabledModulesAndFlagsInvalidEnabledSettings()
        {
            var config = ScriptableObject.CreateInstance<FeatureConfig>();
            try { Assert.AreEqual(0, MobileModuleValidator.Validate(config, BuildTarget.Android).Count); config.purchasing.enabled = true; config.tutorial.enabled = true; config.tutorial.saveSlot = "baseunity_haptics";
                var issues = MobileModuleValidator.Validate(config, BuildTarget.Android); Assert.IsTrue(issues.Any(x => x.message.Contains("trusted receipt verifier"))); Assert.IsTrue(issues.Any(x => x.message.Contains("unique save slot"))); Assert.IsTrue(issues.Any(x => x.message.Contains("at least one product"))); }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
