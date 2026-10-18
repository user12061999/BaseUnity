using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BaseUnity.Features;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    public static class MobileModuleValidator
    {
        public static List<FeatureValidationIssue> Validate(FeatureConfig config, BuildTarget target)
        {
            var issues = new List<FeatureValidationIssue>(); if (!config) return issues;
            Action<string> error = text => issues.Add(new FeatureValidationIssue(MessageType.Error, text, config));
            Action<string> warn = text => issues.Add(new FeatureValidationIssue(MessageType.Warning, text, config));
            if (config.assets?.enabled == true)
            {
                if (!config.assetProvider) error("Assets: assign an Addressables provider.");
                if (FeatureConfigurationUtility.FindType("UnityEngine.AddressableAssets.Addressables") == null) error("Assets: install the Addressables package before its adapter.");
                warn("Assets: build the Addressables content catalog and verify local/remote addresses for the target player.");
            }
            if (config.popups?.enabled == true && config.popups.capacity < 1) error("Popups: capacity must be positive.");
            if (config.feedback?.enabled == true)
            {
                if (config.feedback.toastSeconds <= 0 || float.IsNaN(config.feedback.toastSeconds) || float.IsInfinity(config.feedback.toastSeconds)) error("Feedback: toast duration must be finite and positive.");
                if (config.popups?.enabled != true) warn("Feedback: confirmation dialogs need the popup queue enabled.");
            }
            var reserved = new[] { "baseunity_audio", "baseunity_language", "baseunity_haptics", "baseunity_notifications" };
            if (config.offline?.enabled == true && reserved.Contains(config.offline.saveSlot)) error("Offline: save slot is reserved by a core module.");
            if (config.tutorial?.enabled == true)
            {
                string slot = config.tutorial.saveSlot;
                if (slot == null || !Regex.IsMatch(slot, @"\A[a-zA-Z0-9_-]{1,64}\z") || reserved.Contains(slot) || slot == config.offline?.saveSlot) error("Tutorial: choose a valid unique save slot.");
                var ids = new HashSet<string>();
                foreach (var tutorial in config.tutorial.tutorials ?? Array.Empty<TutorialDefinition>())
                {
                    if (tutorial == null || string.IsNullOrWhiteSpace(tutorial.id) || !ids.Add(tutorial.id)) { error("Tutorial: IDs must be non-empty and unique."); continue; }
                    var steps = tutorial.steps ?? Array.Empty<TutorialStep>();
                    if (steps.Length == 0 || steps.Any(x => x == null || string.IsNullOrWhiteSpace(x.id)) || steps.Where(x => x != null).Select(x => x.id).Distinct().Count() != steps.Length) error("Tutorial: configure unique step IDs for " + tutorial.id);
                    if (steps.Any(x => x != null && x.waitForTargetAction && string.IsNullOrWhiteSpace(x.target))) error("Tutorial: action steps need a target key.");
                }
            }
            if (config.haptics?.enabled == true)
            {
                if (!config.hapticProvider) error("Haptics: assign a mobile haptics provider.");
                float interval = config.haptics.minimumInterval;
                if (interval < 0 || float.IsNaN(interval) || float.IsInfinity(interval)) error("Haptics: interval must be finite and non-negative.");
            }
            if (config.notifications?.enabled == true)
            {
                if (!config.notificationProvider) error("Notifications: assign a mobile notification provider.");
                if (FeatureConfigurationUtility.FindType("Unity.Notifications.NotificationCenter") == null) error("Notifications: install Mobile Notifications 2.5 or newer.");
                if (string.IsNullOrWhiteSpace(config.notifications.channelId) || string.IsNullOrWhiteSpace(config.notifications.channelName)) error("Notifications: configure an Android channel ID/name.");
                if (config.notifications.dailyReminderId <= 0 || string.IsNullOrWhiteSpace(config.notifications.dailyTitle)) error("Notifications: daily reminder needs a positive ID and a title.");
                var type = FeatureConfigurationUtility.FindType("Unity.Notifications.NotificationSettings+iOSSettings");
                if (target == BuildTarget.iOS && type != null && (bool)(type.GetProperty("RequestAuthorizationOnAppLaunch")?.GetValue(null) ?? false)) error("Notifications: disable iOS authorization on launch using the Modules configuration button; request it from a player action.");
                warn("Notifications: configure Android notification icons in Project Settings and verify permissions/delivery on a device.");
            }
            if (config.purchasing?.enabled == true)
            {
                if (config.offline?.enabled != true) error("IAP: enable the Offline wallet for atomic rewards and purchase receipts.");
                if (!config.purchaseProvider) error("IAP: assign a Unity IAP store provider.");
                if (!config.purchaseVerifier) error("IAP: assign a trusted receipt verifier. Purchases are never granted without verification.");
                if (FeatureConfigurationUtility.FindType("UnityEngine.Purchasing.StoreController") == null) error("IAP: install Unity IAP 5 before its adapter.");
                if (config.purchaseVerifier && config.purchaseVerifier.GetType().FullName == "BaseUnity.Integrations.ServerPurchaseVerifier")
                {
                    var property = new SerializedObject(config.purchaseVerifier).FindProperty("endpoint");
                    if (!Uri.TryCreate(property?.stringValue, UriKind.Absolute, out var uri) || uri.Scheme != "https") error("IAP: configure an HTTPS endpoint on the server receipt verifier asset.");
                }
                var ids = new HashSet<string>(); var storeIds = new HashSet<string>();
                var products = config.purchasing.products ?? Array.Empty<StoreProduct>();
                if (products.Length == 0) error("IAP: configure at least one product.");
                foreach (var product in products)
                {
                    if (product == null || string.IsNullOrWhiteSpace(product.id) || !ids.Add(product.id)) { error("IAP: logical product IDs must be present and unique."); continue; }
                    if (product.coins < 0 || product.gems < 0 || !Enum.IsDefined(typeof(StoreProductKind), product.kind)) error("IAP: invalid product rewards/type: " + product.id);
                    if (product.kind == StoreProductKind.Consumable && !string.IsNullOrWhiteSpace(product.entitlement)) error("IAP: persistent entitlements require a non-consumable product.");
                    string storeId = target == BuildTarget.iOS ? product.iosId : product.androidId;
                    if (string.IsNullOrWhiteSpace(storeId) || !storeIds.Add(storeId)) error("IAP: target store IDs must be non-empty and unique.");
                }
                warn("IAP: configure matching products in Play Console/App Store Connect and test using store sandbox accounts. Verify the server binds receipts to the app/player/product and handles refunds.");
            }
            return issues;
        }
    }
}
