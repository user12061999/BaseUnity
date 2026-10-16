using BaseUnity.Features;
using UnityEditor;

namespace BaseUnity.Editor
{
    [CustomEditor(typeof(FeatureConfig))]
    public sealed class FeatureConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (UnityEngine.GUILayout.Button("Open Feature Manager")) FeatureManagerWindow.Open((FeatureConfig)target);
            DrawDefaultInspector();
            var config = (FeatureConfig)target;
            if (config.ads.enabled && !config.adsProvider)
                EditorGUILayout.HelpBox("Ads is enabled but no provider is assigned. Import its SDK, install the adapter from Tools > BaseUnity > Features, then create and assign the provider asset.", MessageType.Warning);
            if (config.firebase.enabled && !config.firebaseProvider)
                EditorGUILayout.HelpBox("Firebase is enabled but no provider is assigned. Import Firebase Analytics, Crashlytics and Remote Config SDKs, install the adapter, then assign its provider asset.", MessageType.Warning);
            EditorGUILayout.HelpBox("Settings are copied when Bootstrap starts. Restart Play Mode after editing this asset. Runtime switches use AdsService/FirebaseService.SetEnabled().", MessageType.Info);
        }
    }
}
