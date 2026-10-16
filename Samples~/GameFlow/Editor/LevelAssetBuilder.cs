using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow.Editor
{
    public static class LevelAssetBuilder
    {
        [MenuItem("Tools/BaseUnity/Game Flow/Generate Level Assets")]
        public static void GenerateSelected()
        {
            var config = Selection.activeObject as GameFlowConfig;
            if (!config)
            {
                var ids = AssetDatabase.FindAssets("t:GameFlowConfig", new[] { "Assets" });
                if (ids.Length != 1) throw new InvalidOperationException("Select a GameFlowConfig first.");
                config = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(ids[0]));
            }
            Generate(config);
        }
        public static void Generate(GameFlowConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/') + "/Levels";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string prefabPath = folder + "/TapTargetGameplay.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) prefab = BuildDemo(prefabPath, config);
            Undo.RecordObject(config, "Create level data assets");
            for (int i = 0; i < config.LevelCount; i++)
            {
                var entry = config.levels[i];
                if (entry == null || entry.data) continue;
                var data = ScriptableObject.CreateInstance<LevelData>();
                data.title = entry.title; data.targetScore = entry.targetScore;
                data.timeLimitSeconds = entry.timeLimitSeconds; data.sceneOverride = entry.sceneOverride;
                data.gameplayPrefab = entry.gameplayPrefab ? entry.gameplayPrefab : prefab;
                AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(folder + "/Level" + (i + 1).ToString("D3") + ".asset"));
                entry.data = data;
            }
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            Debug.Log("Level assets ready in " + folder + ". Existing LevelData and prefabs were preserved.");
        }
        private static GameObject BuildDemo(string path, GameFlowConfig config)
        {
            var root = new GameObject("Tap Target Gameplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = 0.5f;
                var background = Rect("Background", root.transform, 0, 0, 0, 0);
                background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
                background.offsetMin = background.offsetMax = Vector2.zero;
                var backgroundImage = background.gameObject.AddComponent<Image>();
                backgroundImage.color = new Color(0.04f, 0.15f, 0.27f); backgroundImage.raycastTarget = false;
                var content = Rect("Safe Area", root.transform, 900, 1600, 0, 0);
                var safe = root.AddComponent<GameFlowSafeArea>(); safe.canvas = canvas; safe.content = content;
                var font = config.uiPrefab ? config.uiPrefab.title.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var sprite = config.uiPrefab ? config.uiPrefab.playButton.image.sprite : null;
                var bg = Rect("Board", content, 820, 1000, 0, -100).gameObject.AddComponent<Image>();
                bg.color = new Color(0.025f, 0.09f, 0.19f); bg.raycastTarget = false;
                Label(content, font, "TAP THE GOLD TARGET", 0, 350, 700, 70, 38);
                Label(content, font, "Collect points. Avoid the red trap!", 0, -500, 760, 70, 28);
                var demo = root.AddComponent<TapTargetGameplay>();
                demo.target = Button(content, font, sprite, "Tap target", "+10", -210, 180, new Color(1, 0.85f, 0.3f));
                demo.trap = Button(content, font, sprite, "Trap", "AVOID", 0, -330, new Color(0.85f, 0.16f, 0.24f));
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(string name, Transform parent, float w, float h, float x, float y)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); return rect;
        }
        private static void Label(Transform parent, Font font, string caption, float x, float y, float w, float h, int size)
        {
            var text = Rect(caption, parent, w, h, x, y).gameObject.AddComponent<Text>(); text.font = font; text.text = caption;
            text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
        }
        private static Button Button(Transform parent, Font font, Sprite sprite, string name, string caption, float x, float y, Color color)
        {
            var rect = Rect(name, parent, 220, 140, x, y); var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Label(rect, font, caption, 0, 0, 200, 110, 40); return button;
        }
    }
}
