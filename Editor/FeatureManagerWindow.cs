using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BaseUnity.Core;
using BaseUnity.Features;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    public sealed class FeatureManagerWindow : EditorWindow
    {
        [SerializeField] private FeatureConfig config;
        private SerializedObject serialized;
        private Vector2 scroll;
        private int tab;
        private string notice;
        private MessageType noticeType;

        [MenuItem("Tools/BaseUnity/Feature Manager")]
        public static void Open()
        {
            var window = GetWindow<FeatureManagerWindow>(true, "BaseUnity • Feature Manager", true);
            window.minSize = new Vector2(560, 600);
            window.FindConfig(); window.Show();
        }
        public static void Open(FeatureConfig value) { Open(); GetWindow<FeatureManagerWindow>().Select(value); }
        private void OnEnable() { FindConfig(); }
        private void Select(FeatureConfig value) { config = value; serialized = value ? new SerializedObject(value) : null; Repaint(); }
        private void FindConfig()
        {
            if (config) { Select(config); return; }
            if (Selection.activeObject is FeatureConfig selected) { Select(selected); return; }
            var candidates = AssetDatabase.FindAssets("t:FeatureConfig", new[] { "Assets" });
            if (candidates.Length == 1) Select(AssetDatabase.LoadAssetAtPath<FeatureConfig>(AssetDatabase.GUIDToAssetPath(candidates[0])));
        }
        private void OnGUI()
        {
            float oldWidth = EditorGUIUtility.labelWidth;
            try { EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.38f, 200, 290); DrawGUI(); }
            finally { EditorGUIUtility.labelWidth = oldWidth; }
        }
        private void DrawGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("BASE UNITY", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Ads & Firebase configuration", EditorStyles.largeLabel);
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                var chosen = (FeatureConfig)EditorGUILayout.ObjectField("Configuration", config, typeof(FeatureConfig), false);
                if (chosen != config) Select(chosen);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("New", GUILayout.Width(60))) { FeatureSetupMenu.CreateConfig(); Select(Selection.activeObject as FeatureConfig); }
                if (GUILayout.Button("Find", GUILayout.Width(60))) FindConfig();
            }
            if (!config) { EditorGUILayout.HelpBox("Choose a FeatureConfig asset or create a new one. No SDK is needed to configure features.", MessageType.Info); return; }
            if (serialized == null || serialized.targetObject != config) serialized = new SerializedObject(config);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorGUILayout.HelpBox("Stop Play Mode to edit startup configuration.", MessageType.Info);
            tab = GUILayout.Toolbar(tab, new[] { "Ads", "Firebase", "Project Setup" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                serialized.Update();
                if (tab == 0) DrawAds(); else if (tab == 1) DrawFirebase(); else DrawSetup();
                serialized.ApplyModifiedProperties();
            }
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, noticeType);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Changes support Undo. Restart Play to use saved settings.", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Save", GUILayout.Width(90))) Run(() => { serialized.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(config); return "Configuration saved."; });
            }
            EditorGUILayout.Space(8);
        }
        private SerializedProperty Field(string path, string label, bool children = false)
        {
            var property = serialized.FindProperty(path);
            EditorGUILayout.PropertyField(property, new GUIContent(label), children); return property;
        }
        private void Header(string title) { EditorGUILayout.Space(12); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); }
        private void DrawAds()
        {
            Header("Advertising"); Field("ads.enabled", "Enable Ads");
            EditorGUILayout.HelpBox("You can enter IDs while Ads is off. App IDs contain '~'; AdMob ad-unit IDs contain '/'.", MessageType.Info);
            var network = serialized.FindProperty("ads.network");
            EditorGUI.BeginChangeCheck(); EditorGUILayout.PropertyField(network, new GUIContent("Network"));
            if (EditorGUI.EndChangeCheck()) { serialized.FindProperty("adsProvider").objectReferenceValue = null; notice = "Provider cleared. Assign the new provider and check the ad-unit IDs for this network."; noticeType = MessageType.Info; }
            bool max = network.enumValueIndex == (int)AdsNetwork.AppLovinMax;
            Field("adsProvider", "Provider asset");
            string typeName = max ? "BaseUnity.Integrations.AppLovinMaxProvider" : "BaseUnity.Integrations.AdMobProvider";
            DrawProvider(typeName, "adsProvider", max ? (Action)FeatureSetupMenu.InstallMax : FeatureSetupMenu.InstallAdMob);
            var provider = serialized.FindProperty("adsProvider").objectReferenceValue;
            if (provider && provider.GetType().FullName != typeName) EditorGUILayout.HelpBox("Provider does not match the selected network.", MessageType.Warning);
            Field("ads.testMode", "Use test ads");
            Header("App configuration");
            if (max) Field("ads.maxSdkKey", "MAX SDK key");
            Field("ads.androidAppId", max ? "AdMob Android App ID (mediation)" : "Android App ID");
            Field("ads.iosAppId", max ? "AdMob iOS App ID (mediation)" : "iOS App ID");
            if (GUILayout.Button("Apply app IDs / SDK key to installed SDK"))
            {
                serialized.ApplyModifiedProperties();
                Run(() => FeatureConfigurationUtility.ApplyAdsSdkSettings(config));
            }
            Header("Ad formats and unit IDs");
            Field("ads.banner", "Banner"); Field("ads.interstitial", "Interstitial"); Field("ads.rewarded", "Rewarded");
            Header("Android"); Field("ads.androidBanner", "Banner unit ID"); Field("ads.androidInterstitial", "Interstitial unit ID"); Field("ads.androidRewarded", "Rewarded unit ID");
            Header("iOS"); Field("ads.iosBanner", "Banner unit ID"); Field("ads.iosInterstitial", "Interstitial unit ID"); Field("ads.iosRewarded", "Rewarded unit ID");
            Header("Timing"); Field("ads.requestTimeoutSeconds", "Request timeout (seconds)"); Field("ads.interstitialCooldownSeconds", "Interstitial cooldown (seconds)");
            if (max && provider && provider.GetType().FullName == typeName)
            {
                Header("MAX test devices");
                var providerSettings = new SerializedObject(provider); providerSettings.Update();
                var devices = providerSettings.FindProperty("testDeviceAdvertisingIds");
                if (devices != null) EditorGUILayout.PropertyField(devices, new GUIContent("Advertising IDs"), true);
                if (providerSettings.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(provider);
            }
        }
        private void DrawFirebase()
        {
            Header("Firebase"); Field("firebase.enabled", "Enable Firebase");
            Field("firebaseProvider", "Provider asset");
            DrawProvider("BaseUnity.Integrations.FirebaseSdkProvider", "firebaseProvider", FeatureSetupMenu.InstallFirebase);
            Field("firebase.analytics", "Analytics"); Field("firebase.crashlytics", "Crashlytics"); Field("firebase.remoteConfig", "Remote Config");
            Header("Firebase project / app IDs");
            EditorGUILayout.HelpBox("Import the configuration downloaded from Firebase Console. It supplies Project ID, App ID and API key together; an ID alone is not enough to configure the native SDK.", MessageType.Info);
            DrawFirebaseFile(false); DrawFirebaseFile(true);
            Header("Remote Config"); Field("firebase.requestTimeoutSeconds", "Request timeout (seconds)");
            Field("firebase.minimumFetchIntervalSeconds", "Minimum fetch interval (seconds)");
            Field("firebase.defaults", "Local default values", true);
        }
        private void DrawFirebaseFile(bool ios)
        {
            string name = ios ? "GoogleService-Info.plist" : "google-services.json";
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(ios ? "iOS" : "Android", EditorStyles.boldLabel);
                if (GUILayout.Button("Import " + name))
                {
                    string selected = EditorUtility.OpenFilePanel("Select " + name, "", ios ? "plist" : "json");
                    if (!string.IsNullOrEmpty(selected)) Run(() => FeatureConfigurationUtility.ImportFirebaseFile(selected, ios));
                }
                string path = "Assets/" + name;
                if (File.Exists(path))
                {
                    try
                    {
                        var info = FeatureConfigurationUtility.ReadFirebaseInfo(File.ReadAllText(path), ios);
                        EditorGUILayout.LabelField("Project ID", info.projectId);
                        EditorGUILayout.LabelField("App ID", info.appId);
                        EditorGUILayout.LabelField(ios ? "Bundle ID" : "Package name", info.applicationId);
                    }
                    catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Warning); }
                }
                else EditorGUILayout.LabelField("No configuration imported yet.", EditorStyles.miniLabel);
            }
        }
        private void DrawProvider(string typeName, string property, Action install)
        {
            var type = FeatureConfigurationUtility.FindType(typeName);
            EditorGUILayout.LabelField("Adapter", type == null ? "Not installed" : "Installed");
            if (type == null)
            { if (GUILayout.Button("Install adapter (requires SDK)")) Run(() => { install(); return "Adapter copied. Wait for compilation, then create a provider asset."; }); }
            else if (GUILayout.Button("Create and assign provider asset"))
            {
                serialized.ApplyModifiedProperties();
                Run(() => {
                    string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/');
                    var asset = CreateInstance(type);
                    string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + type.Name + ".asset");
                    AssetDatabase.CreateAsset(asset, path);
                    serialized.Update(); serialized.FindProperty(property).objectReferenceValue = asset;
                    serialized.ApplyModifiedProperties(); AssetDatabase.SaveAssets(); return "Provider created and assigned.";
                });
            }
        }
        private void DrawSetup()
        {
            Header("Application identifiers");
            EditorGUILayout.LabelField("Android package", FeatureConfigurationUtility.ApplicationId(false));
            EditorGUILayout.LabelField("iOS bundle", FeatureConfigurationUtility.ApplicationId(true));
            if (GUILayout.Button("Open Player Settings")) SettingsService.OpenProjectSettings("Project/Player");
            Header("Connect this configuration");
            EditorGUILayout.HelpBox("Choose a GameFlowConfig asset or a scene Bootstrap in the Project/Hierarchy, then click Assign. This does not change other configs.", MessageType.Info);
            if (GUILayout.Button("Assign to selected GameFlowConfig / Bootstrap")) Run(() => {
                serialized.ApplyModifiedProperties(); return FeatureConfigurationUtility.Assign(config, Selection.activeObject);
            });
            Header("SDK downloads and setup");
            if (GUILayout.Button("AdMob Unity setup")) Application.OpenURL("https://developers.google.com/admob/unity/quick-start");
            if (GUILayout.Button("AppLovin MAX Unity setup")) Application.OpenURL("https://support.applovin.com/en/max/unity/overview/integration");
            if (GUILayout.Button("Firebase Unity setup")) Application.OpenURL("https://firebase.google.com/docs/unity/setup");
            EditorGUILayout.HelpBox("Save stores BaseUnity settings. Apply app IDs / SDK key updates the vendor settings asset separately. Firebase import copies the official config into Assets; replacing a file creates a backup under ProjectSettings/BaseUnityConfigBackups.", MessageType.Info);
        }
        private void Run(Func<string> action)
        { try { notice = action(); noticeType = MessageType.Info; } catch (Exception error) { notice = error.GetBaseException().Message; noticeType = MessageType.Error; } }
    }
}
