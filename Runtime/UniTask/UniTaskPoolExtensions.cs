using System;
using System.Threading;
using BaseUnity.Pooling;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BaseUnity.Async
{
    public static class UniTaskPoolExtensions
    {
        /// <summary>False if the rental ended or the pool was disposed. Caller cancellation leaves ownership with the caller.</summary>
        public static async UniTask<bool> ReturnAfterAsync(this PrefabPool pool, GameObject instance, TimeSpan delay,
            bool ignoreTimeScale = false, CancellationToken cancellationToken = default)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
            cancellationToken.ThrowIfCancellationRequested();
            if (!pool.TryGetRentalId(instance, out var rental)) throw new InvalidOperationException("Object is not rented from this pool.");
            var disposed = pool.DisposalToken;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposed))
            {
                try { await UniTask.Delay(delay, ignoreTimeScale, cancellationToken: linked.Token); }
                catch (OperationCanceledException) when (disposed.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { return false; }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return pool.TryReturn(instance, rental);
        }
    }
}
