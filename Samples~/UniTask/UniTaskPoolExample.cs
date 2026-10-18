using System;
using BaseUnity.Async;
using BaseUnity.Pooling;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BaseUnity.Samples
{
    /// <summary>Assign a prefab, then wire Spawn to a UI button or call it from gameplay.</summary>
    public sealed class UniTaskPoolExample : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private float lifetimeSeconds = 2;
        [SerializeField] private bool ignoreTimeScale;
        private PrefabPool pool;
        private void Awake()
        {
            if (!prefab) { Debug.LogError("Assign a prefab to UniTaskPoolExample.", this); return; }
            pool = new PrefabPool(prefab, transform, prewarm: 8, maxRetained: 32);
        }
        public void Spawn() { if (pool != null) SpawnAsync().Forget(); }
        private async UniTask SpawnAsync()
        {
            var source = pool;
            var item = source.Rent(transform.position, transform.rotation, transform);
            source.TryGetRentalId(item, out var rental);
            try { await source.ReturnAfterAsync(item, TimeSpan.FromSeconds(lifetimeSeconds), ignoreTimeScale, destroyCancellationToken); }
            finally { source.TryReturn(item, rental); }
        }
        private void OnDestroy() { pool?.Dispose(); }
    }
}
