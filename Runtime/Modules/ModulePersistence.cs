using System;
using System.Threading;
using BaseUnity.Save;

namespace BaseUnity.Features
{
    internal static class ModulePersistence
    {
        public static T Load<T>(ISaveStore saves, string slot, Func<T, bool> valid, T fallback) where T : class
        {
            T loaded;
            if (saves is JsonSaveStore json)
                return json.LoadValidated(slot, out loaded, valid) == SaveLoadState.Missing ? fallback : loaded;
            if (!saves.TryLoad(slot, out loaded)) return fallback;
            if (loaded == null || !valid(loaded)) throw new InvalidOperationException("Invalid module save: " + slot);
            return loaded;
        }
    }
    internal sealed class ModuleScope : IDisposable
    {
        private Action release;
        public ModuleScope(Action release) { this.release = release; }
        public void Dispose() { var action = Interlocked.Exchange(ref release, null); action?.Invoke(); }
    }
}
