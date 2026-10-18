using System;
using System.Collections;
using BaseUnity.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BaseUnity.Tests
{
    public sealed class PoolTests
    {
        private GameObject prefab;
        private GameObject parent;
        private PrefabPool pool;
        [SetUp] public void Setup()
        {
            prefab = new GameObject("Template"); prefab.SetActive(false);
            parent = new GameObject("Pool owner");
            pool = new PrefabPool(prefab, parent.transform, 1, 1);
        }
        [TearDown] public void Cleanup()
        {
            pool.Dispose();
            UnityEngine.Object.Destroy(prefab);
            UnityEngine.Object.Destroy(parent);
        }
        [UnityTest] public IEnumerator ReusesReturnedInstanceAndRejectsDoubleReturn()
        {
            var instance = pool.Rent(Vector3.one, Quaternion.identity);
            Assert.IsTrue(instance.activeSelf); Assert.AreEqual(Vector3.one, instance.transform.position);
            pool.Return(instance); Assert.IsFalse(instance.activeSelf);
            Assert.Throws<InvalidOperationException>(() => pool.Return(instance));
            Assert.AreSame(instance, pool.Rent(Vector3.zero, Quaternion.identity));
            yield return null;
        }
        [UnityTest] public IEnumerator RejectsForeignObjectsAndDestroysOverflow()
        {
            Assert.Throws<InvalidOperationException>(() => pool.Return(prefab));
            var first = pool.Rent(Vector3.zero, Quaternion.identity);
            var second = pool.Rent(Vector3.zero, Quaternion.identity);
            pool.Return(first); pool.Return(second);
            yield return null;
            Assert.IsTrue(first); Assert.IsFalse(second);
        }
        [UnityTest] public IEnumerator RentalIdsRejectStaleAndForeignReturns()
        {
            var item = pool.Rent(Vector3.zero, Quaternion.identity);
            Assert.IsTrue(pool.TryGetRentalId(item, out var previous)); pool.Return(item);
            Assert.AreSame(item, pool.Rent(Vector3.zero, Quaternion.identity));
            Assert.IsTrue(pool.TryGetRentalId(item, out var current)); Assert.AreNotEqual(previous, current);
            Assert.IsFalse(pool.TryReturn(item, previous)); Assert.IsFalse(pool.TryReturn(prefab, current));
            Assert.IsTrue(pool.TryReturn(item, current)); Assert.IsFalse(pool.TryReturn(item, current));
            Assert.AreEqual(0, pool.CountActive); Assert.AreEqual(1, pool.CountInactive); Assert.AreEqual(1, pool.CountAll);
            pool.Dispose(); Assert.IsTrue(pool.DisposalToken.IsCancellationRequested);
            Assert.IsFalse(pool.TryReturn(item, current)); yield return null;
        }
        [UnityTest] public IEnumerator DisposeDestroysActiveInstancesAndPreventsReuse()
        {
            var instance = pool.Rent(Vector3.zero, Quaternion.identity);
            pool.Dispose();
            yield return null;
            Assert.IsFalse(instance);
            Assert.Throws<ObjectDisposedException>(() => pool.Rent(Vector3.zero, Quaternion.identity));
        }
    }
}
