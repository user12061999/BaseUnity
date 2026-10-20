using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace BaseUnity.GameFlow.Editor
{
    public sealed class UIThemeWorkbench : EditorWindow
    {
        [SerializeField] private GameFlowConfig config;
        [SerializeField] private UIThemeConfig theme;
        [SerializeField] private UIPreviewScreen screen;
        [SerializeField] private UIPreviewLanguage language;
        [SerializeField] private int deviceIndex;
        [SerializeField] private bool safeArea = true;
        private UIPreviewSession preview;
        private UILayoutReport report;
        private string message;
        private int themeDirty;
        private Vector2 scroll;
        [MenuItem("Tools/BaseUnity/UI/Theme and Preview Workbench")]
        public static void Open()
        {
            var window = GetWindow<UIThemeWorkbench>("BaseUnity UI"); window.minSize = new Vector2(580, 620);
            try { window.config = UIThemeBuilder.SelectedConfig(); window.theme = window.config.uiTheme; } catch { }
            window.Rebuild(); window.Show();
        }
        private void OnDisable() { preview?.Dispose(); preview = null; }
        private void OnInspectorUpdate()
        {
            if (theme && themeDirty != EditorUtility.GetDirtyCount(theme)) Rebuild();
            Repaint();
        }
        private void Rebuild()
        {
            preview?.Dispose(); preview = null;
            if (!config) return;
            try { preview = new UIPreviewSession(config, theme, screen, UIPreviewDevice.Defaults[Mathf.Clamp(deviceIndex, 0, UIPreviewDevice.Defaults.Length - 1)], safeArea, language); themeDirty = theme ? EditorUtility.GetDirtyCount(theme) : 0; message = null; }
            catch (Exception error) { message = error.Message; }
            Repaint();
        }
        private void Run(Action action)
        { try { action(); Rebuild(); } catch (Exception error) { message = error.Message; } }
        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var next = (GameFlowConfig)EditorGUILayout.ObjectField("Game config", config, typeof(GameFlowConfig), false);
            if (next != config) { config = next; theme = config ? config.uiTheme : null; }
            theme = (UIThemeConfig)EditorGUILayout.ObjectField("Preview theme", theme, typeof(UIThemeConfig), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                screen = (UIPreviewScreen)EditorGUILayout.EnumPopup("Screen", screen);
                language = (UIPreviewLanguage)EditorGUILayout.EnumPopup(language, GUILayout.Width(155));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                deviceIndex = EditorGUILayout.Popup("Device", deviceIndex, UIPreviewDevice.Defaults.Select(x => x.name + "  " + x.width + "x" + x.height).ToArray());
                safeArea = GUILayout.Toggle(safeArea, "Safe area", GUILayout.Width(85));
            }
            if (EditorGUI.EndChangeCheck()) Rebuild();
            using (new EditorGUI.DisabledScope(!config || EditorApplication.isPlayingOrWillChangePlaymode))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Prepare theme/components")) Run(() => { UIThemeBuilder.Generate(config); theme = config.uiTheme; });
                using (new EditorGUI.DisabledScope(!theme))
                {
                    if (GUILayout.Button("Edit theme")) { Selection.activeObject = theme; EditorGUIUtility.PingObject(theme); }
                    if (GUILayout.Button("Apply theme to prefabs")) Run(() => { AssetDatabase.SaveAssetIfDirty(theme); UIThemeBuilder.ApplyTheme(config, theme); });
                    if (GUILayout.Button("Duplicate theme")) Run(() => { var copy = Instantiate(theme); string path = AssetDatabase.GenerateUniqueAssetPath(UIThemeBuilder.Folder(config) + "/UI/Theme/Theme Copy.asset"); AssetDatabase.CreateAsset(copy, path); theme = copy; Selection.activeObject = copy; });
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!config)) if (GUILayout.Button("Refresh preview")) Rebuild();
                using (new EditorGUI.DisabledScope(preview == null)) if (GUILayout.Button("Export preview PNG")) Run(() => { string path = EditorUtility.SaveFilePanel("Export UI preview", UIThemeBuilder.Folder(config), screen + ".png", "png"); if (!string.IsNullOrEmpty(path)) preview.ExportPng(path); });
                using (new EditorGUI.DisabledScope(!config)) if (GUILayout.Button("Validate all devices/screens"))
                {
                    try { preview?.Dispose(); preview = null; report = UILayoutValidator.ValidateAll(config, theme); string path = "ProjectSettings/BaseUnityUIReports/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json"; report.Save(path); Debug.Log("UI layout report: " + path + "; " + report.cases + " cases, " + report.issues.Count + " findings."); Rebuild(); }
                    catch (Exception error) { message = error.Message; }
                }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Warning);
            EditorGUILayout.HelpBox("Editor-only sample data. Theme preview does not change prefabs; Apply writes project-owned assets and creates a backup. Layout findings need review on real devices.", MessageType.Info);
            var area = GUILayoutUtility.GetRect(100, 10000, 150, Mathf.Max(150, position.height - (report != null ? 340 : 255)), GUILayout.ExpandWidth(true));
            if (preview != null)
            {
                try { var texture = preview.Render(); EditorGUI.DrawRect(area, new Color(.08f, .08f, .1f)); GUI.DrawTexture(area, texture, ScaleMode.ScaleToFit);
                    if (safeArea)
                    {
                        float scale = Mathf.Min(area.width / preview.Width, area.height / preview.Height);
                        Vector2 start = area.center - new Vector2(preview.Width, preview.Height) * scale / 2;
                        var bounds = new Rect(start.x + preview.SafeArea.x * scale, start.y + (preview.Height - preview.SafeArea.yMax) * scale, preview.SafeArea.width * scale, preview.SafeArea.height * scale);
                        var color = new Color(.25f, .95f, .75f, .8f);
                        EditorGUI.DrawRect(new Rect(bounds.x, bounds.y, bounds.width, 1), color); EditorGUI.DrawRect(new Rect(bounds.x, bounds.yMax - 1, bounds.width, 1), color);
                        EditorGUI.DrawRect(new Rect(bounds.x, bounds.y, 1, bounds.height), color); EditorGUI.DrawRect(new Rect(bounds.xMax - 1, bounds.y, 1, bounds.height), color);
                    } }
                catch (Exception error) { message = error.Message; }
            }
            if (report != null)
            {
                EditorGUILayout.LabelField(report.cases + " cases / " + report.issues.Count + " findings", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(90));
                foreach (var issue in report.issues.Take(100)) EditorGUILayout.LabelField(issue.screen + " / " + issue.device + " / " + issue.language + " / " + issue.hierarchy + ": " + issue.message, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
