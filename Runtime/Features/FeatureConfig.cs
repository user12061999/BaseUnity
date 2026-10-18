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
        [HideInInspector] public bool tagForUnderAgeOfConsent;
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
        public ConsentSettings consent = new ConsentSettings();
        public ConsentProvider consentProvider;
        public FirebaseSettings firebase = new FirebaseSettings();
        public FirebaseProvider firebaseProvider;
        public AssetSettings assets = new AssetSettings();
        public AssetProvider assetProvider;
        public PopupSettings popups = new PopupSettings();
        public FeedbackSettings feedback = new FeedbackSettings();
        public TutorialSettings tutorial = new TutorialSettings();
        public HapticSettings haptics = new HapticSettings();
        public HapticProvider hapticProvider;
        public LocalNotificationSettings notifications = new LocalNotificationSettings();
        public LocalNotificationProvider notificationProvider;
        public PurchaseSettings purchasing = new PurchaseSettings();
        public PurchaseProvider purchaseProvider;
        public PurchaseVerifier purchaseVerifier;
        public OfflineSettings offline = new OfflineSettings();
        public LocalizationSettings localization = new LocalizationSettings();
        public GameIntegrationSettings gameIntegration = new GameIntegrationSettings();
    }
}
