using System;
using System.IO;
using BaseUnity.Features;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.GameFlow.Editor
{
    public static class UIFeedbackBuilder
    {
        [MenuItem("Tools/BaseUnity/Game Flow/Generate Missing UI Feedback Profile")]
        public static void GenerateSelected()
        {
            var config = Selection.activeObject as GameFlowConfig;
            if (!config) { var ids = AssetDatabase.FindAssets("t:GameFlowConfig", new[] { "Assets" }); if (ids.Length != 1) throw new InvalidOperationException("Select a GameFlowConfig first."); config = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(ids[0])); }
            Generate(config);
        }
        public static void Generate(GameFlowConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (!config || !config.features || config.features.uiFeedbackProfile) return;
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/') + "/UIFeedback";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var profile = AssetDatabase.LoadAssetAtPath<UIFeedbackProfile>(folder + "/UIFeedback.asset");
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<UIFeedbackProfile>();
                profile.rewardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Layer Lab/GUI Pro-CasualGame/ResourcesData/Sprite/Components/UI_Etc/ResourceBar_Icon_Gold.png");
                var cues = (UICue[])Enum.GetValues(typeof(UICue)); profile.sounds = new UIFeedbackProfile.Sound[cues.Length];
                for (int i = 0; i < cues.Length; i++) profile.sounds[i] = new UIFeedbackProfile.Sound { cue = cues[i] };
                AssetDatabase.CreateAsset(profile, folder + "/UIFeedback.asset");
            }
            Undo.RecordObject(config.features, "Assign UI Feedback Profile"); config.features.uiFeedbackProfile = profile;
            EditorUtility.SetDirty(config.features); AssetDatabase.SaveAssetIfDirty(config.features);
        }
    }
}
