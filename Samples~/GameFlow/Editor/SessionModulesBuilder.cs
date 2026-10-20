using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor
{
    public static class SessionModulesBuilder
    {
        [MenuItem("Tools/BaseUnity/Game Flow/Prepare Session Flow and Audio")]
        public static void GenerateSelected() { Generate(UIThemeBuilder.SelectedConfig()); }
        public static void Generate(GameFlowConfig config)
        {
            if (!config || !AssetDatabase.GetAssetPath(config).StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidOperationException("Select a project-owned GameFlowConfig.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            string backup = UIThemeBuilder.Backup(config);
            string folder = UIThemeBuilder.Folder(config) + "/UI/Theme";
            Directory.CreateDirectory(folder + "/Screens"); AssetDatabase.Refresh();
            Undo.RecordObject(config, "Prepare session and audio");
            if (!config.audioProfile)
            {
                config.audioProfile = AssetDatabase.LoadAssetAtPath<GameFlowAudioProfile>(folder + "/AudioProfile.asset");
                if (!config.audioProfile) { config.audioProfile = ScriptableObject.CreateInstance<GameFlowAudioProfile>(); AssetDatabase.CreateAsset(config.audioProfile, folder + "/AudioProfile.asset"); }
            }
            if (!config.sessionOverlayPrefab)
            {
                string path = folder + "/Screens/SessionOverlay.prefab";
                config.sessionOverlayPrefab = AssetDatabase.LoadAssetAtPath<SessionOverlayView>(path);
                if (!config.sessionOverlayPrefab)
                {
                    var root = new GameObject("Session overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    try
                    {
                        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 160;
                        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = .5f;
                        var view = root.AddComponent<SessionOverlayView>();
                        var content = new GameObject("Content", typeof(RectTransform), typeof(Image)); content.transform.SetParent(root.transform, false);
                        var rect = content.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                        content.GetComponent<Image>().color = new Color(0, 0, 0, .45f); view.content = content;
                        var safe = new GameObject("Safe Area", typeof(RectTransform)); safe.transform.SetParent(content.transform, false);
                        rect = safe.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                        var safeArea = content.AddComponent<GameFlowSafeArea>(); safeArea.canvas = canvas; safeArea.content = rect;
                        var font = config.uiPrefab && config.uiPrefab.title ? config.uiPrefab.title.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        view.heading = Label(safe.transform, "Heading", 120, 40, font); view.heading.text = "GET READY";
                        view.value = Label(safe.transform, "Countdown", -20, 100, font); view.value.text = "3";
                        content.SetActive(false);
                        config.sessionOverlayPrefab = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<SessionOverlayView>();
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
            }
            if (config.uiPrefab && !config.uiPrefab.restartButton)
            {
                string path = AssetDatabase.GetAssetPath(config.uiPrefab);
                if (!path.StartsWith(UIThemeBuilder.Folder(config) + "/", StringComparison.Ordinal)) throw new InvalidOperationException("Copy the UI prefab into the config folder before adding Restart.");
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<GameFlowView>();
                    if (view.pauseHomeButton)
                    {
                        view.restartButton = UnityEngine.Object.Instantiate(view.pauseHomeButton, view.pauseHomeButton.transform.parent);
                        view.restartButton.name = "Restart level"; view.restartButton.onClick = new Button.ButtonClickedEvent();
                        var rect = (RectTransform)view.restartButton.transform; rect.anchoredPosition = new Vector2(0, -430); rect.sizeDelta = new Vector2(620, 90);
                        var label = view.restartButton.GetComponentInChildren<Text>(true); if (label) { label.text = "RESTART"; label.fontSize = 30; }
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("Session overlay/audio ready. Enable GameFlowConfig > Session Flow to use countdown. Backup: " + backup);
        }
        private static Text Label(Transform parent, string name, float y, int size, Font font)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text)); root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(780, 130); rect.anchoredPosition = new Vector2(0, y);
            var label = root.GetComponent<Text>(); label.font = font; label.fontSize = size; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
    }
}
