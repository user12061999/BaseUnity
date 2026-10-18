using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow.Editor
{
    public static class OfflineUIBuilder
    {
        [MenuItem("Tools/BaseUnity/Game Flow/Generate Missing Offline UI Prefabs")]
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
            if (config.offlineMenuPrefab) return;
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/') + "/OfflineUI";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var sprite = config.uiPrefab && config.uiPrefab.playButton ? config.uiPrefab.playButton.image.sprite : null;
            var row = AssetDatabase.LoadAssetAtPath<OfflineRowView>(folder + "/ItemRow.prefab");
            if (!row)
            {
                var root = Rect("Item Row", null, 800, 180, 0, 0);
                try
                {
                    var layout = root.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 180;
                    var view = root.gameObject.AddComponent<OfflineRowView>();
                    view.title = Label(root, "Item", 0, 50, 780, 70);
                    view.action = Button(root, "Action", "BUY", 0, -40, 640, sprite);
                    view.actionLabel = view.action.GetComponentInChildren<Text>();
                    row = PrefabUtility.SaveAsPrefabAsset(root.gameObject, folder + "/ItemRow.prefab").GetComponent<OfflineRowView>();
                } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
            }
            string[] keys = { "SHOP", "DAILY REWARD", "MISSIONS", "INVENTORY" };
            string[] names = { "Shop", "DailyReward", "Missions", "Inventory" };
            var panels = new OfflinePanelView[4];
            for (int i = 0; i < keys.Length; i++)
            {
                string path = folder + "/" + names[i] + ".prefab";
                panels[i] = AssetDatabase.LoadAssetAtPath<OfflinePanelView>(path);
                if (panels[i]) continue;
                var root = Rect(names[i], null, 900, 850, 0, 0);
                try
                {
                    var view = root.gameObject.AddComponent<OfflinePanelView>(); view.key = keys[i]; view.rowPrefab = row;
                    view.title = Label(root, keys[i], 0, 330, 800, 70); view.title.fontStyle = FontStyle.Bold;
                    view.empty = Label(root, "No items", 0, 60, 780, 70);
                    view.rows = Rect("Rows", root, 800, 590, 0, -20);
                    var layout = view.rows.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 15; layout.childAlignment = TextAnchor.UpperCenter;
                    layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                    view.previous = Button(root, "Previous", "<", -280, -350, 110, sprite);
                    view.next = Button(root, "Next", ">", 280, -350, 110, sprite);
                    view.pageLabel = Label(root, "1 / 1", 0, -350, 240, 70);
                    panels[i] = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<OfflinePanelView>();
                } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
            }
            var menu = AssetDatabase.LoadAssetAtPath<OfflineMenuView>(folder + "/OfflineMenu.prefab");
            if (!menu)
            {
                var root = Rect("Offline Menu", null, 900, 1600, 0, 0);
                try
                {
                    root.gameObject.AddComponent<Image>().color = new Color(.025f, .06f, .12f);
                    var view = root.gameObject.AddComponent<OfflineMenuView>();
                    var content = Rect("Content", root, 900, 1600, 0, 0); view.animatedContent = content;
                    view.balance = Label(content, "Wallet", 0, 700, 800, 80);
                    view.tabs = new OfflineMenuView.Tab[4];
                    for (int i = 0; i < 4; i++)
                    {
                        var panel = (GameObject)PrefabUtility.InstantiatePrefab(panels[i].gameObject, content);
                        var button = Button(content, keys[i], keys[i], i % 2 == 0 ? -205 : 205, 570 - (i / 2) * 110, 390, sprite);
                        var selected = Rect("Selected", button.transform, 340, 6, 0, -42).gameObject.AddComponent<Image>();
                        selected.color = new Color(.3f, 1f, .8f); selected.raycastTarget = false;
                        view.tabs[i] = new OfflineMenuView.Tab { key = keys[i], button = button, selected = selected.gameObject, panel = panel.GetComponent<OfflinePanelView>() };
                        panel.SetActive(i == 0); selected.gameObject.SetActive(i == 0);
                    }
                    view.message = Label(content, "", 0, -440, 800, 110); view.message.fontSize = 25;
                    view.language = Button(content, "Language", "Tiếng Việt", 0, -570, 340, sprite); view.languageLabel = view.language.GetComponentInChildren<Text>();
                    view.close = Button(content, "CLOSE", "CLOSE", 0, -700, 340, sprite);
                    var busy = Rect("Saving Blocker", root, 900, 1600, 0, 0); busy.gameObject.AddComponent<Image>().color = new Color(.025f, .06f, .12f, .96f);
                    Label(busy, "Saving...", 0, 0, 780, 80); view.loading = busy.gameObject; view.loading.SetActive(false);
                    menu = PrefabUtility.SaveAsPrefabAsset(root.gameObject, folder + "/OfflineMenu.prefab").GetComponent<OfflineMenuView>();
                } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
            }
            Undo.RecordObject(config, "Assign offline UI prefabs"); config.offlineMenuPrefab = menu;
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        }
        private static RectTransform Rect(string name, Transform parent, float width, float height, float x, float y)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (parent) rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, y); return rect;
        }
        private static Text Label(Transform parent, string text, float x, float y, float w, float h)
        {
            var label = Rect("Label", parent, w, h, x, y).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 30;
            label.text = text; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
        private static Button Button(Transform parent, string name, string text, float x, float y, float width, Sprite sprite)
        {
            var rect = Rect(name, parent, width, 90, x, y);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced;
            image.color = sprite ? Color.white : new Color(.12f, .36f, .57f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label(rect, text, 0, 0, width - 25, 75); label.fontSize = 27; label.fontStyle = FontStyle.Bold;
            if (sprite) { var outline = label.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .65f); outline.effectDistance = new Vector2(1, -1); }
            return button;
        }
    }
}
