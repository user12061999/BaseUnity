using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Async;
using BaseUnity.Features;
using BaseUnity.Pooling;
using BaseUnity.Scenes;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BaseUnity.Tests
{
    public sealed class UniTaskIntegrationTests
    {
        private GameObject prefab, owner;
        private PrefabPool pool;
        private float scale;
        [SetUp] public void Setup()
        {
            scale = UnityEngine.Time.timeScale;
            prefab = new GameObject("UniTask pool template"); prefab.SetActive(false);
            owner = new GameObject("UniTask pool owner"); pool = new PrefabPool(prefab, owner.transform, 1, 2);
        }
        [TearDown] public void Cleanup()
        { UnityEngine.Time.timeScale = scale; pool.Dispose(); UnityEngine.Object.Destroy(prefab); UnityEngine.Object.Destroy(owner); }
        [UnityTest] public IEnumerator TimedReturnReusesTheSameObject() => UniTask.ToCoroutine(async () => {
            var item = pool.Rent(Vector3.zero, Quaternion.identity);
            Assert.AreEqual(1, pool.CountActive); Assert.IsTrue(await pool.ReturnAfterAsync(item, TimeSpan.FromMilliseconds(20), true));
            Assert.AreEqual(0, pool.CountActive); Assert.AreEqual(1, pool.CountInactive);
            Assert.AreSame(item, pool.Rent(Vector3.one, Quaternion.identity));
        });
        [UnityTest] public IEnumerator OldTimerCannotReturnANewRental() => UniTask.ToCoroutine(async () => {
            var item = pool.Rent(Vector3.zero, Quaternion.identity);
            var oldTimer = pool.ReturnAfterAsync(item, TimeSpan.FromMilliseconds(50), true);
            Assert.IsTrue(pool.TryGetRentalId(item, out var oldId)); pool.Return(item);
            Assert.AreSame(item, pool.Rent(Vector3.zero, Quaternion.identity));
            Assert.IsFalse(pool.TryReturn(item, oldId)); Assert.IsFalse(await oldTimer);
            Assert.IsTrue(item.activeSelf); Assert.AreEqual(1, pool.CountActive);
        });
        [UnityTest] public IEnumerator CallerCancellationDoesNotReturnTheObject() => UniTask.ToCoroutine(async () => {
            var item = pool.Rent(Vector3.zero, Quaternion.identity);
            using (var cancel = new CancellationTokenSource())
            {
                var wait = pool.ReturnAfterAsync(item, TimeSpan.FromSeconds(2), true, cancel.Token); cancel.Cancel();
                bool canceled = false; try { await wait; } catch (OperationCanceledException) { canceled = true; }
                Assert.IsTrue(canceled); Assert.IsTrue(item.activeSelf); Assert.AreEqual(1, pool.CountActive);
            }
            pool.Return(item);
        });
        [UnityTest] public IEnumerator DisposeCancelsLongWaitAndDestroysTheObject() => UniTask.ToCoroutine(async () => {
            var item = pool.Rent(Vector3.zero, Quaternion.identity);
            var wait = pool.ReturnAfterAsync(item, TimeSpan.FromSeconds(10), true); pool.Dispose();
            var outcome = await UniTask.WhenAny(wait, UniTask.Delay(TimeSpan.FromSeconds(1), true).ContinueWith(() => true));
            Assert.AreEqual(0, outcome.winArgumentIndex); Assert.IsFalse(outcome.result1);
            await UniTask.NextFrame(); Assert.IsFalse(item); Assert.AreEqual(0, pool.CountAll);
        });
        [UnityTest] public IEnumerator ScaledDelayPausesAndUnscaledDelayStillReturns() => UniTask.ToCoroutine(async () => {
            UnityEngine.Time.timeScale = 0;
            var first = pool.Rent(Vector3.zero, Quaternion.identity); var second = pool.Rent(Vector3.zero, Quaternion.identity);
            var scaled = pool.ReturnAfterAsync(first, TimeSpan.FromMilliseconds(30));
            Assert.IsTrue(await pool.ReturnAfterAsync(second, TimeSpan.FromMilliseconds(60), true));
            Assert.AreEqual(UniTaskStatus.Pending, scaled.Status); Assert.IsTrue(first.activeSelf);
            UnityEngine.Time.timeScale = 1; Assert.IsTrue(await scaled);
        });
        [UnityTest] public IEnumerator AlreadyCanceledCallLeavesPoolUnchanged() => UniTask.ToCoroutine(async () => {
            var item = pool.Rent(Vector3.zero, Quaternion.identity);
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); bool canceled = false;
                try { await pool.ReturnAfterAsync(item, TimeSpan.Zero, cancellationToken: cancel.Token); }
                catch (OperationCanceledException) { canceled = true; }
                Assert.IsTrue(canceled); Assert.AreEqual(1, pool.CountActive);
            }
        });
        private sealed class Ads : IAdsBackend
        {
            public TaskCompletionSource<AdResult> pending;
            public Task InitializeAsync(AdsSettings settings, CancellationToken token) => Task.CompletedTask;
            public Task<bool> LoadAsync(AdFormat format, CancellationToken token) => Task.FromResult(true);
            public bool IsReady(AdFormat format) => true;
            public Task<AdResult> ShowAsync(AdFormat format, CancellationToken token) => pending == null ? Task.FromResult(AdResult.Rewarded) : pending.Task;
            public void HideBanner() { }
            public void Dispose() { }
        }
        [UnityTest] public IEnumerator AdsWrapperPreservesVerifiedResultsAndSuppressesCanceledCallbacks() => UniTask.ToCoroutine(async () => {
            var fake = new Ads();
            using (var service = new AdsService(new AdsSettings { enabled = true }, () => fake))
            {
                Assert.IsTrue(await service.InitializeUniTaskAsync()); Assert.IsTrue(await service.LoadUniTaskAsync(AdFormat.Rewarded));
                Assert.AreEqual(AdResult.Rewarded, await service.ShowUniTaskAsync(AdFormat.Rewarded));
                fake.pending = new TaskCompletionSource<AdResult>(); var show = service.ShowUniTaskAsync(AdFormat.Rewarded);
                service.SetEnabled(false); fake.pending.SetResult(AdResult.Rewarded);
                Assert.AreEqual(AdResult.Disabled, await show);
            }
            using (var firebase = new FirebaseService(new FirebaseSettings()))
            { Assert.IsFalse(await firebase.InitializeUniTaskAsync()); Assert.IsFalse(await firebase.FetchRemoteConfigUniTaskAsync()); }
        });
        [UnityTest] public IEnumerator SceneWrapperPreservesCancellationAndArgumentErrors() => UniTask.ToCoroutine(async () => {
            var scenes = new SceneService();
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); bool canceled = false;
                try { await scenes.LoadUniTaskAsync("NotLoaded", cancellationToken: cancel.Token); } catch (OperationCanceledException) { canceled = true; }
                Assert.IsTrue(canceled); Assert.IsFalse(scenes.IsBusy);
            }
            bool invalid = false; try { await scenes.LoadUniTaskAsync(""); } catch (ArgumentException) { invalid = true; }
            Assert.IsTrue(invalid); Assert.IsFalse(scenes.IsBusy);
        });
    }
}
