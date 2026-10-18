using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BaseUnity.Features
{
    public sealed class StoreProductInfo { public string id, title, price; public bool available; }
    [Serializable] public sealed class StorePurchase
    {
        public string transactionId, productId, receipt, store;
        public string receiptFormat, storeProductId, applicationId, originalTransactionId;
        public bool requiresConfirmation = true;
    }
    public interface IPurchaseBackend : IDisposable
    {
        event Action<StorePurchase> Pending;
        event Action<string> Failed;
        Task InitializeAsync(PurchaseSettings settings, CancellationToken cancellation);
        IReadOnlyList<StoreProductInfo> Products { get; }
        void Purchase(string productId);
        void Confirm(StorePurchase purchase);
        Task<bool> RestoreAsync(CancellationToken cancellation);
    }
    public abstract class PurchaseProvider : ScriptableObject { public abstract IPurchaseBackend Create(); }
    public interface IPurchaseVerifier : IDisposable { Task<bool> VerifyAsync(StorePurchase purchase, CancellationToken cancellation); }
    public abstract class PurchaseVerifier : ScriptableObject { public abstract IPurchaseVerifier Create(); }
    /// <summary>Store receipt -> verification -> atomic wallet/receipt save -> confirm. Never grants from a button click.</summary>
    public sealed class PurchaseService : IDisposable
    {
        private readonly PurchaseSettings settings;
        private readonly OfflineService wallet;
        private readonly Func<IPurchaseBackend> factory;
        private readonly Func<IPurchaseVerifier> verifierFactory;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, StorePurchase> pending = new Dictionary<string, StorePurchase>();
        private readonly Dictionary<string, TaskCompletionSource<bool>> processing = new Dictionary<string, TaskCompletionSource<bool>>();
        private IPurchaseBackend backend;
        private IPurchaseVerifier verifier;
        private Task<bool> initialization;
        private bool disposed, storeBusy, restoring;
        private string buyingProduct;
        public bool Enabled => settings.enabled && !disposed;
        public bool IsBusy => storeBusy || restoring || processing.Count > 0;
        public FeatureStatus Status { get; private set; }
        public string LastError { get; private set; }
        public int PendingCount => pending.Count;
        public IReadOnlyList<StoreProductInfo> Products => backend?.Products ?? Array.Empty<StoreProductInfo>();
        public event Action Changed;
        public PurchaseService(PurchaseSettings settings, OfflineService wallet, Func<IPurchaseBackend> factory = null, Func<IPurchaseVerifier> verifierFactory = null)
        {
            this.settings = JsonUtility.FromJson<PurchaseSettings>(JsonUtility.ToJson(settings ?? new PurchaseSettings())); this.wallet = wallet; this.factory = factory; this.verifierFactory = verifierFactory;
            Status = !Enabled ? FeatureStatus.Disabled : factory == null || verifierFactory == null ? FeatureStatus.MissingProvider : FeatureStatus.Initializing;
        }
        public Task<bool> InitializeAsync()
        {
            if (!Enabled || factory == null || verifierFactory == null) { if (Enabled) LastError = "Assign a store provider and a trusted receipt verifier."; return Task.FromResult(false); }
            if (initialization != null && initialization.IsCompleted && !initialization.Result)
            {
                if (IsBusy) return Task.FromResult(false);
                ReleaseBackend(); initialization = null;
            }
            return initialization ?? (initialization = Initialize());
        }
        private async Task<bool> Initialize()
        {
            try
            {
                if (!wallet.Enabled || wallet.LoadError != null) throw new InvalidOperationException("IAP needs a healthy enabled Offline wallet.");
                if (settings.products == null || settings.products.Length == 0 || (settings.products ?? Array.Empty<StoreProduct>()).Any(x => x == null || string.IsNullOrWhiteSpace(x.id) || x.coins < 0 || x.gems < 0 || !Enum.IsDefined(typeof(StoreProductKind), x.kind)) || settings.products.Select(x => x.id).Distinct().Count() != settings.products.Length)
                    throw new InvalidOperationException("Invalid IAP catalog.");
                backend = factory(); verifier = verifierFactory();
                if (backend == null || verifier == null) throw new InvalidOperationException("Missing IAP backend or verifier.");
                backend.Pending += OnPending; backend.Failed += OnFailed;
                await FeatureTasks.Wait(backend.InitializeAsync(settings, lifetime.Token), lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested(); Status = FeatureStatus.Ready; Changed?.Invoke(); return true;
            }
            catch (Exception error) { if (!disposed) { LastError = error.Message; Status = FeatureStatus.Failed; Changed?.Invoke(); } return false; }
        }
        public async Task<bool> BuyAsync(string id)
        {
            if (!Enabled || IsBusy || !await InitializeAsync() || !Products.Any(x => x.id == id && x.available)) return false;
            var product = settings.products.FirstOrDefault(x => x.id == id);
            if (IsBusy || disposed || product == null || (product.kind == StoreProductKind.NonConsumable && wallet.HasStoreProduct(id))) return false;
            try { storeBusy = true; buyingProduct = id; Changed?.Invoke(); backend.Purchase(id); return true; }
            catch (Exception error) { OnFailed(error.Message); return false; }
        }
        public async Task<bool> RestoreAsync()
        {
            if (!Enabled || IsBusy || !await InitializeAsync()) return false;
            if (IsBusy || disposed) return false;
            try
            {
                restoring = true; Changed?.Invoke();
                bool restored = await FeatureTasks.Wait(backend.RestoreAsync(lifetime.Token), lifetime.Token);
                await RetryPendingAsync(); return restored && pending.Count == 0;
            }
            catch (Exception error) { if (!disposed) LastError = error.Message; return false; }
            finally { if (!disposed) { restoring = false; Changed?.Invoke(); } }
        }
        private void OnFailed(string error) { if (disposed) return; storeBusy = false; buyingProduct = null; LastError = error; Changed?.Invoke(); }
        private void OnPending(StorePurchase purchase)
        {
            if (disposed || purchase == null || string.IsNullOrWhiteSpace(purchase.transactionId) || string.IsNullOrWhiteSpace(purchase.productId)) return;
            if (pending.TryGetValue(purchase.transactionId, out var existing) && existing.productId != purchase.productId) { OnFailed("Store transaction/product mismatch."); return; }
            // Snapshot callback data so a backend cannot mutate the product while verification is in flight.
            var copy = JsonUtility.FromJson<StorePurchase>(JsonUtility.ToJson(purchase)); pending[copy.transactionId] = copy;
            if (buyingProduct == copy.productId) { storeBusy = false; buyingProduct = null; }
            _ = Process(copy);
        }
        public async Task RetryPendingAsync() { foreach (var purchase in pending.Values.ToArray()) await Process(purchase); }
        private Task Process(StorePurchase purchase)
        {
            if (disposed) return Task.CompletedTask;
            if (processing.TryGetValue(purchase.transactionId, out var existing)) return existing.Task;
            var completion = new TaskCompletionSource<bool>(); processing.Add(purchase.transactionId, completion);
            _ = ProcessCore(purchase, completion); return completion.Task;
        }
        private async Task ProcessCore(StorePurchase purchase, TaskCompletionSource<bool> completion)
        {
            bool entered = false;
            try
            {
                await serial.WaitAsync(lifetime.Token); entered = true;
                var product = settings.products.FirstOrDefault(x => x.id == purchase.productId);
                if (product == null || (!purchase.requiresConfirmation && product.kind != StoreProductKind.NonConsumable)) throw new InvalidOperationException("Unrecognized purchase or unsupported consumable restore.");
                if (!wallet.HasStoreTransaction(purchase.transactionId, purchase.productId))
                {
                    if (string.IsNullOrWhiteSpace(purchase.receipt) || !await FeatureTasks.Wait(verifier.VerifyAsync(purchase, lifetime.Token), lifetime.Token)) throw new InvalidOperationException("Receipt verification failed. Order remains pending.");
                    lifetime.Token.ThrowIfCancellationRequested();
                    if (!wallet.ApplyStorePurchase(purchase.transactionId, product)) throw new InvalidOperationException(wallet.LastError);
                }
                lifetime.Token.ThrowIfCancellationRequested();
                if (purchase.requiresConfirmation) backend.Confirm(purchase);
                pending.Remove(purchase.transactionId); LastError = null;
            }
            catch (Exception error) { if (!disposed) LastError = error.Message; }
            finally
            {
                if (entered) serial.Release(); processing.Remove(purchase.transactionId);
                if (!disposed) Changed?.Invoke();
                completion.TrySetResult(true);
            }
        }
        private void ReleaseBackend()
        {
            if (backend != null) { backend.Pending -= OnPending; backend.Failed -= OnFailed; backend.Dispose(); backend = null; }
            verifier?.Dispose(); verifier = null;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; lifetime.Cancel();
            ReleaseBackend(); Status = FeatureStatus.Disabled; Changed = null;
            // The cancellation source remains valid for late native callbacks/tasks until they have unwound.
        }
    }
}
