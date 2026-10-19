using System;
using UnityEngine;

namespace BaseUnity.Features
{
    [Serializable] public sealed class AssetSettings { public bool enabled; }
    [Serializable] public sealed class PopupSettings { public bool enabled = true; [Min(1)] public int capacity = 32; }
    [Serializable] public sealed class FeedbackSettings
    {
        public bool enabled = true;
        [Min(.1f)] public float toastSeconds = 2.5f;
        public bool uiSounds = true, uiMotion = true;
        public bool uiHover, reducedMotion;
    }
    [Serializable] public sealed class TutorialStep
    {
        public string id, message, target;
        public bool waitForTargetAction, blockOutsideTarget = true;
    }
    [Serializable] public sealed class TutorialDefinition
    {
        public string id; public bool allowSkip = true;
        public TutorialStep[] steps = Array.Empty<TutorialStep>();
    }
    [Serializable] public sealed class TutorialSettings
    {
        public bool enabled; public bool autoStartAtHome; public string saveSlot = "baseunity_tutorial";
        public TutorialDefinition[] tutorials = Array.Empty<TutorialDefinition>();
    }
    public enum HapticKind { Light, Medium, Heavy, Success, Failure }
    [Serializable] public sealed class HapticSettings { public bool enabled; [Min(0)] public float minimumInterval = .08f; }
    [Serializable] public sealed class LocalNotificationSettings
    {
        public bool enabled; public string channelId = "baseunity_reminders", channelName = "Game reminders";
        public string smallIcon;
        public string dailyTitle = "Daily reward", dailyMessage = "Your daily reward is ready!";
        [Min(1)] public int dailyReminderId = 1001;
    }
    public enum StoreProductKind { Consumable, NonConsumable }
    [Serializable] public sealed class StoreProduct
    {
        public string id, androidId, iosId;
        public StoreProductKind kind;
        [Min(0)] public int coins, gems;
        public string entitlement;
    }
    [Serializable] public sealed class PurchaseSettings
    {
        public bool enabled; public string removeAdsEntitlement = "remove_ads";
        public StoreProduct[] products = Array.Empty<StoreProduct>();
    }
}
