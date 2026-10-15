using System;
using System.Collections.Generic;

namespace BaseUnity.Core
{
    /// <summary>Synchronous, main-thread events. Dispose subscription tokens with their owner.</summary>
    public sealed class EventBus : IDisposable
    {
        private readonly Dictionary<Type, List<Delegate>> listeners = new Dictionary<Type, List<Delegate>>();
        private bool disposed;

        public IDisposable Subscribe<T>(Action<T> listener)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EventBus));
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            if (!listeners.TryGetValue(typeof(T), out var list))
                listeners.Add(typeof(T), list = new List<Delegate>());
            list.Add(listener);
            return new Subscription(() => list.Remove(listener));
        }

        public void Publish<T>(T message)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EventBus));
            if (!listeners.TryGetValue(typeof(T), out var list)) return;
            // Snapshot makes subscribe/unsubscribe during dispatch predictable.
            var errors = new List<Exception>();
            foreach (var listener in list.ToArray())
                try { ((Action<T>)listener)(message); }
                catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException(errors);
        }

        public void Dispose() { disposed = true; listeners.Clear(); }

        private sealed class Subscription : IDisposable
        {
            private Action unsubscribe;
            public Subscription(Action unsubscribe) { this.unsubscribe = unsubscribe; }
            public void Dispose() { var action = unsubscribe; unsubscribe = null; action?.Invoke(); }
        }
    }
}
