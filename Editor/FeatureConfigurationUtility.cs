using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using BaseUnity.Core;
using BaseUnity.Features;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BaseUnity.Editor
{
    public static class FeatureConfigurationUtility
    {
        public sealed class FirebaseInfo { public string projectId, appId, applicationId; }
        [Serializable] private sealed class AndroidConfig { public Project project_info; public Client[] client; }
        [Serializable] private sealed class Project { public string project_id; }
        [Serializable] private sealed class Client { public ClientInfo client_info; }
        [Serializable] private sealed class ClientInfo { public string mobilesdk_app_id; public AndroidInfo android_client_info; }
        [Serializable] private sealed class AndroidInfo { public string package_name; }

        public static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);
        public static string ApplicationId(bool ios) => PlayerSettings.GetApplicationIdentifier(ios ? NamedBuildTarget.iOS : NamedBuildTarget.Android);
        public static FirebaseInfo ReadFirebaseInfo(string text, bool ios, string expectedApplication = null)
        {
            var info = new FirebaseInfo();
            if (ios)
            {
                var xml = new XmlDocument { XmlResolver = null };
                using (var input = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null })) xml.Load(input);
                var keys = xml.SelectNodes("/plist/dict/key");
                foreach (XmlNode key in keys)
                {
                    var value = key.NextSibling;
                    while (value != null && value.NodeType != XmlNodeType.Element) value = value.NextSibling;
                    if (value == null || value.Name != "string") continue;
                    if (key.InnerText == "PROJECT_ID") info.projectId = value.InnerText;
                    if (key.InnerText == "GOOGLE_APP_ID") info.appId = value.InnerText;
                    if (key.InnerText == "BUNDLE_ID") info.applicationId = value.InnerText;
                }
            }
            else
            {
                var json = JsonUtility.FromJson<AndroidConfig>(text);
                var clients = json?.client ?? Array.Empty<Client>();
                var client = string.IsNullOrEmpty(expectedApplication) ? clients.FirstOrDefault() :
                    clients.FirstOrDefault(c => c?.client_info?.android_client_info?.package_name == expectedApplication);
                if (client == null) throw new InvalidDataException("Firebase JSON has no client for " + (expectedApplication ?? "this app") + ". Check Android package name in Player Settings.");
                info.projectId = json.project_info?.project_id;
                info.appId = client.client_info?.mobilesdk_app_id;
                info.applicationId = client.client_info?.android_client_info?.package_name;
            }
            if (string.IsNullOrWhiteSpace(info.projectId) || string.IsNullOrWhiteSpace(info.appId) || string.IsNullOrWhiteSpace(info.applicationId))
                throw new InvalidDataException("Invalid Firebase app configuration: Project ID, App ID or package/bundle ID is missing.");
            if (!string.IsNullOrEmpty(expectedApplication) && info.applicationId != expectedApplication)
                throw new InvalidDataException("Firebase app identifier does not match Player Settings (" + expectedApplication + ").");
            return info;
        }
        public static string ImportFirebaseFile(string source, bool ios)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before importing config.");
            string content = File.ReadAllText(source);
            var info = ReadFirebaseInfo(content, ios, ApplicationId(ios));
            string destination = "Assets/" + (ios ? "GoogleService-Info.plist" : "google-services.json");
            var duplicates = Directory.GetFiles("Assets", Path.GetFileName(destination), SearchOption.AllDirectories)
                .Where(path => Path.GetFullPath(path) != Path.GetFullPath(destination)).ToArray();
            if (duplicates.Length > 0) throw new InvalidOperationException("Another Firebase config already exists: " + duplicates[0] + ". Move it to Assets root before importing to avoid duplicate app configurations.");
            if (File.Exists(destination) && File.ReadAllText(destination) == content) return "This Firebase configuration is already imported.";
            if (File.Exists(destination))
            {
                const string backups = "ProjectSettings/BaseUnityConfigBackups";
                Directory.CreateDirectory(backups);
                File.Copy(destination, backups + "/" + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".backup");
            }
            File.WriteAllText(destination, content);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
            return "Imported " + info.projectId + " / " + info.applicationId + ".";
        }
        public static string ApplyAdsSdkSettings(FeatureConfig config)
        {
            if (!config) throw new ArgumentNullException(nameof(config));
            ValidateAppId(config.ads.androidAppId); ValidateAppId(config.ads.iosAppId);
            bool max = config.ads.network == AdsNetwork.AppLovinMax;
            var type = FindType(max ? "AppLovinSettings" : "GoogleMobileAds.Editor.GoogleMobileAdsSettings");
            if (type == null) throw new InvalidOperationException("SDK settings are unavailable. Install the selected SDK first. Your BaseUnity IDs can still be saved.");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var target = (UnityEngine.Object)(max ? type.GetProperty("Instance", flags)?.GetValue(null) : type.GetMethod("LoadInstance", flags)?.Invoke(null, null));
            if (!target) throw new InvalidOperationException("This SDK version has an unsupported settings API. Use the vendor settings window.");
            var properties = new SerializedObject(target);
            var android = properties.FindProperty("adMobAndroidAppId");
            var ios = properties.FindProperty(max ? "adMobIosAppId" : "adMobIOSAppId");
            var sdkKey = max ? properties.FindProperty("sdkKey") : null;
            if (android == null || ios == null || (max && sdkKey == null)) throw new InvalidOperationException("Unsupported SDK settings fields; no IDs were changed.");
            // Empty drafts do not erase IDs previously configured in the vendor's settings.
            if (!string.IsNullOrWhiteSpace(config.ads.androidAppId)) android.stringValue = config.ads.androidAppId.Trim();
            if (!string.IsNullOrWhiteSpace(config.ads.iosAppId)) ios.stringValue = config.ads.iosAppId.Trim();
            if (max && !string.IsNullOrWhiteSpace(config.ads.maxSdkKey)) sdkKey.stringValue = config.ads.maxSdkKey.Trim();
            properties.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(target); AssetDatabase.SaveAssetIfDirty(config);
            return "Non-empty app IDs / SDK key applied to SDK settings. Empty fields left existing SDK values unchanged.";
        }
        public static void ValidateAppId(string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && !Regex.IsMatch(id.Trim(), @"^ca-app-pub-\d+~\d+$"))
                throw new InvalidDataException("AdMob App ID must look like ca-app-pub-...~... . Ad-unit IDs with '/' belong in the Banner/Interstitial/Rewarded fields.");
        }
        public static string Assign(FeatureConfig config, UnityEngine.Object selected)
        {
            if (selected is GameObject go) selected = go.GetComponent<GameBootstrap>();
            if (!selected || selected == config) throw new InvalidOperationException("Select a GameFlowConfig asset or scene Bootstrap first.");
            var target = new SerializedObject(selected);
            // GameFlow's asset-level config takes precedence over the inherited Bootstrap field.
            if (selected is GameBootstrap)
            {
                var flowConfig = target.FindProperty("config")?.objectReferenceValue;
                if (flowConfig && flowConfig.GetType().FullName == "BaseUnity.GameFlow.GameFlowConfig")
                { selected = flowConfig; target = new SerializedObject(selected); }
            }
            var property = selected is GameBootstrap ? target.FindProperty("featureConfig") :
                selected.GetType().FullName == "BaseUnity.GameFlow.GameFlowConfig" ? target.FindProperty("features") : null;
            if (property == null) throw new InvalidOperationException("Selected object is not a GameFlowConfig or Bootstrap.");
            property.objectReferenceValue = config; target.ApplyModifiedProperties();
            if (EditorUtility.IsPersistent(selected)) AssetDatabase.SaveAssetIfDirty(selected);
            return selected is GameBootstrap ? "Assigned to Bootstrap. Save the scene to persist the change." : "Assigned to GameFlowConfig.";
        }
    }
}
