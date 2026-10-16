using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BaseUnity.Features;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    public sealed class FeatureValidationIssue
    {
        public readonly MessageType severity;
        public readonly string message;
        public readonly UnityEngine.Object context;
        public FeatureValidationIssue(MessageType severity, string message, UnityEngine.Object context)
        { this.severity = severity; this.message = message; this.context = context; }
    }

    /// <summary>Read-only checks. Does not initialize SDKs, modify settings or contact services.</summary>
    public static class FeatureBuildValidator
    {
        public static List<FeatureValidationIssue> Validate(FeatureConfig config, BuildTarget target)
        {
            var issues = new List<FeatureValidationIssue>();
            Action<string> error = message => issues.Add(new FeatureValidationIssue(MessageType.Error, message, config));
            Action<string> warn = message => issues.Add(new FeatureValidationIssue(MessageType.Warning, message, config));
            if (!config) { error("Choose a FeatureConfig to validate."); return issues; }
            bool ios = target == BuildTarget.iOS;
            bool mobile = ios || target == BuildTarget.Android;
            var ads = config.ads;
            if (ads != null && ads.enabled)
            {
                bool max = ads.network == AdsNetwork.AppLovinMax;
                string expected = max ? "BaseUnity.Integrations.AppLovinMaxProvider" : "BaseUnity.Integrations.AdMobProvider";
                if (!config.adsProvider) error("Ads: assign a provider in Feature Manager > Ads.");
                else if (config.adsProvider.GetType().FullName != expected)
                    warn("Ads: provider differs from the selected network. Verify this custom provider's requirements manually.");
                if ((!config.adsProvider || config.adsProvider.GetType().FullName == expected) &&
                    FeatureConfigurationUtility.FindType(max ? "MaxSdk" : "GoogleMobileAds.Api.MobileAds") == null)
                    error("Ads: selected network SDK is not installed or not compiled. Install the SDK before its BaseUnity adapter.");
                if (!mobile) warn("Bundled Ads adapters support Android/iOS players only. Select a mobile validation target.");
                if (ads.requestTimeoutSeconds < 1) error("Ads: request timeout must be at least 1 second.");
                if (ads.interstitial && (ads.interstitialCooldownSeconds < 0 || float.IsNaN(ads.interstitialCooldownSeconds) || float.IsInfinity(ads.interstitialCooldownSeconds)))
                    error("Ads: interstitial cooldown must be a finite non-negative value.");
                if (!ads.banner && !ads.interstitial && !ads.rewarded) warn("Ads is enabled but all ad formats are disabled.");
                if (ads.testMode) warn("Ads test mode is enabled. Review this before a production release.");
                if (mobile)
                {
                    string appId = ios ? ads.iosAppId : ads.androidAppId;
                    if (!max && string.IsNullOrWhiteSpace(appId)) warn("AdMob App ID is blank in BaseUnity. Verify the target platform's App ID in the vendor settings.");
                    try { FeatureConfigurationUtility.ValidateAppId(appId); } catch (Exception e) { error(e.Message); }
                    if (max && string.IsNullOrWhiteSpace(ads.maxSdkKey)) warn("MAX SDK key is blank in BaseUnity. Verify the key in AppLovin settings.");
                    // AdMob substitutes official demo unit IDs in test mode; MAX still needs real placements.
                    if (max || !ads.testMode)
                    {
                        if (ads.banner) CheckUnit(ios ? ads.iosBanner : ads.androidBanner, "Banner", max, error);
                        if (ads.interstitial) CheckUnit(ios ? ads.iosInterstitial : ads.androidInterstitial, "Interstitial", max, error);
                        if (ads.rewarded) CheckUnit(ios ? ads.iosRewarded : ads.androidRewarded, "Rewarded", max, error);
                    }
                    if (max && ads.testMode && config.adsProvider && config.adsProvider.GetType().FullName == expected)
                    {
                        var devices = new SerializedObject(config.adsProvider).FindProperty("testDeviceAdvertisingIds");
                        if (devices == null || devices.arraySize == 0) error("MAX test mode requires test-device advertising IDs on the provider asset.");
                        else for (int i = 0; i < devices.arraySize; i++)
                            if (string.IsNullOrWhiteSpace(devices.GetArrayElementAtIndex(i).stringValue)) error("MAX test-device advertising IDs must not be empty.");
                    }
                    warn("Verify applied vendor App IDs/SDK key and mediation dashboard settings separately; BaseUnity fields alone do not configure the SDK.");
                }
            }
            var firebase = config.firebase;
            if (firebase != null && firebase.enabled && (firebase.analytics || firebase.crashlytics || firebase.remoteConfig))
            {
                if (!config.firebaseProvider) error("Firebase: assign a provider in Feature Manager > Firebase.");
                if (!config.firebaseProvider || config.firebaseProvider.GetType().FullName == "BaseUnity.Integrations.FirebaseSdkProvider")
                    foreach (string type in new[] { "Firebase.FirebaseApp", "Firebase.Analytics.FirebaseAnalytics", "Firebase.Crashlytics.Crashlytics", "Firebase.RemoteConfig.FirebaseRemoteConfig" })
                        if (FeatureConfigurationUtility.FindType(type) == null) error("Firebase SDK type is missing: " + type + ". The bundled adapter requires all three product SDKs.");
                if (firebase.requestTimeoutSeconds < 1) error("Firebase: request timeout must be at least 1 second.");
                if (mobile)
                {
                    string name = ios ? "GoogleService-Info.plist" : "google-services.json";
                    string path = "Assets/" + name;
                    if (!File.Exists(path)) error("Firebase: import " + name + " through Feature Manager.");
                    else try { FeatureConfigurationUtility.ReadFirebaseInfo(File.ReadAllText(path), ios, FeatureConfigurationUtility.ApplicationId(ios)); }
                        catch (Exception e) { error("Firebase: " + e.Message); }
                    if (Directory.Exists("Assets") && Directory.GetFiles("Assets", name, SearchOption.AllDirectories).Length > 1)
                        error("Firebase: multiple " + name + " files exist under Assets. Keep one app configuration.");
                }
                else warn("Firebase native config checks require an Android or iOS validation target.");
                if (firebase.remoteConfig)
                {
                    if (firebase.minimumFetchIntervalSeconds < 0) error("Remote Config: fetch interval must not be negative.");
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in firebase.defaults ?? Array.Empty<RemoteDefault>())
                        if (entry == null || string.IsNullOrWhiteSpace(entry.key)) warn("Remote Config: a local default has an empty key and will be ignored.");
                        else if (!keys.Add(entry.key)) warn("Remote Config: duplicate default key '" + entry.key + "'; the last value wins.");
                }
            }
            return issues;
        }

        private static void CheckUnit(string id, string format, bool max, Action<string> error)
        {
            if (string.IsNullOrWhiteSpace(id)) error(format + ": missing ad-unit ID for the selected platform.");
            else if (!max && !Regex.IsMatch(id, @"^ca-app-pub-\d+/\d+$")) error(format + ": AdMob unit ID must use ca-app-pub-.../... (not an App ID with '~').");
            else if (max && id.StartsWith("ca-app-pub-", StringComparison.Ordinal)) error(format + ": use a MAX ad-unit ID when MAX is the provider, including AdMob mediation.");
        }

        public static List<FeatureValidationIssue> ValidateScenes(UnityEngine.Object flow, EditorBuildSettingsScene[] scenes)
        {
            var issues = new List<FeatureValidationIssue>();
            var enabled = (scenes ?? Array.Empty<EditorBuildSettingsScene>()).Where(s => s.enabled).Select(s => s.path).ToArray();
            Action<string> error = message => issues.Add(new FeatureValidationIssue(MessageType.Error, message, flow));
            if (enabled.Length == 0) error("No scenes are enabled in the build scene list.");
            foreach (string path in enabled)
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) error("Build scene is missing: " + path);
            if (!flow) { issues.Add(new FeatureValidationIssue(MessageType.Warning, "No GameFlowConfig selected; Home/InGame/level scene references were not checked.", null)); return issues; }
            if (flow.GetType().FullName != "BaseUnity.GameFlow.GameFlowConfig") { error("Choose a GameFlowConfig asset for scene validation."); return issues; }
            var data = new SerializedObject(flow);
            Action<string, string, bool> check = (path, label, required) => {
                if (string.IsNullOrWhiteSpace(path)) { if (required) error(label + ": scene is not assigned."); return; }
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) error(label + ": scene asset is missing: " + path);
                else if (!enabled.Contains(path)) error(label + ": enable this scene in the build scene list: " + path);
            };
            check(data.FindProperty("homeScene").stringValue, "Home", true);
            check(data.FindProperty("inGameScene").stringValue, "InGame", true);
            var levels = data.FindProperty("levels");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var asset = level.FindPropertyRelative("data").objectReferenceValue;
                string path = asset ? new SerializedObject(asset).FindProperty("sceneOverride").stringValue : level.FindPropertyRelative("sceneOverride").stringValue;
                check(path, "Level " + (i + 1), false);
            }
            return issues;
        }
    }
}
