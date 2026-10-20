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
                var consent = config.consent;
                if (consent != null && consent.enabled)
                {
                    if (!config.consentProvider) error("Consent: assign a Google UMP provider in Feature Manager > Ads.");
                    if ((!config.consentProvider || config.consentProvider.GetType().FullName == "BaseUnity.Integrations.GoogleUmpProvider") &&
                        FeatureConfigurationUtility.FindType("GoogleMobileAds.Ump.Api.ConsentInformation") == null)
                        error("Consent: Google UMP SDK is missing. Import the official Google Mobile Ads Unity plugin, then install the consent adapter.");
                    if (mobile && string.IsNullOrWhiteSpace(ios ? ads.iosAppId : ads.androidAppId))
                        error("Consent: a real AdMob App ID is required for the privacy message, including MAX mediation.");
                    if (consent.debugMode)
                    {
                        warn("Consent testing is enabled; debug geography and IDs are used only in development builds.");
                        if (consent.testDeviceHashedIds == null || consent.testDeviceHashedIds.Length == 0 || consent.testDeviceHashedIds.Any(string.IsNullOrWhiteSpace))
                            error("Consent testing: add valid UMP test-device hashed IDs.");
                    }
                    if (max)
                    {
                        var type = FeatureConfigurationUtility.FindType("AppLovinMax.Scripts.IntegrationManager.Editor.AppLovinInternalSettings");
                        var instance = type?.GetProperty("Instance")?.GetValue(null);
                        if (instance != null && (bool)(type.GetProperty("ConsentFlowEnabled")?.GetValue(instance) ?? false))
                            error("Consent: disable MAX Terms and Privacy Policy Flow in AppLovin Integration Manager when using BaseUnity's external UMP flow.");
                        if (consent.tagForUnderAgeOfConsent) error("Consent: MAX must not initialize for children. Select an appropriate ad provider and audience flow.");
                    }
                    warn("Consent: publish the privacy message for this AdMob app and include every mediated ad partner. Verify the native flow on a device.");
                }
                else warn("Google consent is disabled. Supply your own CMP before requesting ads where required.");
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
            issues.AddRange(MobileModuleValidator.Validate(config, target));
            var offline = config.offline;
            if (offline != null && offline.enabled)
            {
                if (offline.saveSlot == null || !Regex.IsMatch(offline.saveSlot, @"\A[a-zA-Z0-9_-]{1,64}\z") || (offline.saveSlot == "baseunity_language" || offline.saveSlot == "baseunity_audio")) error("Offline: choose a unique valid save slot (1-64 letters, digits, '-' or '_').");
                if (offline.startingCoins < 0 || offline.startingGems < 0) error("Offline: starting balances must not be negative.");
                if (offline.levelRewards && (offline.winCoins < 0 || offline.firstWinBonus < 0 || (long)offline.winCoins + offline.firstWinBonus > int.MaxValue)) error("Offline: level rewards must be non-negative and fit within an integer.");
                if (offline.dailyReward && (offline.dailyCoins == null || offline.dailyCoins.Length == 0 || offline.dailyCoins.Any(x => x < 0))) error("Daily reward: configure at least one non-negative reward.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                if (offline.shop) foreach (var item in offline.items ?? Array.Empty<ShopItem>())
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id)) { error("Shop: item IDs must be present and unique."); continue; }
                    if (item.price < 0 || item.quantity < 1 || item.purchaseLimit < 0 || !Enum.IsDefined(typeof(Currency), item.currency)) error("Shop: invalid price, quantity, purchase limit or currency for " + item.id);
                }
                ids.Clear();
                if (offline.achievements) foreach (var goal in offline.achievementGoals ?? Array.Empty<DailyMission>())
                {
                    if (goal == null || string.IsNullOrWhiteSpace(goal.id) || !ids.Add(goal.id) || goal.target < 1 || goal.coins < 0 || !Enum.IsDefined(typeof(MissionMetric), goal.metric)) error("Achievements: IDs must be unique with valid target, reward and metric.");
                }
                ids.Clear();
                if (offline.missions) foreach (var mission in offline.dailyMissions ?? Array.Empty<DailyMission>())
                {
                    if (mission == null || string.IsNullOrWhiteSpace(mission.id) || !ids.Add(mission.id)) { error("Missions: IDs must be present and unique."); continue; }
                    if (mission.target < 1 || mission.coins < 0 || !Enum.IsDefined(typeof(MissionMetric), mission.metric)) error("Missions: invalid target, reward or metric for " + mission.id);
                }
            }
            if (config.localization != null && config.localization.enabled && config.localization.defaultLanguage != "en" && config.localization.defaultLanguage != "vi") error("Localization: default language must be en or vi.");
            if (config.gameIntegration != null && config.gameIntegration.interstitialBetweenLevels && config.gameIntegration.interstitialEveryWins < 1) error("Game integration: interstitial cadence must be at least one win.");
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
            Action<string, string[]> checkBindings = (property, fields) => {
                var asset = data.FindProperty(property)?.objectReferenceValue; if (!asset) return;
                var ui = new SerializedObject(asset);
                foreach (string field in fields) if (!ui.FindProperty(field)?.objectReferenceValue) error(property + ": missing binding " + field);
            };
            checkBindings("levelSelectionPrefab", new[] { "completed", "pageNumber", "cells", "cellPrefab", "previous", "next", "back" });
            checkBindings("mobileModulesPrefab", new[] { "content", "menu", "menuPanel", "popup", "tutorial", "toast", "loading", "popupTitle", "popupMessage", "tutorialMessage", "toastMessage", "loadingMessage", "popupAccept", "popupCancel", "tutorialNext", "tutorialSkip", "buttonTemplate", "labelTemplate", "panelTemplate" });
            checkBindings("offlineOverlayPrefab", new[] { "waiting", "home", "result", "balance", "reward", "message", "menu", "retry", "doubleReward" });
            var mobile = data.FindProperty("mobileModulesPrefab")?.objectReferenceValue;
            if (mobile) foreach (string field in new[] { "masks", "borders" })
            {
                var array = new SerializedObject(mobile).FindProperty(field);
                if (array.arraySize != 4) error("Mobile UI: " + field + " requires four bindings.");
                for (int i = 0; i < array.arraySize; i++) if (!array.GetArrayElementAtIndex(i).objectReferenceValue) error("Mobile UI: missing " + field + " " + i);
            }
            var theme = data.FindProperty("uiTheme")?.objectReferenceValue;
            if (theme)
            {
                var styles = new SerializedObject(theme).FindProperty("styles"); var roles = new HashSet<int>();
                for (int i = 0; i < styles.arraySize; i++)
                {
                    var entry = styles.GetArrayElementAtIndex(i); int role = entry.FindPropertyRelative("role").intValue; float scale = entry.FindPropertyRelative("textScale").floatValue;
                    if (!roles.Add(role)) error("UI theme: duplicate semantic role " + role);
                    if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < .5f || scale > 2) error("UI theme: textScale must be finite and between 0.5 and 2.");
                }
                if (styles.arraySize == 0) error("UI theme: configure at least one semantic role.");
            }
            var components = data.FindProperty("uiComponents")?.objectReferenceValue;
            if (components)
            {
                var library = new SerializedObject(components);
                foreach (string field in new[] { "primaryButton", "secondaryButton", "tabButton", "currencyBar", "itemCard", "questCard" })
                    if (!library.FindProperty(field).objectReferenceValue) error("UI component library: missing " + field);
            }
            var menu = data.FindProperty("offlineMenuPrefab")?.objectReferenceValue;
            if (menu)
            {
                var ui = new SerializedObject(menu);
                foreach (string field in new[] { "balance", "message", "languageLabel", "language", "close", "loading", "animatedContent" })
                    if (!ui.FindProperty(field).objectReferenceValue) error("Offline UI: missing binding " + field);
                var tabs = ui.FindProperty("tabs"); var seen = new HashSet<string>();
                for (int i = 0; i < tabs.arraySize; i++)
                {
                    var tab = tabs.GetArrayElementAtIndex(i); string key = tab.FindPropertyRelative("key").stringValue;
                    if (!seen.Add(key)) error("Offline UI: duplicate tab " + key);
                    foreach (string field in new[] { "button", "selected", "panel" })
                        if (!tab.FindPropertyRelative(field).objectReferenceValue) error("Offline UI: " + key + " missing " + field);
                    var panel = tab.FindPropertyRelative("panel").objectReferenceValue;
                    if (!panel) continue;
                    var p = new SerializedObject(panel);
                    foreach (string field in new[] { "title", "empty", "pageLabel", "rows", "rowPrefab", "previous", "next" })
                        if (!p.FindProperty(field).objectReferenceValue) error("Offline UI panel " + key + ": missing " + field);
                    if (p.FindProperty("key").stringValue != key || p.FindProperty("pageSize").intValue < 1) error("Offline UI: invalid panel key/page size for " + key);
                    var row = p.FindProperty("rowPrefab").objectReferenceValue;
                    if (row) foreach (string field in new[] { "title", "action", "actionLabel" })
                        if (!new SerializedObject(row).FindProperty(field).objectReferenceValue) error("Offline UI row: missing " + field);
                }
                foreach (string key in new[] { "SHOP", "DAILY REWARD", "MISSIONS", "INVENTORY" })
                    if (!seen.Contains(key)) error("Offline UI: missing tab " + key);
            }
            var feature = data.FindProperty("features").objectReferenceValue as FeatureConfig;
            if (feature && feature.offline != null && feature.offline.enabled && feature.offline.saveSlot == data.FindProperty("saveSlot").stringValue)
                error("Offline and GameFlow progress must use different save slots.");
            Action<string, string, bool> check = (path, label, required) => {
                if (string.IsNullOrWhiteSpace(path)) { if (required) error(label + ": scene is not assigned."); return; }
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) error(label + ": scene asset is missing: " + path);
                else if (!enabled.Contains(path)) error(label + ": enable this scene in the build scene list: " + path);
            };
            check(data.FindProperty("homeScene").stringValue, "Home", true);
            check(data.FindProperty("inGameScene").stringValue, "InGame", true);
            string saveSlot = data.FindProperty("saveSlot").stringValue;
            if (saveSlot == null || !Regex.IsMatch(saveSlot, @"\A[a-zA-Z0-9_-]{1,64}\z") || saveSlot == "baseunity_audio" || saveSlot == "baseunity_language" || saveSlot == "baseunity_haptics" || saveSlot == "baseunity_notifications" || (feature && feature.tutorial?.enabled == true && saveSlot == feature.tutorial.saveSlot)) error("GameFlow: invalid, reserved or shared tutorial save slot.");
            var levels = data.FindProperty("levels");
            if (levels.arraySize == 0) error("GameFlow: configure at least one level.");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var asset = level.FindPropertyRelative("data").objectReferenceValue;
                var source = asset ? new SerializedObject(asset) : null;
                int target = asset ? source.FindProperty("targetScore").intValue : level.FindPropertyRelative("targetScore").intValue;
                float time = asset ? source.FindProperty("timeLimitSeconds").floatValue : level.FindPropertyRelative("timeLimitSeconds").floatValue;
                if (target < 1 || time < 0 || float.IsNaN(time) || float.IsInfinity(time)) error("Level " + (i + 1) + ": target must be positive and time must be finite and non-negative.");
                string address = asset ? source.FindProperty("addressablePrefab")?.stringValue : level.FindPropertyRelative("addressablePrefab")?.stringValue;
                if (!string.IsNullOrWhiteSpace(address) && (!feature || feature.assets?.enabled != true || !feature.assetProvider)) error("Level " + (i + 1) + ": addressable gameplay requires Assets and an asset provider.");
                string path = asset ? source.FindProperty("sceneOverride").stringValue : level.FindPropertyRelative("sceneOverride").stringValue;
                check(path, "Level " + (i + 1), false);
            }
            return issues;
        }
    }
}
