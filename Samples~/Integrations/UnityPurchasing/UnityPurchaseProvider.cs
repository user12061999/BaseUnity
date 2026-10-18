using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;
using UnityEngine.Purchasing;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/Unity IAP")]
    public sealed class UnityPurchaseProvider : PurchaseProvider
    {
        public override IPurchaseBackend Create() => new Backend();
        private sealed class Backend : IPurchaseBackend
        {
            private StoreController store;
            private PurchaseSettings settings;
            private bool disposed;
            private readonly SynchronizationContext context = SynchronizationContext.Current;
            private readonly Dictionary<string, PendingOrder> pending = new Dictionary<string, PendingOrder>();
            private TaskCompletionSource<bool> productsReady, purchasesFetched;
            public event Action<StorePurchase> Pending;
            public event Action<string> Failed;
            private void Dispatch(Action action) { if (context == null) throw new InvalidOperationException("Create IAP backend on Unity main thread."); context.Post(_ => { if (!disposed) action(); }, null); }
            public IReadOnlyList<StoreProductInfo> Products => store == null ? Array.Empty<StoreProductInfo>() : store.GetProducts().Select(p => new StoreProductInfo {
                id = p.definition.id, title = p.metadata.localizedTitle, price = p.metadata.localizedPriceString, available = p.availableToPurchase }).ToArray();
            public async Task InitializeAsync(PurchaseSettings settings, CancellationToken cancellation)
            {
                if (Application.isEditor || !Application.isMobilePlatform) throw new PlatformNotSupportedException("Real IAP requires Android/iOS. Editor fake-store purchases are not granted.");
                this.settings = settings; store = UnityIAPServices.StoreController();
                store.OnPurchasePending += PurchasePending; store.OnPurchaseFailed += PurchaseFailed; store.OnPurchaseDeferred += PurchaseDeferred;
                store.OnPurchasesFetched += PurchasesFetched; store.OnPurchasesFetchFailed += PurchasesFailed;
                store.OnProductsFetched += ProductsFetched; store.OnProductsFetchFailed += ProductsFailed; store.OnStoreDisconnected += Disconnected;
                productsReady = new TaskCompletionSource<bool>(); Observe(productsReady.Task);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    using (timeout.Token.Register(() => productsReady.TrySetCanceled()))
                    {
                        var connect = store.Connect(); Observe(connect);
                        var cancelled = new TaskCompletionSource<bool>();
                        using (timeout.Token.Register(() => cancelled.TrySetCanceled())) { await Task.WhenAny(connect, cancelled.Task); timeout.Token.ThrowIfCancellationRequested(); await connect; }
                        var definitions = settings.products.Select(p => new ProductDefinition(p.id,
                            Application.platform == RuntimePlatform.IPhonePlayer ? p.iosId : p.androidId,
                            p.kind == StoreProductKind.Consumable ? ProductType.Consumable : ProductType.NonConsumable)).ToList();
                        store.FetchProductsWithNoRetries(definitions); await productsReady.Task;
                    }
                }
                store.FetchPurchases();
            }
            private void ProductsFetched(List<Product> products) => Dispatch(() => productsReady.TrySetResult(true));
            private void ProductsFailed(ProductFetchFailed failure) => Dispatch(() => productsReady.TrySetException(new InvalidOperationException(failure.FailureReason)));
            private void Disconnected(StoreConnectionFailureDescription failure) => Dispatch(() => { productsReady?.TrySetException(new InvalidOperationException(failure.Message)); Failed?.Invoke(failure.Message); });
            private void PurchaseFailed(FailedOrder order) => Dispatch(() => Failed?.Invoke(order.FailureReason + ": " + order.Details));
            private void PurchaseDeferred(DeferredOrder order) => Dispatch(() => Failed?.Invoke("Purchase deferred. Waiting for store approval."));
            private void PurchasePending(PendingOrder order) => Dispatch(() => Forward(order, true));
            private void Forward(Order order, bool confirm)
            {
                var items = order.CartOrdered.Items().ToArray();
                if (items.Length != 1 || items[0].Quantity != 1) { Failed?.Invoke("Only single-product, single-quantity orders are supported; order remains pending."); return; }
                string id = items[0].Product.definition.id;
                if (!confirm && !settings.products.Any(x => x.id == id && x.kind == StoreProductKind.NonConsumable)) return;
                string transaction = order.Info.TransactionID;
                if (string.IsNullOrWhiteSpace(transaction)) { Failed?.Invoke("Store transaction ID is missing."); return; }
                if (confirm) pending[transaction] = (PendingOrder)order;
                string jws = order.Info.Apple?.jwsRepresentation;
                Pending?.Invoke(new StorePurchase { transactionId = transaction, productId = id,
                    receipt = string.IsNullOrWhiteSpace(jws) ? order.Info.Receipt : jws,
                    receiptFormat = string.IsNullOrWhiteSpace(jws) ? "unity-unified" : "apple-jws",
                    originalTransactionId = order.Info.Apple?.OriginalTransactionID,
                    storeProductId = items[0].Product.definition.storeSpecificId, applicationId = Application.identifier,
                    store = Application.platform == RuntimePlatform.IPhonePlayer ? "AppleAppStore" : "GooglePlay", requiresConfirmation = confirm });
            }
            private void PurchasesFetched(Orders orders) => Dispatch(() => {
                foreach (var order in orders.PendingOrders) Forward(order, true);
                foreach (var order in orders.ConfirmedOrders) Forward(order, false);
                purchasesFetched?.TrySetResult(true);
            });
            private void PurchasesFailed(PurchasesFetchFailureDescription failure) => Dispatch(() => { purchasesFetched?.TrySetException(new InvalidOperationException(failure.Message)); Failed?.Invoke(failure.Message); });
            public void Purchase(string productId) { if (disposed) throw new ObjectDisposedException(nameof(Backend)); store.PurchaseProduct(store.GetProducts().First(p => p.definition.id == productId)); }
            public void Confirm(StorePurchase purchase)
            {
                if (disposed) throw new ObjectDisposedException(nameof(Backend));
                if (!pending.TryGetValue(purchase.transactionId, out var order)) throw new InvalidOperationException("Pending store order not found.");
                store.ConfirmPurchase(order); pending.Remove(purchase.transactionId);
            }
            public async Task<bool> RestoreAsync(CancellationToken cancellation)
            {
                if (purchasesFetched != null && !purchasesFetched.Task.IsCompleted) return false;
                var restored = new TaskCompletionSource<bool>(); purchasesFetched = new TaskCompletionSource<bool>(); Observe(restored.Task); Observe(purchasesFetched.Task);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(60));
                    using (timeout.Token.Register(() => { restored.TrySetCanceled(); purchasesFetched.TrySetCanceled(); }))
                    {
                        store.RestoreTransactions((success, error) => Dispatch(() => { if (success) restored.TrySetResult(true); else restored.TrySetException(new InvalidOperationException(error)); }));
                        await restored.Task; store.FetchPurchases(); return await purchasesFetched.Task;
                    }
                }
            }
            private static void Observe(Task task) { _ = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                if (store == null) return;
                store.OnPurchasePending -= PurchasePending; store.OnPurchaseFailed -= PurchaseFailed; store.OnPurchaseDeferred -= PurchaseDeferred;
                store.OnPurchasesFetched -= PurchasesFetched; store.OnPurchasesFetchFailed -= PurchasesFailed; store.OnProductsFetched -= ProductsFetched; store.OnProductsFetchFailed -= ProductsFailed; store.OnStoreDisconnected -= Disconnected;
                productsReady?.TrySetCanceled(); purchasesFetched?.TrySetCanceled(); Pending = null; Failed = null;
            }
        }
    }
}
