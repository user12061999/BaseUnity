using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    [Serializable]
    public sealed class BuildValidationPolicy
    {
        public bool enabled = true;
        public bool blockTestAdsInRelease = true;
        public bool warningsAsErrors;
    }

    [FilePath("ProjectSettings/BaseUnityBuildValidation.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class BaseUnityBuildSettings : ScriptableSingleton<BaseUnityBuildSettings>
    {
        [SerializeField] private BuildValidationPolicy policy = new BuildValidationPolicy();
        public BuildValidationPolicy Snapshot => JsonUtility.FromJson<BuildValidationPolicy>(JsonUtility.ToJson(policy));
        [SettingsProvider]
        public static SettingsProvider CreateProvider() => new SettingsProvider("Project/BaseUnity/Build Validation", SettingsScope.Project)
        {
            label = "Build Validation",
            guiHandler = _ => {
                var data = new SerializedObject(instance); data.Update();
                EditorGUILayout.LabelField("Automatic build validation", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Checks saved scenes and the configs referenced by their BaseUnity bootstraps, using the current build's scene list. Errors stop the build. No SDK is initialized.", MessageType.Info);
                EditorGUILayout.PropertyField(data.FindProperty("policy.enabled"), new GUIContent("Validate before build"));
                if (data.FindProperty("policy.enabled").boolValue)
                {
                    EditorGUILayout.PropertyField(data.FindProperty("policy.blockTestAdsInRelease"), new GUIContent("Block test ads in Release"));
                    EditorGUILayout.PropertyField(data.FindProperty("policy.warningsAsErrors"), new GUIContent("Treat warnings as errors"));
                }
                if (data.ApplyModifiedProperties()) instance.Save(true);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Last automatic report: Library/BaseUnity/LastBuildValidation.json", EditorStyles.wordWrappedLabel);
                using (new EditorGUI.DisabledScope(!File.Exists("Library/BaseUnity/LastBuildValidation.json")))
                    if (GUILayout.Button("Show last automatic report")) EditorUtility.RevealInFinder(Path.GetFullPath("Library/BaseUnity/LastBuildValidation.json"));
            }
        };
    }
}
