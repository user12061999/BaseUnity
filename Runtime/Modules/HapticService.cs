using System;
using BaseUnity.Save;
using UnityEngine;

namespace BaseUnity.Features
{
    public interface IHapticBackend : IDisposable { bool Play(HapticKind kind); }
    public abstract class HapticProvider : ScriptableObject { public abstract IHapticBackend Create(); }
    public sealed class HapticService : IDisposable
    {
        [Serializable] public sealed class Preference { public bool enabled = true; }
        private readonly HapticSettings settings;
        private readonly ISaveStore saves;
        private readonly Func<IHapticBackend> factory;
        private IHapticBackend backend;
        private bool optedIn = true, disposed;
        private double last = double.NegativeInfinity;
        public bool ModuleEnabled => settings.enabled && !disposed;
        public bool Enabled => settings.enabled && optedIn && !disposed && LoadError == null;
        public string LoadError { get; private set; }
        public string LastError { get; private set; }
        public HapticService(HapticSettings settings, ISaveStore saves, Func<IHapticBackend> factory = null)
        {
            this.settings = settings ?? new HapticSettings(); this.saves = saves; this.factory = factory;
            try { optedIn = ModulePersistence.Load(saves, "baseunity_haptics", _ => true, new Preference()).enabled; }
            catch (Exception error) { LoadError = LastError = error.Message; }
        }
        public bool SetUserEnabled(bool enabled)
        {
            if (LoadError != null || disposed) return false;
            try { saves.Save("baseunity_haptics", new Preference { enabled = enabled }); optedIn = enabled; return true; }
            catch (Exception error) { LastError = error.Message; return false; }
        }
        public bool Play(HapticKind kind = HapticKind.Light)
        {
            if (!Enabled || !Enum.IsDefined(typeof(HapticKind), kind) || UnityEngine.Time.realtimeSinceStartupAsDouble - last < settings.minimumInterval) return false;
            try { if (backend == null) backend = factory?.Invoke(); bool result = backend != null && backend.Play(kind); if (result) last = UnityEngine.Time.realtimeSinceStartupAsDouble; return result; }
            catch (Exception error) { LastError = error.Message; return false; }
        }
        public void Dispose() { if (disposed) return; disposed = true; backend?.Dispose(); }
    }
}
