using System;
using System.Collections.Generic;
using System.IO;
namespace BaseUnity.Save
{
    public sealed class SaveMigrationException : IOException
    { public SaveMigrationException(string message, Exception inner = null) : base(message, inner) { } }
    /// <summary>Pure JSON transforms, one version at a time. Per-store, not static. Register the entire path before loading.</summary>
    public sealed class SaveMigrationRegistry
    {
        private readonly Dictionary<Type, Dictionary<int, Func<string, string>>> steps = new Dictionary<Type, Dictionary<int, Func<string, string>>>();
        public void Register<T>(int fromVersion, Func<string, string> migrate) where T : class
        {
            if (fromVersion < 1 || fromVersion == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(fromVersion));
            if (migrate == null) throw new ArgumentNullException(nameof(migrate));
            if (!steps.TryGetValue(typeof(T), out var list)) steps[typeof(T)] = list = new Dictionary<int, Func<string, string>>();
            if (list.ContainsKey(fromVersion)) throw new InvalidOperationException("Migration is already registered for " + typeof(T).Name + " v" + fromVersion);
            list.Add(fromVersion, migrate);
        }
        public bool CanMigrate<T>(int from, int to) where T : class
        {
            if (from < 1 || to < 1) return false;
            if (from == to) return true;
            if (from < 1 || from > to || !steps.TryGetValue(typeof(T), out var list)) return false;
            for (int version = from; version < to; version++) if (!list.ContainsKey(version)) return false;
            return true;
        }
        internal string Migrate<T>(string json, int from, int to) where T : class
        {
            if (!CanMigrate<T>(from, to)) throw new NotSupportedException("Missing save migration path for " + typeof(T).Name + ": " + from + " -> " + to);
            try
            {
                for (int version = from; version < to; version++)
                { json = steps[typeof(T)][version](json); if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Migration returned no JSON."); }
                return json;
            }
            catch (Exception error) { throw new SaveMigrationException("Migration failed for " + typeof(T).Name + ": " + from + " -> " + to + ". Files were preserved.", error); }
        }
    }
}
