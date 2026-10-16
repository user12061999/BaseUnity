using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BaseUnity.Features
{
    /// <summary>Main-thread API. Disabled/missing ads never grant rewards or block gameplay.</summary>
    public sealed class AdsService : IDisposable
    {
        private readonly AdsSettings settings;
        private readonly Func<IAdsBackend> factory;
        private IAdsBackend backend;
        private CancellationTokenSource lifetime = new CancellationTokenSource();
        private Task<bool> initialization;
        private bool busy, disposed;
        private double lastInterstitial = double.NegativeInfinity;
        public bool Enabled { get; private set; }
        public FeatureStatus Status { get; private set; }
        public string LastError { get; private set; }
        public AdsService(AdsSettings settings, Func<IAdsBackend> factory = null)
        {
            this.settings = settings ?? new AdsSettings(); this.factory = factory;
            Enabled = this.settings.enabled;
            Status = Enabled ? (factory == null ? FeatureStatus.MissingProvider : FeatureStatus.Initializing) : FeatureStatus.Disabled;
        }
        private bool Allowed(AdFormat format) => Enabled && !disposed &&
            (format == AdFormat.Banner ? settings.banner : format == AdFormat.Interstitial ? settings.interstitial : settings.rewarded);
        public bool IsReady(AdFormat format) => Allowed(format) && Status == FeatureStatus.Ready && backend != null && backend.IsReady(format);
        public Task<bool> InitializeAsync()
        {
            if (!Enabled || disposed || factory == null || (!settings.banner && !settings.interstitial && !settings.rewarded)) return Task.FromResult(false);
            return initialization ?? (initialization = InitializeCore());
        }
        private async Task<bool> InitializeCore()
        {
            var token = lifetime.Token;
            try
            {
                Status = FeatureStatus.Initializing; backend = factory();
                if (backend == null) throw new InvalidOperationException("Ads provider returned no backend.");
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.requestTimeoutSeconds)));
                    await FeatureTasks.Wait(backend.InitializeAsync(settings, timeout.Token), timeout.Token);
                }
                token.ThrowIfCancellationRequested(); Status = FeatureStatus.Ready; LastError = null; return true;
            }
            catch (Exception error)
            {
                if (!token.IsCancellationRequested) { LastError = error.Message; Status = FeatureStatus.Failed; backend?.Dispose(); backend = null; }
                return false;
            }
        }
        public async Task<bool> LoadAsync(AdFormat format)
        {
            if (!Allowed(format) || busy) return false;
            busy = true; var token = lifetime.Token;
            try
            {
                if (!await InitializeAsync() || !Allowed(format) || token.IsCancellationRequested) return false;
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.requestTimeoutSeconds)));
                    return await FeatureTasks.Wait(backend.LoadAsync(format, timeout.Token), timeout.Token);
                }
            }
            catch (Exception error) { if (!token.IsCancellationRequested) LastError = error.Message; return false; }
            finally { if (!token.IsCancellationRequested) busy = false; }
        }
        public async Task<AdResult> ShowAsync(AdFormat format)
        {
            if (!Allowed(format)) return AdResult.Disabled;
            if (busy) return AdResult.Busy;
            if (!IsReady(format)) return AdResult.Unavailable;
            if (format == AdFormat.Interstitial && UnityEngine.Time.realtimeSinceStartupAsDouble - lastInterstitial < settings.interstitialCooldownSeconds)
                return AdResult.Cooldown;
            busy = true; var token = lifetime.Token;
            try
            {
                var result = await FeatureTasks.Wait(backend.ShowAsync(format, token), token);
                if (token.IsCancellationRequested || !Allowed(format)) return AdResult.Disabled;
                if (format != AdFormat.Rewarded && result == AdResult.Rewarded) result = AdResult.Closed;
                if (format == AdFormat.Interstitial && result == AdResult.Closed) lastInterstitial = UnityEngine.Time.realtimeSinceStartupAsDouble;
                // Only a rewarded placement can grant a reward; closing a video is insufficient.
                return result;
            }
            catch (OperationCanceledException) { return AdResult.Disabled; }
            catch (Exception error) { LastError = error.Message; return AdResult.Failed; }
            finally { if (!token.IsCancellationRequested) busy = false; }
        }
        public void HideBanner() { backend?.HideBanner(); }
        public void SetEnabled(bool enabled)
        {
            if (disposed || Enabled == enabled) return;
            Enabled = enabled;
            if (!enabled) Shutdown();
            Status = enabled ? (factory == null ? FeatureStatus.MissingProvider : FeatureStatus.Initializing) : FeatureStatus.Disabled;
        }
        private void Shutdown()
        {
            lifetime.Cancel(); backend?.Dispose(); backend = null; initialization = null; busy = false;
            lifetime.Dispose(); lifetime = new CancellationTokenSource();
        }
        public void Dispose() { if (disposed) return; disposed = true; Enabled = false; Shutdown(); lifetime.Dispose(); Status = FeatureStatus.Disabled; }
    }
}
