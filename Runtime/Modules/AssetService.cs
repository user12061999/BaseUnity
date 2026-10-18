using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BaseUnity.Features
{
    public interface IAssetHandle : IDisposable { UnityEngine.Object Asset { get; } }
    public interface IAssetBackend : IDisposable { Task<IAssetHandle> LoadAsync(string address, CancellationToken cancellation); }
    public abstract class AssetProvider : ScriptableObject { public abstract IAssetBackend Create(); }
    public sealed class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        private IAssetHandle handle;
        private Action<AssetLease<T>> release;
        public T Asset => handle?.Asset as T;
        internal AssetLease(IAssetHandle handle, Action<AssetLease<T>> release) { this.handle = handle; this.release = release; }
        public void Dispose() { var current = handle; if (current == null) return; handle = null; current.Dispose(); release?.Invoke(this); release = null; }
    }
    /// <summary>Main-thread scoped asset ownership. Every successful load has one disposable lease.</summary>
    public sealed class AssetService : IDisposable
    {
        private readonly AssetSettings settings;
        private readonly Func<IAssetBackend> factory;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly HashSet<IDisposable> leases = new HashSet<IDisposable>();
        private IAssetBackend backend;
        private bool disposed;
        public bool Enabled => settings.enabled && !disposed;
        public int ActiveLeases => leases.Count;
        public string LastError { get; private set; }
        public AssetService(AssetSettings settings, Func<IAssetBackend> factory = null) { this.settings = settings ?? new AssetSettings(); this.factory = factory; }
        public async Task<AssetLease<T>> LoadAsync<T>(string address, CancellationToken cancellation = default) where T : UnityEngine.Object
        {
            if (disposed || !settings.enabled || string.IsNullOrWhiteSpace(address) || cancellation.IsCancellationRequested) return null;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellation))
            {
                Task<IAssetHandle> operation = null; IAssetHandle handle = null;
                try
                {
                    if (backend == null) backend = factory?.Invoke();
                    if (backend == null) throw new InvalidOperationException("Assign an Addressables provider.");
                    operation = backend.LoadAsync(address, linked.Token);
                    handle = await FeatureTasks.Wait(operation, linked.Token); linked.Token.ThrowIfCancellationRequested();
                    if (handle == null || !handle.Asset || !(handle.Asset is T)) throw new InvalidOperationException("Addressable asset has the wrong type: " + address);
                    var lease = new AssetLease<T>(handle, item => leases.Remove(item)); leases.Add(lease); LastError = null; return lease;
                }
                catch (Exception error)
                {
                    if (handle != null) handle.Dispose();
                    else if (operation != null) _ = ReleaseLate(operation);
                    if (!(error is OperationCanceledException)) LastError = error.Message;
                    return null;
                }
            }
        }
        private static async Task ReleaseLate(Task<IAssetHandle> operation) { try { (await operation)?.Dispose(); } catch { } }
        public void Dispose()
        {
            if (disposed) return; disposed = true; lifetime.Cancel();
            foreach (var lease in new List<IDisposable>(leases)) lease.Dispose(); leases.Clear(); backend?.Dispose(); lifetime.Dispose();
        }
    }
}
