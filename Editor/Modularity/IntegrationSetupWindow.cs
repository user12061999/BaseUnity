using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace BaseUnity.Modularity
{
    public sealed class IntegrationSetupWindow : EditorWindow
    {
        [SerializeField] private GameObject root;
        private Vector2 scroll;
        [MenuItem("Tools/BaseUnity/Modular Integration Setup")]
        public static void Open() { var window=GetWindow<IntegrationSetupWindow>("Integration Setup");window.root=Selection.activeGameObject;window.Show(); }
        private static void Field(SerializedObject data,string path) { var value=data.FindProperty(path);if(value!=null)EditorGUILayout.PropertyField(value,true); }
        private void OnGUI()
        {
            root=(GameObject)EditorGUILayout.ObjectField("Bootstrap",root,typeof(GameObject),true);
            EditorGUILayout.HelpBox("Select a modular bootstrap. Providers are explicit assets; SDK App IDs/keys must also be configured in the official SDK settings. Disabled features hide their fields.",MessageType.Info);
            if(!root)return;
            if(EditorApplication.isPlayingOrWillChangePlaymode) { EditorGUILayout.HelpBox("Stop Play mode to change startup integration settings.",MessageType.Info);return; }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            var modules=root.GetComponents<Component>().Where(c=>c&&c.GetType().Namespace=="BaseUnity.IntegrationModules"&&c.GetType().Name.EndsWith("Module",StringComparison.Ordinal)).ToArray();
            if(modules.Length==0)EditorGUILayout.HelpBox("Install the selected integration facade modules and create a bootstrap first.",MessageType.Info);
            foreach(var component in modules)using(var data=new SerializedObject(component)) {
                data.Update();string name=component.GetType().Name;EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField(name,EditorStyles.boldLabel);
                Field(data,"m_Enabled");Field(data,"settings.enabled");
                if(data.FindProperty("m_Enabled").boolValue&&data.FindProperty("settings.enabled").boolValue) {
                    Field(data,"provider");Field(data,"initializeOnStart");
                    if(name=="AdsModule") {
                        Field(data,"settings.network");Field(data,"settings.testMode");
                        bool max=data.FindProperty("settings.network").enumValueIndex==1;
                        if(max)Field(data,"settings.maxSdkKey");else { Field(data,"settings.androidAppId");Field(data,"settings.iosAppId"); }
                        foreach(string format in new[]{"banner","interstitial","rewarded"}) {
                            Field(data,"settings."+format);if(data.FindProperty("settings."+format).boolValue) {
                                string title=char.ToUpperInvariant(format[0])+format.Substring(1);Field(data,"settings.android"+title);Field(data,"settings.ios"+title);
                            }
                        }
                        Field(data,"settings.interstitialCooldownSeconds");Field(data,"settings.requestTimeoutSeconds");
                    } else if(name=="FirebaseModule") {
                        Field(data,"settings.analytics");Field(data,"settings.crashlytics");Field(data,"settings.remoteConfig");
                        if(data.FindProperty("settings.remoteConfig").boolValue) { Field(data,"settings.defaults");Field(data,"settings.minimumFetchIntervalSeconds"); }
                        Field(data,"settings.requestTimeoutSeconds");
                    } else {
                        Field(data,"settings.tagForUnderAgeOfConsent");Field(data,"settings.debugMode");
                        if(data.FindProperty("settings.debugMode").boolValue) { Field(data,"settings.debugRegion");Field(data,"settings.testDeviceHashedIds"); }
                    }
                }
                data.ApplyModifiedProperties();EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            if(GUILayout.Button("Validate enabled build scenes"))ModularBuildValidator.ValidateMenu();
        }
    }
}
