using System.Collections.Generic;
using System.Linq;
using BaseUnity.Features;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    public sealed class FeatureValidationWindow : EditorWindow
    {
        [SerializeField] private FeatureConfig config;
        [SerializeField] private Object flow;
        [SerializeField] private BuildTarget target = BuildTarget.Android;
        private List<FeatureValidationIssue> issues;
        private Vector2 scroll;

        [MenuItem("Tools/BaseUnity/Validate Build Configuration")]
        public static void Open() { Open(Selection.activeObject as FeatureConfig); }
        public static void Open(FeatureConfig value)
        {
            var window = GetWindow<FeatureValidationWindow>("BaseUnity Validation");
            window.minSize = new Vector2(580, 420);
            window.config = value;
            if (!value)
            {
                var configs = AssetDatabase.FindAssets("t:FeatureConfig");
                if (configs.Length == 1) window.config = AssetDatabase.LoadAssetAtPath<FeatureConfig>(AssetDatabase.GUIDToAssetPath(configs[0]));
            }
            var flows = AssetDatabase.FindAssets("t:GameFlowConfig").Select(g => AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(f => f && new SerializedObject(f).FindProperty("features")?.objectReferenceValue == window.config).ToArray();
            window.flow = flows.Length == 1 ? flows[0] : null;
            var active = EditorUserBuildSettings.activeBuildTarget;
            window.target = active == BuildTarget.iOS ? BuildTarget.iOS : BuildTarget.Android;
            window.issues = null;
            window.Show();
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pre-build configuration check", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Read-only report for the selected config and platform. Uses the Editor build scene list. Review scene overrides in custom Build Profiles and ensure Bootstrap is the first scene. This does not build or initialize SDKs.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            config = (FeatureConfig)EditorGUILayout.ObjectField("Feature config", config, typeof(FeatureConfig), false);
            flow = EditorGUILayout.ObjectField("Game Flow config (optional)", flow, typeof(ScriptableObject), false);
            target = EditorGUILayout.Popup("Platform", target == BuildTarget.iOS ? 1 : 0, new[] { "Android", "iOS" }) == 1 ? BuildTarget.iOS : BuildTarget.Android;
            if (EditorGUI.EndChangeCheck()) issues = null;
            if (GUILayout.Button("Run validation"))
            {
                issues = FeatureBuildValidator.Validate(config, target);
                issues.AddRange(FeatureBuildValidator.ValidateScenes(flow, EditorBuildSettings.scenes));
                if (flow && flow.GetType().FullName == "BaseUnity.GameFlow.GameFlowConfig" && new SerializedObject(flow).FindProperty("features").objectReferenceValue != config)
                    issues.Add(new FeatureValidationIssue(MessageType.Error, "GameFlowConfig references a different FeatureConfig. Assign the config you intend to ship.", flow));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Feature Manager")) FeatureManagerWindow.Open(config);
                if (GUILayout.Button("Player Settings")) SettingsService.OpenProjectSettings("Project/Player");
            }
            if (issues == null) { EditorGUILayout.LabelField("Run validation after changing settings or build scenes."); return; }
            int errors = issues.Count(i => i.severity == MessageType.Error);
            int warnings = issues.Count(i => i.severity == MessageType.Warning);
            EditorGUILayout.LabelField(errors + " error(s), " + warnings + " warning(s)", EditorStyles.boldLabel);
            if (issues.Count == 0) EditorGUILayout.HelpBox("No issues found by these checks. Device testing and vendor dashboard verification are still required.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(issue.message, issue.severity);
                if (issue.context && GUILayout.Button("Select related asset", GUILayout.Width(160)))
                { Selection.activeObject = issue.context; EditorGUIUtility.PingObject(issue.context); }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
