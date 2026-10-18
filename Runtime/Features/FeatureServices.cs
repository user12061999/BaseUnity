using System;
using System.Threading.Tasks;
using BaseUnity.Core;
using UnityEngine;

namespace BaseUnity.Features
{
    public sealed class FeatureServices : IDisposable
    {
        private readonly FeatureConfig runtimeConfig;
        public AssetService Assets { get; }
        public PopupService Popups { get; }
        public FeedbackService Feedback { get; }
        public TutorialService Tutorial { get; private set; }
        public HapticService Haptics { get; private set; }
        public LocalNotificationService Notifications { get; private set; }
        public PurchaseService Purchases { get; private set; }
        public AdsService Ads { get; }
        public ConsentService Consent { get; }
        public FirebaseService Firebase { get; }
        public OfflineService Offline { get; private set; }
        public LocalizationService Localization { get; private set; }
        public GameIntegrationSettings GameIntegration => UnityEngine.JsonUtility.FromJson<GameIntegrationSettings>(UnityEngine.JsonUtility.ToJson(runtimeConfig.gameIntegration ?? new GameIntegrationSettings()));
        public FeatureServices(FeatureConfig source)
        {
            runtimeConfig = source ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<FeatureConfig>();
            Assets = new AssetService(runtimeConfig.assets, runtimeConfig.assetProvider ? (Func<IAssetBackend>)runtimeConfig.assetProvider.Create : null);
            Popups = new PopupService(runtimeConfig.popups); Feedback = new FeedbackService(runtimeConfig.feedback, Popups);
            Consent = new ConsentService(runtimeConfig.consent, runtimeConfig.consentProvider ? (Func<IConsentBackend>)runtimeConfig.consentProvider.Create : null);
            if (runtimeConfig.ads != null) runtimeConfig.ads.tagForUnderAgeOfConsent = runtimeConfig.consent != null && runtimeConfig.consent.tagForUnderAgeOfConsent;
            Ads = new AdsService(runtimeConfig.ads, runtimeConfig.adsProvider ? (Func<IAdsBackend>)runtimeConfig.adsProvider.Create : null, Consent);
            Firebase = new FirebaseService(runtimeConfig.firebase, runtimeConfig.firebaseProvider ? (Func<IFirebaseBackend>)runtimeConfig.firebaseProvider.Create : null);
        }
        public void Register(ServiceRegistry services)
        {
            services.Register(Assets); services.Register(Popups); services.Register(Feedback);
            services.Register(this); services.Register(Consent); services.Register(Ads); services.Register(Firebase);
            var saves = services.Get<BaseUnity.Save.ISaveStore>();
            Offline = new OfflineService(runtimeConfig.offline, saves);
            Localization = new LocalizationService(runtimeConfig.localization, saves);
            services.Register(Offline); services.Register(Localization);
            Tutorial = new TutorialService(runtimeConfig.tutorial, saves);
            Haptics = new HapticService(runtimeConfig.haptics, saves, runtimeConfig.hapticProvider ? (Func<IHapticBackend>)runtimeConfig.hapticProvider.Create : null);
            Notifications = new LocalNotificationService(runtimeConfig.notifications, saves, runtimeConfig.notificationProvider ? (Func<ILocalNotificationBackend>)runtimeConfig.notificationProvider.Create : null);
            Purchases = new PurchaseService(runtimeConfig.purchasing, Offline, runtimeConfig.purchaseProvider ? (Func<IPurchaseBackend>)runtimeConfig.purchaseProvider.Create : null,
                runtimeConfig.purchaseVerifier ? (Func<IPurchaseVerifier>)runtimeConfig.purchaseVerifier.Create : null);
            services.Register(Tutorial); services.Register(Haptics); services.Register(Notifications); services.Register(Purchases);
            Offline.Changed += ApplyEntitlements; ApplyEntitlements();
            Notifications.Initialize();
        }
        public async Task InitializeAsync()
        {
            // Neither SDK initialization nor an unavailable network delays the game's OnReady.
            await Task.WhenAll(Ads.InitializeAsync(), Firebase.InitializeAsync(), Purchases != null ? Purchases.InitializeAsync() : Task.CompletedTask);
        }
        private void ApplyEntitlements() => Ads.SetNoAds(Offline.HasEntitlement(runtimeConfig.purchasing?.removeAdsEntitlement ?? "remove_ads"));
        public void Dispose()
        {
            if (Offline != null) Offline.Changed -= ApplyEntitlements;
            Purchases?.Dispose(); Notifications?.Dispose(); Haptics?.Dispose(); Tutorial?.Dispose();
            Assets.Dispose(); Feedback.Dispose(); Popups.Dispose(); Ads.Dispose(); Consent.Dispose(); Firebase.Dispose();
            if (runtimeConfig) UnityEngine.Object.Destroy(runtimeConfig);
        }
    }
}
