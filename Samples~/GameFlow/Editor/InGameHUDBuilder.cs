using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor
{
    public static class InGameHUDBuilder
    {
        private static Font font;
        private static Sprite buttonArt;
        [MenuItem("Tools/BaseUnity/Game Flow/Generate Missing InGame HUD and Boosters")]
        public static void GenerateSelected()
        {
            var config = Selection.activeObject as GameFlowConfig;
            if (!config) { var ids = AssetDatabase.FindAssets("t:GameFlowConfig", new[] { "Assets" }); if (ids.Length != 1) throw new InvalidOperationException("Select a GameFlowConfig first."); config = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(ids[0])); }
            Generate(config);
        }
        public static void Generate(GameFlowConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/') + "/InGameHUD";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            font = config.uiPrefab && config.uiPrefab.title ? config.uiPrefab.title.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonArt = config.uiPrefab && config.uiPrefab.playButton ? config.uiPrefab.playButton.image.sprite : null;
            Undo.RecordObject(config, "Assign InGame HUD and boosters");
            if (!config.boosters)
            {
                config.boosters = AssetDatabase.LoadAssetAtPath<BoosterCatalog>(folder + "/Boosters.asset");
                if (!config.boosters) { var catalog = ScriptableObject.CreateInstance<BoosterCatalog>(); AssetDatabase.CreateAsset(catalog, folder + "/Boosters.asset"); config.boosters = catalog; }
            }
            if (!config.inGameHUDPrefab)
            {
                var slot = Slot(folder); string path = folder + "/InGameHUD.prefab";
                config.inGameHUDPrefab = AssetDatabase.LoadAssetAtPath<InGameHUDView>(path);
                if (!config.inGameHUDPrefab)
                {
                    var root = Rect(null, "InGame HUD", 900, 1600, 0, 0);
                    try
                    {
                        var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
                        root.gameObject.AddComponent<GraphicRaycaster>(); var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = .5f;
                        var safeContent = Rect(root, "Safe Area", 900, 1600, 0, 0); var safe = root.gameObject.AddComponent<GameFlowSafeArea>(); safe.canvas = canvas; safe.content = safeContent;
                        var view = root.gameObject.AddComponent<InGameHUDView>(); view.slotPrefab = slot;
                        view.gameplay = Rect(safeContent, "Gameplay HUD", 900, 1600, 0, 0).gameObject;
                        var header = Box(view.gameplay.transform, "Goal panel", 840, 210, 0, 530, new Color(.03f, .075f, .15f, .95f)); header.GetComponent<Image>().raycastTarget = false;
                        view.level = Label(header, "LEVEL 1", 0, 60, 760, 50, 36);
                        view.target = Label(header, "TARGET 0 / 30", -205, 0, 390, 50, 26); view.timer = Label(header, "TIME 60s", 205, 0, 390, 50, 26);
                        var progress = Box(header, "Progress track", 760, 16, 0, -65, new Color(.1f, .16f, .24f)); progress.GetComponent<Image>().raycastTarget = false;
                        view.progress = Box(progress, "Progress fill", 760, 16, 0, 0, new Color(.3f, .85f, .6f)).GetComponent<Image>(); view.progress.raycastTarget = false;
                        view.progress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); view.progress.type = Image.Type.Filled; view.progress.fillMethod = Image.FillMethod.Horizontal; view.progress.fillOrigin = 0;
                        var bar = Box(view.gameplay.transform, "Boosters", 840, 135, 0, -715, new Color(.03f, .075f, .15f, .97f)); view.boosterBar = bar.gameObject;
                        view.slots = Rect(bar, "Slots", 800, 115, 0, 0);
                        var layout = view.slots.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 20; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
                        view.message = Label(view.gameplay.transform, "", 0, 410, 800, 35, 23);
                        var modal = Box(safeContent, "Booster confirmation", 900, 1600, 0, 0, new Color(.02f, .04f, .09f, 1)); view.modal = modal.gameObject;
                        view.modalTitle = Label(modal, "USE BOOSTER", 0, 220, 800, 90, 40); view.modalMessage = Label(modal, "", 0, 45, 780, 180, 30);
                        view.accept = Button(modal, "Confirm booster", "USE", -205, -160, 370, 90); view.acceptLabel = view.accept.GetComponentInChildren<Text>();
                        view.cancel = Button(modal, "Cancel booster", "CANCEL", 205, -160, 370, 90);
                        modal.gameObject.SetActive(false);
                        config.inGameHUDPrefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<InGameHUDView>();
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
                }
            }
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        }
        private static BoosterSlotView Slot(string folder)
        {
            string path = folder + "/BoosterSlot.prefab"; var slot = AssetDatabase.LoadAssetAtPath<BoosterSlotView>(path); if (slot) return slot;
            var root = Rect(null, "Booster slot", 390, 115, 0, 0);
            try
            {
                root.gameObject.AddComponent<LayoutElement>().preferredWidth = 390;
                slot = root.gameObject.AddComponent<BoosterSlotView>(); slot.button = Button(root, "Use booster", "", 0, 0, 390, 110);
                var buttonRect = (RectTransform)slot.button.transform; buttonRect.anchorMin = Vector2.zero; buttonRect.anchorMax = Vector2.one; buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
                slot.title = slot.button.GetComponentInChildren<Text>(); slot.title.rectTransform.anchoredPosition = new Vector2(0, 25); slot.title.rectTransform.sizeDelta = new Vector2(340, 35); slot.title.fontSize = 22;
                slot.quantity = Label(slot.button.transform, "x0", -100, -22, 130, 35, 23); slot.status = Label(slot.button.transform, "USE", 65, -22, 190, 35, 20);
                slot.quantity.color = slot.status.color = slot.title.color;
                slot.icon = Rect(slot.button.transform, "Icon", 40, 40, -155, 25).gameObject.AddComponent<Image>(); slot.icon.raycastTarget = false; slot.icon.preserveAspect = true; slot.icon.enabled = false;
                slot.icon.rectTransform.anchorMin = slot.icon.rectTransform.anchorMax = new Vector2(0, .5f); slot.icon.rectTransform.anchoredPosition = new Vector2(25, 25);
                slot.title.rectTransform.anchorMin = new Vector2(0, .5f); slot.title.rectTransform.anchorMax = new Vector2(1, .5f); slot.title.rectTransform.sizeDelta = new Vector2(-65, 35); slot.title.rectTransform.anchoredPosition = new Vector2(15, 25);
                slot.title.resizeTextForBestFit = true; slot.title.resizeTextMinSize = 12; slot.title.resizeTextMaxSize = 22;
                slot.quantity.rectTransform.anchorMin = new Vector2(0, .5f); slot.quantity.rectTransform.anchorMax = new Vector2(.35f, .5f); slot.quantity.rectTransform.sizeDelta = new Vector2(0, 35); slot.quantity.rectTransform.anchoredPosition = new Vector2(0, -22);
                slot.status.rectTransform.anchorMin = new Vector2(.35f, .5f); slot.status.rectTransform.anchorMax = new Vector2(1, .5f); slot.status.rectTransform.sizeDelta = new Vector2(-10, 35); slot.status.rectTransform.anchoredPosition = new Vector2(0, -22); slot.status.resizeTextForBestFit = true; slot.status.resizeTextMinSize = 12; slot.status.resizeTextMaxSize = 20;
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<BoosterSlotView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        private static RectTransform Rect(Transform parent, string name, float w, float h, float x, float y)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); if (parent) rect.SetParent(parent, false); rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); return rect; }
        private static RectTransform Box(Transform parent, string name, float w, float h, float x, float y, Color color)
        { var rect = Rect(parent, name, w, h, x, y); rect.gameObject.AddComponent<Image>().color = color; return rect; }
        private static Text Label(Transform parent, string text, float x, float y, float w, float h, int size)
        { var label = Rect(parent, "Label", w, h, x, y).gameObject.AddComponent<Text>(); label.text = text; label.font = font; label.fontSize = size; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label; }
        private static Button Button(Transform parent, string name, string text, float x, float y, float w, float h)
        { bool cancel = name.StartsWith("Cancel", StringComparison.Ordinal); var art = cancel ? null : buttonArt; var rect = Box(parent, name, w, h, x, y, art ? Color.white : new Color(.14f, .35f, .55f)); var image = rect.GetComponent<Image>(); image.sprite = art; image.type = Image.Type.Sliced; var button = rect.gameObject.AddComponent<Button>(); var label = Label(rect, text, 0, 0, w - 20, h - 10, 25); label.color = art ? new Color(.025f, .08f, .15f) : Color.white; return button; }
    }
}
