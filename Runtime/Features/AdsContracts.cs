using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace BaseUnity.Features {
    public enum AdsNetwork { AdMob, AppLovinMax }
    [Serializable] public sealed class AdsSettings
    {
        public bool enabled;
        public AdsNetwork network;
        [Tooltip("Applied to installed SDK settings from the Feature Manager editor window.")]
        public string androidAppId, iosAppId, maxSdkKey;
        public bool banner = true, interstitial = true, rewarded = true;
        [Tooltip("Use Google test ad units. Keep enabled during development.")]
        public bool testMode = true;
        [HideInInspector] public bool tagForUnderAgeOfConsent;
        [Min(1)] public int requestTimeoutSeconds = 20;
        [Min(0)] public float interstitialCooldownSeconds = 60;
        public string androidBanner, androidInterstitial, androidRewarded;
        public string iosBanner, iosInterstitial, iosRewarded;
    }
    public enum AdFormat { Banner, Interstitial, Rewarded }
    public enum AdResult { Disabled, Unavailable, Busy, Cooldown, Closed, Rewarded, Failed }

    public abstract class AdsProvider : ScriptableObject { public abstract IAdsBackend Create(); }
    public interface IAdsBackend : IDisposable
    {
        Task InitializeAsync(AdsSettings settings, CancellationToken cancellation);
        Task<bool> LoadAsync(AdFormat format, CancellationToken cancellation);
        bool IsReady(AdFormat format);
        Task<AdResult> ShowAsync(AdFormat format, CancellationToken cancellation);
        void HideBanner();
    }
}
