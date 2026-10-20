using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor
{
    /// <summary>Generates project-owned theme prefabs. Repeated generation preserves existing assets.</summary>
    public static class GameUIPrefabBuilder
    {
        private static Font font;
        private static Sprite art;
        private static readonly Color Navy = new Color(.035f, .065f, .13f);
        [MenuItem("Tools/BaseUnity/Game Flow/Generate Editable UI Prefabs")]
        public static void GenerateSelected()
        {
            var config = Selection.activeObject as GameFlowConfig;
            if (!config) { var ids = AssetDatabase.FindAssets("t:GameFlowConfig", new[] { "Assets" }); if (ids.Length != 1) throw new InvalidOperationException("Select a GameFlowConfig first."); config = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(ids[0])); }
            Generate(config);
        }
        public static void Generate(GameFlowConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/') + "/UI/Theme";
            Directory.CreateDirectory(folder + "/Screens"); Directory.CreateDirectory(folder + "/Components"); AssetDatabase.Refresh();
            if (!config.uiPrefab) LayerLabUIBuilder.Generate(config);
            font = config.uiPrefab.title.font; art = config.uiPrefab.playButton.image.sprite;
            Undo.RecordObject(config, "Assign editable UI prefabs");
            config.uiPrefab = SplitMain(config.uiPrefab, folder);
            OfflineUIBuilder.Generate(config); HomeHubBuilder.Generate(config);
            config.homeHubPrefab = SplitHub(config.homeHubPrefab, folder);
            if (!config.levelSelectionPrefab) config.levelSelectionPrefab = Levels(folder);
            if (!config.mobileModulesPrefab) config.mobileModulesPrefab = Mobile(folder);
            if (!config.offlineOverlayPrefab) config.offlineOverlayPrefab = Offline(folder);
            SessionModulesBuilder.Generate(config);
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(folder + "/GameFlow.prefab");
            Debug.Log("Editable BaseUnity UI ready at " + folder + ". Open Screens or Components in Prefab Mode to edit the theme.");
        }
        private static GameObject Clone(GameObject source)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            return root;
        }
        private static void Extract(GameObject child, string path)
        {
            // Keep the same scene objects: all presenter references are remapped to the nested prefab on save.
            PrefabUtility.SaveAsPrefabAssetAndConnect(child, AssetDatabase.GenerateUniqueAssetPath(path), InteractionMode.AutomatedAction);
        }
        private static GameFlowView SplitMain(GameFlowView source, string folder)
        {
            string path = folder + "/GameFlow.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameFlowView>(path); if (existing) return source;
            var root = Clone(source.gameObject);
            try
            {
                var view = root.GetComponent<GameFlowView>();
                if (!view.privacyButton)
                {
                    view.privacyButton = UnityEngine.Object.Instantiate(view.cancelButton, view.cancelButton.transform.parent);
                    view.privacyButton.name = "Privacy Options"; view.privacyButton.onClick = new Button.ButtonClickedEvent();
                    ((RectTransform)view.privacyButton.transform).anchoredPosition = new Vector2(0, 420);
                    ((RectTransform)view.privacyButton.transform).sizeDelta = new Vector2(600, 65);
                    view.privacyButton.GetComponentInChildren<Text>().text = "PRIVACY OPTIONS";
                }
                var screens = new[] { view.home, view.gameplay, view.loading, view.settings, view.pause, view.result, view.error };
                string[] names = { "Home", "InGame", "Loading", "Settings", "Pause", "Result", "Error" };
                for (int i = 0; i < screens.Length; i++) Extract(screens[i], folder + "/Screens/" + names[i] + ".prefab");
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<GameFlowView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static HomeHubView SplitHub(HomeHubView source, string folder)
        {
            string path = folder + "/HomeHub.prefab"; var existing = AssetDatabase.LoadAssetAtPath<HomeHubView>(path); if (existing) return source;
            var root = Clone(source.gameObject);
            try
            {
                var view = root.GetComponent<HomeHubView>();
                Extract(view.home, folder + "/Screens/HomeHub.prefab");
                Extract(view.confirmation, folder + "/Screens/PurchaseConfirmation.prefab");
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<HomeHubView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static LevelSelectionPrefabView Levels(string folder)
        {
            string path = folder + "/Screens/LevelSelection.prefab"; var existing = AssetDatabase.LoadAssetAtPath<LevelSelectionPrefabView>(path); if (existing) return existing;
            string cellPath = folder + "/Components/LevelCell.prefab";
            var cell = AssetDatabase.LoadAssetAtPath<Button>(cellPath);
            if (!cell) { var button = Button(null, "Level cell", "1  Level\nPLAY", 0, 0, 360, 190); try { cell = PrefabUtility.SaveAsPrefabAsset(button.gameObject, cellPath).GetComponent<Button>(); } finally { UnityEngine.Object.DestroyImmediate(button.gameObject); } }
            var root = Box(null, "Level selection", 900, 1600, 0, 0);
            try
            {
                var view = root.gameObject.AddComponent<LevelSelectionPrefabView>(); view.cellPrefab = cell;
                Label(root, "CHOOSE LEVEL", 0, 620, 800, 80, 54);
                view.completed = Label(root, "0 / 3 COMPLETED", 0, 530, 800, 80, 28);
                view.cells = Rect(root, "Level grid", 760, 690, 0, 115);
                var grid = view.cells.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(360, 190); grid.spacing = new Vector2(40, 60); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
                view.previous = Button(root, "Previous page", "<", -260, -470, 140, 90);
                view.next = Button(root, "Next page", ">", 260, -470, 140, 90);
                view.pageNumber = Label(root, "1 / 1", 0, -470, 240, 80, 30);
                view.back = Button(root, "Back to Home", "BACK HOME", 0, -640, 640, 110);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<LevelSelectionPrefabView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        private static MobileModulesView Mobile(string folder)
        {
            string path = folder + "/MobileModules.prefab"; var existing = AssetDatabase.LoadAssetAtPath<MobileModulesView>(path); if (existing) return existing;
            var root = Canvas("Mobile Modules", 180);
            try
            {
                var view = root.gameObject.AddComponent<MobileModulesView>(); view.content = Safe(root);
                var templates = Rect(view.content, "Templates - edit these for dynamic menu rows", 900, 1600, 0, 0);
                view.buttonTemplate = Button(templates, "Menu button", "ACTION", 0, 0, 660, 80);
                view.labelTemplate = Label(templates, "MENU TEXT", 0, 0, 740, 80, 28);
                view.panelTemplate = Box(templates, "Panel", 820, 650, 0, 0);
                Extract(view.buttonTemplate.gameObject, folder + "/Components/MenuButton.prefab");
                Extract(view.labelTemplate.gameObject, folder + "/Components/MenuLabel.prefab");
                Extract(view.panelTemplate.gameObject, folder + "/Components/Panel.prefab"); templates.gameObject.SetActive(false);
                view.menu = Rect(view.content, "More menu", 900, 1600, 0, 0);
                view.menuPanel = Box(view.menu, "Settings and Store", 820, 1400, 0, 0); Block(view.menuPanel);
                Extract(view.menu.gameObject, folder + "/Screens/MoreMenu.prefab");
                view.popup = Rect(view.content, "Queued popup", 900, 1600, 0, 0); Block(view.popup);
                var panel = Box(view.popup, "Dialog", 820, 650, 0, 0);
                view.popupTitle = Label(panel, "CONFIRM", 0, 210, 740, 100, 38); view.popupMessage = Label(panel, "Message", 0, 40, 740, 210, 28);
                view.popupAccept = Button(panel, "Accept", "OK", 0, -130, 660, 80); view.popupCancel = Button(panel, "Cancel", "CANCEL", 0, -230, 660, 80);
                Extract(view.popup.gameObject, folder + "/Screens/QueuedPopup.prefab");
                view.tutorial = Rect(view.content, "Tutorial", 900, 1600, 0, 0); view.masks = new RectTransform[4]; view.borders = new RectTransform[4];
                for (int i = 0; i < 4; i++)
                {
                    view.masks[i] = Box(view.tutorial, "Outside target " + i, 0, 0, 0, 0); view.masks[i].GetComponent<Image>().color = new Color(0, 0, 0, .65f);
                    view.borders[i] = Box(view.tutorial, "Highlight " + i, 0, 0, 0, 0); var image = view.borders[i].GetComponent<Image>(); image.color = new Color(.3f, 1, .7f); image.raycastTarget = false;
                }
                panel = Box(view.tutorial, "Instructions", 820, 270, 0, 530);
                view.tutorialMessage = Label(panel, "Tutorial message", 0, 65, 760, 120, 28);
                view.tutorialNext = Button(panel, "Next", "NEXT", -185, -70, 330, 80); view.tutorialSkip = Button(panel, "Skip", "SKIP", 185, -70, 330, 80);
                Extract(view.tutorial.gameObject, folder + "/Screens/Tutorial.prefab");
                view.toast = Box(view.content, "Toast", 800, 90, 0, 600); view.toast.GetComponent<Image>().raycastTarget = false; view.toastMessage = Label(view.toast, "Notification", 0, 0, 760, 80, 26);
                Extract(view.toast.gameObject, folder + "/Screens/Toast.prefab");
                view.loading = Box(view.content, "Loading indicator", 700, 100, 0, -500); view.loading.GetComponent<Image>().raycastTarget = false; view.loadingMessage = Label(view.loading, "Loading...", 0, 0, 660, 90, 28);
                Extract(view.loading.gameObject, folder + "/Screens/LoadingIndicator.prefab");
                foreach (var screen in new[] { view.menu, view.popup, view.tutorial, view.toast, view.loading }) screen.gameObject.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<MobileModulesView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        private static OfflineOverlayView Offline(string folder)
        {
            string path = folder + "/OfflineOverlay.prefab"; var existing = AssetDatabase.LoadAssetAtPath<OfflineOverlayView>(path); if (existing) return existing;
            var root = Rect(null, "Offline overlays", 900, 1600, 0, 0);
            try
            {
                var view = root.gameObject.AddComponent<OfflineOverlayView>();
                var waiting = Rect(root, "Waiting for ad", 900, 1600, 0, 0); view.waiting = waiting.gameObject; Block(waiting); Label(waiting, "Waiting for ad...", 0, 0, 800, 80, 30);
                Extract(view.waiting, folder + "/Screens/WaitingForAd.prefab");
                var home = Rect(root, "Offline home bar", 900, 1600, 0, 0); view.home = home.gameObject;
                view.balance = Label(home, "0 Coin    0 Gem", 0, 665, 800, 80, 24); view.menu = Button(home, "Menu", "MENU", 0, 730, 220, 90);
                Extract(view.home, folder + "/Screens/OfflineHomeBar.prefab");
                var result = Rect(root, "Result reward", 900, 1600, 0, 0); view.result = result.gameObject;
                view.reward = Label(result, "REWARD +0 Coin", 0, -570, 800, 80, 30);
                view.retry = Button(result, "Retry reward save", "RETRY REWARD SAVE", 0, -660, 680, 90);
                view.doubleReward = Button(result, "Double reward", "X2 REWARD", 0, -660, 580, 90);
                view.message = Label(result, "", 0, -750, 850, 80, 22);
                Extract(view.result, folder + "/Screens/ResultReward.prefab");
                view.waiting.SetActive(false); view.home.SetActive(false); view.result.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<OfflineOverlayView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        private static RectTransform Canvas(string name, int order)
        {
            var root = Rect(null, name, 900, 1600, 0, 0); var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            root.gameObject.AddComponent<GraphicRaycaster>(); var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = .5f; return root;
        }
        private static RectTransform Safe(RectTransform root)
        { var content = Rect(root, "Safe Area", 900, 1600, 0, 0); var safe = root.gameObject.AddComponent<GameFlowSafeArea>(); safe.canvas = root.GetComponent<Canvas>(); safe.content = content; return content; }
        private static void Block(Transform parent) { var block = Box(parent, "Raycast blocker", 4000, 4000, 0, 0); block.SetAsFirstSibling(); }
        private static RectTransform Rect(Transform parent, string name, float w, float h, float x, float y)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); if (parent) rect.SetParent(parent, false); rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); return rect; }
        private static RectTransform Box(Transform parent, string name, float w, float h, float x, float y)
        { var rect = Rect(parent, name, w, h, x, y); rect.gameObject.AddComponent<Image>().color = Navy; return rect; }
        private static Text Label(Transform parent, string text, float x, float y, float w, float h, int size)
        { var label = Rect(parent, text, w, h, x, y).gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size; label.text = text; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label; }
        private static Button Button(Transform parent, string name, string text, float x, float y, float w, float h)
        { var rect = Box(parent, name, w, h, x, y); var image = rect.GetComponent<Image>(); image.sprite = art; image.type = Image.Type.Sliced; image.color = art ? Color.white : new Color(.15f, .38f, .55f); var button = rect.gameObject.AddComponent<Button>(); var label = Label(rect, text, 0, 0, w - 24, h - 10, 26); label.color = art ? new Color(.025f, .08f, .15f) : Color.white; return button; }
    }
}
