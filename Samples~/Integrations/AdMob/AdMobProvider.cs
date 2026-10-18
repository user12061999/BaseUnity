using System;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using GoogleMobileAds.Api;
using AdFormat = BaseUnity.Features.AdFormat;
using UnityEngine;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/AdMob")]
    public sealed class AdMobProvider : AdsProvider
    {
        public override IAdsBackend Create() => new Backend();
        private sealed class Backend : IAdsBackend
        {
            private AdsSettings settings;
            private BannerView banner;
            private InterstitialAd interstitial;
            private RewardedAd rewarded;
            private bool disposed, bannerReady;
            private readonly SynchronizationContext main = SynchronizationContext.Current;
            private void Dispatch(Action action) { if (main == null) action(); else main.Post(_ => action(), null); }
            public async Task InitializeAsync(AdsSettings value, CancellationToken token)
            {
                settings = value;
                if (Application.isEditor || (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer))
                    throw new PlatformNotSupportedException("AdMob provider requires an Android/iOS player. No simulated reward is granted in Editor.");
                var requestConfiguration = MobileAds.GetRequestConfiguration();
                requestConfiguration.TagForUnderAgeOfConsent = settings.tagForUnderAgeOfConsent ? TagForUnderAgeOfConsent.True : TagForUnderAgeOfConsent.Unspecified;
                MobileAds.SetRequestConfiguration(requestConfiguration);
                var completion = new TaskCompletionSource<bool>();
                using (token.Register(() => completion.TrySetCanceled()))
                {
                    MobileAds.Initialize(status => Dispatch(() => {
                        if (disposed || token.IsCancellationRequested) return;
                        if (status == null) completion.TrySetException(new InvalidOperationException("AdMob initialization failed."));
                        else completion.TrySetResult(true);
                    }));
                    await completion.Task;
                }
            }
            private string Unit(AdFormat format)
            {
                bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
                if (settings.testMode)
                {
                    // Official Google demo ad units; never substitute production IDs during testing.
                    if (format == AdFormat.Banner) return ios ? "ca-app-pub-3940256099942544/2934735716" : "ca-app-pub-3940256099942544/6300978111";
                    if (format == AdFormat.Interstitial) return ios ? "ca-app-pub-3940256099942544/4411468910" : "ca-app-pub-3940256099942544/1033173712";
                    return ios ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-3940256099942544/5224354917";
                }
                if (format == AdFormat.Banner) return ios ? settings.iosBanner : settings.androidBanner;
                if (format == AdFormat.Interstitial) return ios ? settings.iosInterstitial : settings.androidInterstitial;
                return ios ? settings.iosRewarded : settings.androidRewarded;
            }
            public async Task<bool> LoadAsync(AdFormat format, CancellationToken token)
            {
                string id = Unit(format); if (string.IsNullOrWhiteSpace(id) || disposed) return false;
                var completion = new TaskCompletionSource<bool>();
                using (token.Register(() => completion.TrySetCanceled()))
                {
                    if (format == AdFormat.Banner)
                    {
                        banner?.Destroy(); bannerReady = false;
                        var instance = banner = new BannerView(id, AdSize.Banner, AdPosition.Bottom);
                        instance.OnBannerAdLoaded += () => Dispatch(() => {
                            if (disposed || token.IsCancellationRequested || banner != instance) return;
                            instance.Hide(); bannerReady = true; completion.TrySetResult(true);
                        });
                        instance.OnBannerAdLoadFailed += error => Dispatch(() => completion.TrySetResult(false));
                        instance.LoadAd(new AdRequest()); instance.Hide();
                    }
                    else if (format == AdFormat.Interstitial)
                    {
                        interstitial?.Destroy(); interstitial = null;
                        InterstitialAd.Load(id, new AdRequest(), (ad, error) => Dispatch(() => {
                            if (disposed || token.IsCancellationRequested || error != null) { ad?.Destroy(); completion.TrySetResult(false); return; }
                            interstitial = ad; completion.TrySetResult(ad != null);
                        }));
                    }
                    else
                    {
                        rewarded?.Destroy(); rewarded = null;
                        RewardedAd.Load(id, new AdRequest(), (ad, error) => Dispatch(() => {
                            if (disposed || token.IsCancellationRequested || error != null) { ad?.Destroy(); completion.TrySetResult(false); return; }
                            rewarded = ad; completion.TrySetResult(ad != null);
                        }));
                    }
                    return await completion.Task;
                }
            }
            public bool IsReady(AdFormat format) => !disposed && (format == AdFormat.Banner ? bannerReady :
                format == AdFormat.Interstitial ? interstitial != null && interstitial.CanShowAd() : rewarded != null && rewarded.CanShowAd());
            public async Task<AdResult> ShowAsync(AdFormat format, CancellationToken token)
            {
                if (!IsReady(format)) return AdResult.Unavailable;
                if (format == AdFormat.Banner) { banner.Show(); return AdResult.Closed; }
                var completion = new TaskCompletionSource<AdResult>(); bool earned = false;
                using (token.Register(() => completion.TrySetCanceled()))
                {
                    if (format == AdFormat.Interstitial)
                    {
                        var ad = interstitial; interstitial = null;
                        try
                        {
                            ad.OnAdFullScreenContentClosed += () => Dispatch(() => completion.TrySetResult(AdResult.Closed));
                            ad.OnAdFullScreenContentFailed += error => Dispatch(() => completion.TrySetResult(AdResult.Failed));
                            ad.Show(); return await completion.Task;
                        }
                        finally { ad.Destroy(); }
                    }
                    else
                    {
                        var ad = rewarded; rewarded = null;
                        try
                        {
                            ad.OnAdFullScreenContentClosed += () => Dispatch(() => completion.TrySetResult(earned ? AdResult.Rewarded : AdResult.Closed));
                            ad.OnAdFullScreenContentFailed += error => Dispatch(() => completion.TrySetResult(AdResult.Failed));
                            ad.Show(reward => Dispatch(() => { if (!disposed && !token.IsCancellationRequested) earned = true; }));
                            return await completion.Task;
                        }
                        finally { ad.Destroy(); }
                    }
                }
            }
            public void HideBanner() { banner?.Hide(); }
            public void Dispose()
            { disposed = true; bannerReady = false; banner?.Destroy(); interstitial?.Destroy(); rewarded?.Destroy(); banner = null; interstitial = null; rewarded = null; }
        }
    }
}
