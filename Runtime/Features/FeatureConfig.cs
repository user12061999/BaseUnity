using System;
using UnityEngine;

namespace BaseUnity.Features
{
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
        public UIFeedbackProfile uiFeedbackProfile;
        public TutorialSettings tutorial = new TutorialSettings();
        public HapticSettings haptics = new HapticSettings();
        public HapticProvider hapticProvider;
        public LocalNotificationSettings notifications = new LocalNotificationSettings();
        public LocalNotificationProvider notificationProvider;
        public PurchaseSettings purchasing = new PurchaseSettings();
        public PurchaseProvider purchaseProvider;
        public PurchaseVerifier purchaseVerifier;
        public GameplaySettings gameplay = new GameplaySettings();
        public AdvancedSettings advancedSettings = new AdvancedSettings();
        public OfflineSettings offline = new OfflineSettings();
        public LocalizationSettings localization = new LocalizationSettings();
        public GameIntegrationSettings gameIntegration = new GameIntegrationSettings();
    }
}
