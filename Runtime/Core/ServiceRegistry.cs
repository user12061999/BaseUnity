using System;
using System.Collections.Generic;

namespace BaseUnity.Core
{
    /// <summary>Main-thread container. Owns registered IDisposable instances.</summary>
    public sealed class ServiceRegistry : IDisposable
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        private readonly List<object> owned = new List<object>();
        private bool disposed;

        public void Register<T>(T service) where T : class
        {
            if (disposed) throw new ObjectDisposedException(nameof(ServiceRegistry));
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (services.ContainsKey(typeof(T))) throw new InvalidOperationException($"Service {typeof(T).Name} already registered.");
            services.Add(typeof(T), service);
            if (!owned.Exists(item => ReferenceEquals(item, service))) owned.Add(service);
        }

        public bool TryGet<T>(out T service) where T : class
        {
            service = null;
            if (disposed || !services.TryGetValue(typeof(T), out var value)) return false;
            service = (T)value;
            return true;
        }

        public T Get<T>() where T : class => TryGet<T>(out var service)
            ? service : throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            for (int i = owned.Count - 1; i >= 0; i--)
                try { (owned[i] as IDisposable)?.Dispose(); }
                catch (Exception error) { errors.Add(error); }
            owned.Clear();
            services.Clear();
            if (errors.Count > 0) throw new AggregateException(errors);
        }
    }
}
