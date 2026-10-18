using System;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/Addressables")]
    public sealed class AddressablesProvider : AssetProvider
    {
        public override IAssetBackend Create() => new Backend();
        private sealed class Handle : IAssetHandle
        {
            private AsyncOperationHandle<UnityEngine.Object> handle;
            private bool disposed;
            public Handle(AsyncOperationHandle<UnityEngine.Object> handle) { this.handle = handle; }
            public UnityEngine.Object Asset => !disposed && handle.IsValid() ? handle.Result : null;
            public void Dispose() { if (disposed) return; disposed = true; if (handle.IsValid()) Addressables.Release(handle); }
        }
        private sealed class Backend : IAssetBackend
        {
            public async Task<IAssetHandle> LoadAsync(string address, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(address);
                try
                {
                    await handle.Task;
                    if (handle.Status != AsyncOperationStatus.Succeeded) throw new InvalidOperationException("Addressables load failed: " + address, handle.OperationException);
                    cancellation.ThrowIfCancellationRequested(); return new Handle(handle);
                }
                catch { if (handle.IsValid()) Addressables.Release(handle); throw; }
            }
            public void Dispose() { }
        }
    }
}
