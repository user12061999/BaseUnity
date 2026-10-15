using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaseUnity.Pooling
{
    /// <summary>Owns all spawned instances. Return objects to their originating pool; do not destroy them externally.</summary>
    public sealed class PrefabPool : IDisposable
    {
        private readonly GameObject prefab;
        private readonly GameObject storage;
        private readonly Stack<GameObject> available = new Stack<GameObject>();
        private readonly HashSet<GameObject> all = new HashSet<GameObject>();
        private readonly HashSet<GameObject> rented = new HashSet<GameObject>();
        private readonly int maxRetained;
        private bool disposed;

        public PrefabPool(GameObject prefab, Transform parent, int prewarm = 0, int maxRetained = 32)
        {
            if (!prefab) throw new ArgumentNullException(nameof(prefab));
            if (maxRetained < 0 || prewarm < 0 || prewarm > maxRetained) throw new ArgumentOutOfRangeException(nameof(prewarm));
            this.prefab = prefab;
            this.maxRetained = maxRetained;
            storage = new GameObject($"Pool: {prefab.name}");
            storage.transform.SetParent(parent, false);
            storage.SetActive(false);
            for (int i = 0; i < prewarm; i++) available.Push(Create());
        }

        private GameObject Create()
        {
            var instance = UnityEngine.Object.Instantiate(prefab, storage.transform);
            instance.SetActive(false);
            all.Add(instance);
            return instance;
        }

        public GameObject Rent(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PrefabPool));
            GameObject instance = null;
            while (available.Count > 0 && !instance)
            {
                instance = available.Pop();
                if (!instance) all.Remove(instance);
            }
            if (!instance) instance = Create();
            rented.Add(instance);
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = prefab.transform.localScale;
            instance.SetActive(true);
            return instance;
        }

        public void Return(GameObject instance)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PrefabPool));
            if (!instance || !rented.Remove(instance)) throw new InvalidOperationException("Object is not rented from this pool.");
            instance.SetActive(false);
            if (available.Count >= maxRetained)
            { all.Remove(instance); UnityEngine.Object.Destroy(instance); return; }
            instance.transform.SetParent(storage.transform, false);
            available.Push(instance);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var instance in all) if (instance) UnityEngine.Object.Destroy(instance);
            all.Clear(); rented.Clear(); available.Clear();
            if (storage) UnityEngine.Object.Destroy(storage);
        }
    }
}
