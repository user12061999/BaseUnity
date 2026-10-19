using System;
using System.Collections.Generic;

namespace BaseUnity.Pooling
{
    public readonly struct PoolSnapshot
    {
        public readonly string Name;
        public readonly int Total, Active, Inactive;
        internal PoolSnapshot(PrefabPool pool)
        { Name = pool.Name; Total = pool.CountAll; Active = pool.CountActive; Inactive = pool.CountInactive; }
    }
    /// <summary>Weak registrations do not keep abandoned pools alive. No collection in Release.</summary>
    public static class PoolDiagnostics
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly List<WeakReference<PrefabPool>> pools = new List<WeakReference<PrefabPool>>();
#endif
        internal static void Register(PrefabPool pool)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            pools.Add(new WeakReference<PrefabPool>(pool));
#endif
        }
        internal static void Unregister(PrefabPool pool)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            pools.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, pool));
#endif
        }
        public static IReadOnlyList<PoolSnapshot> Capture()
        {
            var result = new List<PoolSnapshot>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int i = pools.Count - 1; i >= 0; i--)
                if (pools[i].TryGetTarget(out var pool)) result.Add(new PoolSnapshot(pool));
                else pools.RemoveAt(i);
#endif
            return result;
        }
    }
}
