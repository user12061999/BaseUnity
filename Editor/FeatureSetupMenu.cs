using System;
using System.IO;
using System.Linq;
using BaseUnity.Features;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    public static class FeatureSetupMenu
    {
        [MenuItem("Tools/BaseUnity/Features/Create Feature Config")]
        public static void CreateConfig()
        {
            string directory = "Assets/BaseUnitySettings";
            Directory.CreateDirectory(directory); AssetDatabase.Refresh();
            var config = ScriptableObject.CreateInstance<FeatureConfig>();
            var path = AssetDatabase.GenerateUniqueAssetPath(directory + "/FeatureConfig.asset");
            AssetDatabase.CreateAsset(config, path); AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log("Assign " + path + " to Bootstrap.FeatureConfig or GameFlowConfig.features. Ads and Firebase are off by default.");
        }
        [MenuItem("Tools/BaseUnity/Features/Install AdMob Adapter")]
        public static void InstallAdMob() => Install("AdMob", "GoogleMobileAds.Api.MobileAds");
        [MenuItem("Tools/BaseUnity/Features/Install AppLovin MAX Adapter")]
        public static void InstallMax() => Install("AppLovinMax", "MaxSdk");
        [MenuItem("Tools/BaseUnity/Features/Install Firebase Adapter")]
        public static void InstallFirebase() => Install("Firebase", "Firebase.FirebaseApp", "Firebase.Analytics.FirebaseAnalytics", "Firebase.Crashlytics.Crashlytics", "Firebase.RemoteConfig.FirebaseRemoteConfig");

        [MenuItem("Tools/BaseUnity/Features/Install Google Consent Adapter")]
        public static void InstallGoogleConsent() => Install("GoogleConsent", "GoogleMobileAds.Ump.Api.ConsentInformation");

        [MenuItem("Tools/BaseUnity/Features/Install Addressables Adapter")]
        public static void InstallAddressables() => Install("Addressables", "UnityEngine.AddressableAssets.Addressables");
        [MenuItem("Tools/BaseUnity/Features/Install Mobile Notifications Adapter")]
        public static void InstallNotifications() => Install("MobileNotifications", "Unity.Notifications.NotificationCenter");
        [MenuItem("Tools/BaseUnity/Features/Install Mobile Haptics Adapter")]
        public static void InstallHaptics() => Install("MobileHaptics");
        [MenuItem("Tools/BaseUnity/Features/Install Unity IAP Adapter")]
        public static void InstallPurchasing() => Install("UnityPurchasing", "UnityEngine.Purchasing.StoreController");
        [MenuItem("Tools/BaseUnity/Features/Install Server Receipt Verifier")]
        public static void InstallPurchaseVerifier() => Install("PurchaseVerification");
        private static void Install(string provider, params string[] requiredTypes)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            foreach (string name in requiredTypes)
                if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(name, false) != null))
                    throw new InvalidOperationException("Import the official SDK first. Missing type: " + name + ". See Samples~/Integrations/README.md.");
            string targetRoot = "Assets/BaseUnityIntegrations/" + provider;
            if (Directory.Exists(targetRoot)) throw new InvalidOperationException(targetRoot + " already exists; existing integration code is never overwritten.");
            string source = null;
            foreach (string guid in AssetDatabase.FindAssets("FeatureSetupMenu t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "../Samples~/Integrations", provider));
                if (Directory.Exists(candidate)) { source = candidate; break; }
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                if (package != null)
                {
                    candidate = Path.Combine(package.resolvedPath, "Samples~/Integrations", provider);
                    if (Directory.Exists(candidate)) { source = candidate; break; }
                }
            }
            if (source == null && Directory.Exists("Samples~/Integrations/" + provider)) source = Path.GetFullPath("Samples~/Integrations/" + provider);
            if (source == null) throw new DirectoryNotFoundException("Cannot find integration source for " + provider);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Path.Combine(targetRoot, file.Substring(source.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target, false);
            }
            AssetDatabase.Refresh();
            Debug.Log(provider + " adapter installed. After compilation create its provider asset via Assets > Create > BaseUnity > Providers, then assign it in FeatureConfig.");
        }
    }
}
