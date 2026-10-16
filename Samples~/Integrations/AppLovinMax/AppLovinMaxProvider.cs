using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/AppLovin MAX")]
    public sealed class AppLovinMaxProvider : AdsProvider
    {
        [Tooltip("Advertising IDs of test devices, configured before MAX initialization.")]
        public string[] testDeviceAdvertisingIds = Array.Empty<string>();
        public override IAdsBackend Create() => new Backend(testDeviceAdvertisingIds);
        private sealed class Backend : IAdsBackend
        {
            // MAX callbacks are global per unit. Keep a native fullscreen barrier across backend replacement.
            private static bool fullscreenActive;
            private readonly string[] testDevices;
            private readonly Dictionary<AdFormat, TaskCompletionSource<bool>> loads = new Dictionary<AdFormat, TaskCompletionSource<bool>>();
            private AdsSettings settings;
            private TaskCompletionSource<bool> initialized;
            private TaskCompletionSource<AdResult> showing;
            private AdFormat shownFormat;
            private bool disposed, subscribed, bannerCreated, bannerReady, earned;
            public Backend(string[] devices) { testDevices = devices ?? Array.Empty<string>(); }
            private string Unit(AdFormat format)
            {
                bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
                if (format == AdFormat.Banner) return ios ? settings.iosBanner : settings.androidBanner;
                if (format == AdFormat.Interstitial) return ios ? settings.iosInterstitial : settings.androidInterstitial;
                return ios ? settings.iosRewarded : settings.androidRewarded;
            }
            public async Task InitializeAsync(AdsSettings value, CancellationToken token)
            {
                settings = value;
                if (Application.isEditor || (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer))
                    throw new PlatformNotSupportedException("MAX provider requires an Android/iOS player. No simulated reward is granted in Editor.");
                if (settings.testMode && testDevices.Length == 0)
                    throw new InvalidOperationException("Add your test-device advertising ID to the MAX provider before using test mode.");
                if (settings.testMode && !MaxSdk.IsInitialized()) MaxSdk.SetTestDeviceAdvertisingIdentifiers(testDevices);
                Subscribe();
                if (MaxSdk.IsInitialized()) return;
                initialized = new TaskCompletionSource<bool>();
                using (token.Register(() => initialized.TrySetCanceled()))
                { MaxSdk.InitializeSdk(); await initialized.Task; }
            }
            private void Subscribe()
            {
                subscribed = true;
                MaxSdkCallbacks.OnSdkInitializedEvent += Initialized;
                MaxSdkCallbacks.Banner.OnAdLoadedEvent += BannerLoaded;
                MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += BannerFailed;
                MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += InterstitialLoaded;
                MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += InterstitialFailed;
                MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += InterstitialHidden;
                MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += InterstitialDisplayFailed;
                MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += RewardedLoaded;
                MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += RewardedFailed;
                MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += RewardedHidden;
                MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += RewardedDisplayFailed;
                MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += Rewarded;
            }
            public async Task<bool> LoadAsync(AdFormat format, CancellationToken token)
            {
                if (disposed || string.IsNullOrWhiteSpace(Unit(format))) return false;
                var completion = new TaskCompletionSource<bool>(); loads[format] = completion;
                using (token.Register(() => completion.TrySetCanceled()))
                {
                    if (format == AdFormat.Banner)
                    {
                        bannerReady = false;
                        if (bannerCreated) MaxSdk.DestroyBanner(Unit(format));
                        MaxSdk.CreateBanner(Unit(format), new MaxSdk.AdViewConfiguration(MaxSdk.AdViewPosition.BottomCenter));
                        bannerCreated = true;
                        MaxSdk.SetBannerBackgroundColor(Unit(format), Color.black);
                        MaxSdk.HideBanner(Unit(format)); MaxSdk.StopBannerAutoRefresh(Unit(format));
                    }
                    else if (format == AdFormat.Interstitial) MaxSdk.LoadInterstitial(Unit(format));
                    else MaxSdk.LoadRewardedAd(Unit(format));
                    return await completion.Task;
                }
            }
            public bool IsReady(AdFormat format) => !disposed && !string.IsNullOrWhiteSpace(Unit(format)) &&
                (format == AdFormat.Banner ? bannerReady : format == AdFormat.Interstitial ? MaxSdk.IsInterstitialReady(Unit(format)) : MaxSdk.IsRewardedAdReady(Unit(format)));
            public async Task<AdResult> ShowAsync(AdFormat format, CancellationToken token)
            {
                if (!IsReady(format)) return AdResult.Unavailable;
                if (format == AdFormat.Banner) { MaxSdk.ShowBanner(Unit(format)); MaxSdk.StartBannerAutoRefresh(Unit(format)); return AdResult.Closed; }
                if (fullscreenActive) return AdResult.Busy;
                fullscreenActive = true;
                var release = WatchNativeClose(format, Unit(format));
                showing = new TaskCompletionSource<AdResult>(); shownFormat = format; earned = false;
                using (token.Register(() => showing.TrySetCanceled()))
                {
                    try
                    {
                        try
                        {
                            if (format == AdFormat.Interstitial) MaxSdk.ShowInterstitial(Unit(format));
                            else MaxSdk.ShowRewardedAd(Unit(format));
                        }
                        catch { release(); throw; }
                        return await showing.Task;
                    }
                    finally { showing = null; }
                }
            }
            private static Action WatchNativeClose(AdFormat format, string unit)
            {
                Action<string, MaxSdk.AdInfo> closed = null;
                Action<string, MaxSdk.ErrorInfo, MaxSdk.AdInfo> failed = null;
                Action release = () => {
                    fullscreenActive = false;
                    if (format == AdFormat.Interstitial)
                    { MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= closed; MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= failed; }
                    else
                    { MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= closed; MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= failed; }
                };
                closed = (id, info) => { if (id == unit) release(); };
                failed = (id, error, info) => { if (id == unit) release(); };
                if (format == AdFormat.Interstitial)
                { MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += closed; MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += failed; }
                else
                { MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += closed; MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += failed; }
                return release;
            }
            private void Initialized(MaxSdk.SdkConfiguration config) { if (!disposed) initialized?.TrySetResult(true); }
            private void Loaded(AdFormat format, string id, bool success)
            {
                if (disposed || id != Unit(format)) return;
                if (format == AdFormat.Banner) bannerReady = success;
                if (loads.TryGetValue(format, out var task)) task.TrySetResult(success);
            }
            private void End(AdFormat format, string id, bool success)
            {
                if (disposed || showing == null || shownFormat != format || id != Unit(format)) return;
                showing.TrySetResult(!success ? AdResult.Failed : earned && format == AdFormat.Rewarded ? AdResult.Rewarded : AdResult.Closed);
            }
            private void BannerLoaded(string id, MaxSdk.AdInfo info) => Loaded(AdFormat.Banner, id, true);
            private void BannerFailed(string id, MaxSdk.ErrorInfo error) => Loaded(AdFormat.Banner, id, false);
            private void InterstitialLoaded(string id, MaxSdk.AdInfo info) => Loaded(AdFormat.Interstitial, id, true);
            private void InterstitialFailed(string id, MaxSdk.ErrorInfo error) => Loaded(AdFormat.Interstitial, id, false);
            private void InterstitialHidden(string id, MaxSdk.AdInfo info) => End(AdFormat.Interstitial, id, true);
            private void InterstitialDisplayFailed(string id, MaxSdk.ErrorInfo error, MaxSdk.AdInfo info) => End(AdFormat.Interstitial, id, false);
            private void RewardedLoaded(string id, MaxSdk.AdInfo info) => Loaded(AdFormat.Rewarded, id, true);
            private void RewardedFailed(string id, MaxSdk.ErrorInfo error) => Loaded(AdFormat.Rewarded, id, false);
            private void RewardedHidden(string id, MaxSdk.AdInfo info) => End(AdFormat.Rewarded, id, true);
            private void RewardedDisplayFailed(string id, MaxSdk.ErrorInfo error, MaxSdk.AdInfo info) => End(AdFormat.Rewarded, id, false);
            private void Rewarded(string id, MaxSdk.Reward reward, MaxSdk.AdInfo info)
            { if (!disposed && showing != null && shownFormat == AdFormat.Rewarded && id == Unit(AdFormat.Rewarded)) earned = true; }
            public void HideBanner()
            { if (bannerCreated) { MaxSdk.HideBanner(Unit(AdFormat.Banner)); MaxSdk.StopBannerAutoRefresh(Unit(AdFormat.Banner)); } }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                if (subscribed)
                {
                    MaxSdkCallbacks.OnSdkInitializedEvent -= Initialized;
                    MaxSdkCallbacks.Banner.OnAdLoadedEvent -= BannerLoaded;
                    MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= BannerFailed;
                    MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= InterstitialLoaded;
                    MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= InterstitialFailed;
                    MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= InterstitialHidden;
                    MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= InterstitialDisplayFailed;
                    MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= RewardedLoaded;
                    MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= RewardedFailed;
                    MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= RewardedHidden;
                    MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= RewardedDisplayFailed;
                    MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= Rewarded;
                }
                if (bannerCreated) MaxSdk.DestroyBanner(Unit(AdFormat.Banner));
                initialized?.TrySetCanceled(); showing?.TrySetCanceled();
                foreach (var task in loads.Values) task.TrySetCanceled();
                loads.Clear();
            }
        }
    }
}
