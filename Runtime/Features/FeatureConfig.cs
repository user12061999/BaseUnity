using System;
using UnityEngine;

namespace BaseUnity.Features
{
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
        [Min(1)] public int requestTimeoutSeconds = 20;
        [Min(0)] public float interstitialCooldownSeconds = 60;
        public string androidBanner, androidInterstitial, androidRewarded;
        public string iosBanner, iosInterstitial, iosRewarded;
    }
    [Serializable] public sealed class RemoteDefault
    {
        public string key;
        public string value;
    }
    [Serializable] public sealed class FirebaseSettings
    {
        public bool enabled;
        public bool analytics = true, crashlytics = true, remoteConfig = true;
        [Min(1)] public int requestTimeoutSeconds = 15;
        [Min(0)] public int minimumFetchIntervalSeconds = 3600;
        public RemoteDefault[] defaults = Array.Empty<RemoteDefault>();
    }
    [CreateAssetMenu(menuName = "BaseUnity/Feature Config")]
    public sealed class FeatureConfig : ScriptableObject
    {
        public AdsSettings ads = new AdsSettings();
        public AdsProvider adsProvider;
        public FirebaseSettings firebase = new FirebaseSettings();
        public FirebaseProvider firebaseProvider;
    }
}
