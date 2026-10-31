using System;
using BaseUnity.Save;
using UnityEngine;
namespace BaseUnity.Features
{
    public sealed class AdvancedSettingsService : IDisposable
    {
        [Serializable] public sealed class Preferences { public int quality = -1, frameRate = 60; public bool batterySaver, reducedMotion, gameplayEffects = true; }
        private readonly AdvancedSettings settings;
        private readonly ISaveStore saves;
        private readonly UIFeedbackService feedback;
        private Preferences current;
        private bool disposed;
        private readonly int originalFrameRate, originalQuality, originalVsync;
        public bool Enabled => !disposed && settings.enabled && LoadError == null;
        public string LoadError { get; private set; }
        public string LastError { get; private set; }
        public Preferences Current => JsonUtility.FromJson<Preferences>(JsonUtility.ToJson(current));
        public event Action Changed;
        public AdvancedSettingsService(AdvancedSettings settings, ISaveStore saves, UIFeedbackService feedback = null)
        {
            this.settings = settings ?? new AdvancedSettings(); this.saves = saves; this.feedback = feedback;
            originalFrameRate = Application.targetFrameRate; originalQuality = QualitySettings.GetQualityLevel(); originalVsync = QualitySettings.vSyncCount;
            current = new Preferences { frameRate = Math.Max(15, Math.Min(120, this.settings.defaultFrameRate)) };
            try { current = ModulePersistence.Load(saves, "baseunity_advanced_settings", Valid, current); }
            catch (Exception e) { LoadError = LastError = e.Message; }
            Apply();
        }
        private static bool Valid(Preferences value) => value != null && value.quality >= -1 && value.frameRate >= 15 && value.frameRate <= 120;
        public bool Save(Preferences value)
        {
            if (!Enabled || !Valid(value) || value.quality >= QualitySettings.names.Length) return false;
            var next = JsonUtility.FromJson<Preferences>(JsonUtility.ToJson(value));
            try { saves.Save("baseunity_advanced_settings", next); }
            catch (Exception e) { LastError = e.Message; return false; }
            current = next; LastError = null; Apply(); Changed?.Invoke(); return true;
        }
        private void Apply()
        {
            if (!Enabled) return;
            feedback?.SetReducedMotion(current.reducedMotion);
            if (!Application.isPlaying) return;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = current.batterySaver ? Math.Max(15, Math.Min(120, settings.batteryFrameRate)) : current.frameRate;
            if (current.quality >= 0 && current.quality < QualitySettings.names.Length) QualitySettings.SetQualityLevel(current.quality, true);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (settings.enabled && Application.isPlaying) { Application.targetFrameRate = originalFrameRate; QualitySettings.SetQualityLevel(originalQuality, true); QualitySettings.vSyncCount = originalVsync; }
            Changed = null;
        }
    }
}
