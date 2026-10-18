using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace BaseUnity.Editor
{
    public static class MobileModuleSetupMenu
    {
        public const string Addressables = "com.unity.addressables@4.1.1";
        public const string Notifications = "com.unity.mobile.notifications@2.5.0";
        public const string Purchasing = "com.unity.purchasing@5.4.4";
        private static AddAndRemoveRequest request;
        [MenuItem("Tools/BaseUnity/Integrations/Install Mobile Module Packages")]
        public static void InstallAll() => Install(Addressables, Notifications, Purchasing);
        [MenuItem("Tools/BaseUnity/Integrations/Install Addressables")]
        public static void InstallAssets() => Install(Addressables);
        [MenuItem("Tools/BaseUnity/Integrations/Install Mobile Notifications")]
        public static void InstallNotifications() => Install(Notifications);
        [MenuItem("Tools/BaseUnity/Integrations/Install Unity IAP")]
        public static void InstallPurchasing() => Install(Purchasing);
        [MenuItem("Tools/BaseUnity/Integrations/Use Explicit Notification Permission")]
        public static void ConfigureNotificationPermission()
        {
            var type = FeatureConfigurationUtility.FindType("Unity.Notifications.NotificationSettings+iOSSettings");
            if (type == null) throw new InvalidOperationException("Install Mobile Notifications first.");
            type.GetProperty("RequestAuthorizationOnAppLaunch")?.SetValue(null, false);
            type.GetProperty("NotificationRequestAuthorizationForRemoteNotificationsOnAppLaunch")?.SetValue(null, false);
        }
        private static void Install(params string[] packages)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (request != null && !request.IsCompleted) return;
            request = Client.AddAndRemove(packages);
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
        }
        private static void Poll()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (request.Status != StatusCode.Success) Debug.LogError("Mobile module package installation failed: " + request.Error?.message);
            else Debug.Log("BaseUnity mobile packages installed. Install their adapters from Feature Manager > Modules.");
            request = null;
        }
    }
}
