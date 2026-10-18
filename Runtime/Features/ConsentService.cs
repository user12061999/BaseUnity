using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BaseUnity.Features
{
    public enum ConsentDebugRegion { Device, Eea, NotEea }
    [Serializable] public sealed class ConsentSettings
    {
        public bool enabled = true;
        public bool tagForUnderAgeOfConsent;
        [Tooltip("Applied only in Editor or development builds, never release players.")]
        public bool debugMode;
        public ConsentDebugRegion debugRegion = ConsentDebugRegion.Eea;
        public string[] testDeviceHashedIds = Array.Empty<string>();
    }
    public abstract class ConsentProvider : ScriptableObject { public abstract IConsentBackend Create(); }
    public interface IConsentBackend : IDisposable
    {
        bool CanRequestAds { get; }
        bool PrivacyOptionsRequired { get; }
        Task GatherAsync(ConsentSettings settings, CancellationToken cancellation);
        Task ShowPrivacyOptionsAsync(CancellationToken cancellation);
        void ResetForTesting();
    }
    /// <summary>Main-thread API. UMP permission to request ads is not a personalized-ads consent flag.</summary>
    public sealed class ConsentService : IDisposable
    {
        private readonly ConsentSettings settings;
        private readonly Func<IConsentBackend> factory;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private IConsentBackend backend;
        private Task<bool> initialization;
        private bool disposed, busy;
        public bool Enabled => settings.enabled;
        public bool UnderAgeOfConsent => settings.tagForUnderAgeOfConsent;
        public bool IsBusy => busy;
        public FeatureStatus Status { get; private set; }
        public string LastError { get; private set; }
        public bool CanRequestAds => !disposed && (!Enabled || (!busy && Status == FeatureStatus.Ready && backend != null && backend.CanRequestAds));
        public bool PrivacyOptionsRequired => !disposed && Enabled && backend != null && backend.PrivacyOptionsRequired;
        public event Action Changed;
        public ConsentService(ConsentSettings settings, Func<IConsentBackend> factory = null)
        {
            this.settings = settings ?? new ConsentSettings(); this.factory = factory;
            Status = !Enabled ? FeatureStatus.Disabled : factory == null ? FeatureStatus.MissingProvider : FeatureStatus.Initializing;
        }
        public Task<bool> InitializeAsync()
        {
            if (disposed) return Task.FromResult(false);
            if (!Enabled) return Task.FromResult(true);
            if (factory == null) { LastError = "Google consent provider is missing. Ads are blocked."; return Task.FromResult(false); }
            return initialization ?? (initialization = Run(false));
        }
        public Task<bool> RetryAsync()
        {
            if (disposed || busy) return Task.FromResult(false);
            initialization = null; return InitializeAsync();
        }
        public Task<bool> ShowPrivacyOptionsAsync()
        {
            if (disposed || busy || !PrivacyOptionsRequired) return Task.FromResult(false);
            return Run(true);
        }
        private async Task<bool> Run(bool privacy)
        {
            busy = true; Status = FeatureStatus.Initializing; LastError = null; Changed?.Invoke();
            var token = lifetime.Token;
            try
            {
                if (backend == null) backend = factory();
                if (backend == null) throw new InvalidOperationException("Consent provider returned no backend.");
                // No user-interaction timeout: gameplay continues while a native form is open.
                await FeatureTasks.Wait(privacy ? backend.ShowPrivacyOptionsAsync(token) : backend.GatherAsync(settings, token), token);
                token.ThrowIfCancellationRequested();
                return true;
            }
            catch (Exception error)
            { if (!token.IsCancellationRequested) LastError = error.Message; return false; }
            finally
            {
                if (!disposed)
                {
                    busy = false;
                    // On an update/form error, UMP may still permit requests using a previous session.
                    Status = backend != null && backend.CanRequestAds ? FeatureStatus.Ready : FeatureStatus.Failed;
                    Changed?.Invoke();
                }
            }
        }
        public void ResetForTesting()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) throw new InvalidOperationException("Consent reset is development-only.");
            if (disposed || busy) throw new InvalidOperationException("Consent is busy or disposed.");
            backend?.ResetForTesting(); initialization = null; Status = Enabled ? FeatureStatus.Initializing : FeatureStatus.Disabled;
            LastError = null; Changed?.Invoke();
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; busy = false; lifetime.Cancel();
            backend?.Dispose(); lifetime.Dispose(); Status = FeatureStatus.Disabled; Changed?.Invoke(); Changed = null;
        }
    }
}
