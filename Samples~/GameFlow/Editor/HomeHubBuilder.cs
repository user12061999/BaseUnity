using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow.Editor
{
    /// <summary>Creates project-owned nested prefabs. Existing generated assets are never overwritten.</summary>
    public static class HomeHubBuilder
    {
        private const string Pack = "Assets/Layer Lab/GUI Pro-CasualGame/ResourcesData/";
        private static Font font;
        private static Sprite blue, yellow, coin, trophy, hero;
        private static readonly Color Navy = new Color(.035f, .065f, .13f), Pale = new Color(.65f, .84f, 1);
        [MenuItem("Tools/BaseUnity/Game Flow/Generate Missing Home Store Quest Prefabs")]
        public static void GenerateSelected()
        {
            var config = Selection.activeObject as GameFlowConfig;
            if (!config) { var ids = AssetDatabase.FindAssets("t:GameFlowConfig", new[] { "Assets" }); if (ids.Length != 1) throw new InvalidOperationException("Select a GameFlowConfig first."); config = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(ids[0])); }
            Generate(config);
        }
        public static void Generate(GameFlowConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/') + "/HomeHub";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<Font>(Pack + "Fonts/LilitaOne-Regular.ttf"); if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            blue = Art("Components/Button/Button01_Blue.Png"); yellow = Art("Components/Button/Button01_Yellow.Png");
            coin = Art("Components/UI_Etc/ResourceBar_Icon_Gold.png"); trophy = Art("Components/IconMisc/Icon_MenuIcon04_Trophy.Png"); hero = Art("Demo/Demo_Character/Character_Sample01_xl.png");
            if (!config.homeHubCatalog)
            {
                var data = AssetDatabase.LoadAssetAtPath<HomeHubCatalog>(folder + "/Presentation.asset");
                if (!data)
                {
                    data = ScriptableObject.CreateInstance<HomeHubCatalog>(); data.avatar = hero;
                    data.entries = new[] {
                        Entry("energy", "Energy", "Năng lượng", "A little boost for your next adventure", "Tiếp sức cho chuyến phiêu lưu tiếp theo", coin),
                        Entry("ticket", "Ticket", "Vé", "Keep tickets ready for your game events", "Chuẩn bị vé cho sự kiện trong game", trophy),
                        Entry("first_win", "First victory", "Chiến thắng đầu tiên", "Win one level to earn this reward", "Thắng một màn để nhận phần thưởng", trophy),
                        Entry("ten_games", "Getting started", "Khởi đầu hành trình", "Finish ten sessions at your own pace", "Hoàn thành mười lượt chơi", trophy) };
                    AssetDatabase.CreateAsset(data, folder + "/Presentation.asset");
                }
                Undo.RecordObject(config, "Assign Home Hub presentation"); config.homeHubCatalog = data;
            }
            if (!config.homeHubPrefab)
            {
                var card = MakeCard(folder);
                var store = MakePage(folder, "Store", "COINS", "IAP"); var quests = MakePage(folder, "Quests", "DAILY QUESTS", "ACHIEVEMENTS");
                string path = folder + "/HomeHub.prefab";
                var existing = AssetDatabase.LoadAssetAtPath<HomeHubView>(path);
                if (!existing)
                {
                    var root = Rect(null, "Home Hub", 900, 1600, 0, 0);
                    try
                    {
                        var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 105;
                        root.gameObject.AddComponent<GraphicRaycaster>(); var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = .5f;
                        var safeContent = Rect(root, "Safe Area", 900, 1600, 0, 0); var safe = root.gameObject.AddComponent<GameFlowSafeArea>(); safe.canvas = canvas; safe.content = safeContent;
                        var view = root.gameObject.AddComponent<HomeHubView>(); view.cardPrefab = card;
                        var home = Box(safeContent, "Home", 900, 1600, 0, 0, Navy); view.home = home.gameObject;
                        Label(home, "BASE CAMP", 0, 620, 800, 45, 22, Pale);
                        view.gameTitle = Label(home, config.gameTitle, 0, 545, 820, 110, 56, Color.white);
                        var profile = Box(home, "Player card", 800, 140, 0, 370, new Color(.075f, .14f, .25f));
                        view.avatar = Picture(profile, "Avatar", hero, -310, 0, 110, 115);
                        view.playerName = Label(profile, "PLAYER", 0, 30, 450, 45, 32, Color.white);
                        view.level = Label(profile, "LEVEL 1", 0, -28, 450, 40, 24, Pale);
                        Picture(home, "Hero", hero, 0, 125, 300, 270);
                        view.play = Button(home, "Play", "PLAY", 0, -110, 650, 115, true);
                        view.levels = Button(home, "Levels", "CHOOSE LEVEL", 0, -245, 550, 80);
                        view.store = Button(home, "Store", "STORE", -215, -390, 380, 105);
                        view.quests = Button(home, "Quests", "QUESTS", 215, -390, 380, 105);
                        view.daily = Button(home, "Daily reward", "DAILY REWARD", -215, -520, 380, 105);
                        view.inventory = Button(home, "Inventory", "INVENTORY", 215, -520, 380, 105);
                        view.dailyDot = Dot(view.daily.transform, false, out _);
                        view.questDot = Dot(view.quests.transform, true, out var count); view.questCount = count;
                        view.settings = Button(home, "Settings", "SETTINGS", -215, -675, 380, 75);
                        view.more = Button(home, "More", "MORE", 215, -675, 380, 75);
                        // Header remains visible across Home/Store/Quest.
                        view.wallet = Label(safeContent, "0 Coin    0 Gem", 0, 735, 800, 60, 28, Color.white);
                        view.modal = Rect(safeContent, "Pages", 900, 1420, 0, -60).gameObject;
                        view.storePage = ((GameObject)PrefabUtility.InstantiatePrefab(store.gameObject, view.modal.transform)).GetComponent<HubPageView>();
                        view.questPage = ((GameObject)PrefabUtility.InstantiatePrefab(quests.gameObject, view.modal.transform)).GetComponent<HubPageView>();
                        var confirm = Box(safeContent, "Purchase confirmation", 900, 1600, 0, 0, new Color(0, 0, 0, .9f)); view.confirmation = confirm.gameObject;
                        Label(confirm, "CONFIRM PURCHASE", 0, 220, 800, 85, 38, Color.white);
                        view.confirmMessage = Label(confirm, "Purchase details", 0, 55, 760, 200, 30, Color.white);
                        view.confirmAccept = Button(confirm, "Confirm", "CONFIRM", -200, -160, 360, 90, true);
                        view.confirmCancel = Button(confirm, "Cancel", "CANCEL", 200, -160, 360, 90);
                        view.modal.SetActive(false); confirm.gameObject.SetActive(false);
                        existing = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<HomeHubView>();
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
                }
                Undo.RecordObject(config, "Assign Home Hub prefab"); config.homeHubPrefab = existing;
            }
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        }
        private static HomeHubCatalog.Entry Entry(string id, string en, string vi, string descEn, string descVi, Sprite icon) => new HomeHubCatalog.Entry { id = id, titleEnglish = en, titleVietnamese = vi, descriptionEnglish = descEn, descriptionVietnamese = descVi, icon = icon };
        private static HubCardView MakeCard(string folder)
        {
            string path = folder + "/HubCard.prefab"; var existing = AssetDatabase.LoadAssetAtPath<HubCardView>(path); if (existing) return existing;
            var root = Box(null, "Reward Product Card", 800, 240, 0, 0, new Color(.08f, .15f, .26f));
            try
            {
                root.gameObject.AddComponent<LayoutElement>().preferredHeight = 240; var view = root.gameObject.AddComponent<HubCardView>();
                view.icon = Picture(root, "Icon", coin, -320, 20, 105, 110);
                view.title = Label(root, "Item title", 55, 75, 590, 55, 32, Color.white);
                view.description = Label(root, "Description", 65, 22, 570, 55, 22, Pale);
                view.reward = Label(root, "+100 Coin", -160, -45, 420, 40, 26, new Color(1, .83f, .32f));
                view.progress = Label(root, "0 / 3", -160, -93, 400, 36, 20, Color.white);
                var track = Box(root, "Progress", 410, 10, -155, -70, Navy); view.progressRoot = track.gameObject;
                view.progressFill = Box(track, "Fill", 410, 10, 0, 0, new Color(.3f, .86f, .64f)).GetComponent<Image>();
                // Filled images need a sprite, including in projects without the art pack.
                view.progressFill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); view.progressFill.type = Image.Type.Filled; view.progressFill.fillMethod = Image.FillMethod.Horizontal; view.progressFill.fillOrigin = 0;
                view.action = Button(root, "Action", "CLAIM", 245, -65, 260, 75, true); view.actionLabel = view.action.GetComponentInChildren<Text>();
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<HubCardView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        private static HubPageView MakePage(string folder, string name, string first, string second)
        {
            string path = folder + "/" + name + ".prefab"; var existing = AssetDatabase.LoadAssetAtPath<HubPageView>(path); if (existing) return existing;
            var root = Box(null, name, 900, 1420, 0, 0, Navy);
            try
            {
                var view = root.gameObject.AddComponent<HubPageView>(); view.heading = Label(root, name.ToUpperInvariant(), 0, 615, 800, 90, 50, Color.white);
                view.primary = Button(root, first, first, -210, 495, 390, 85); view.secondary = Button(root, second, second, 210, 495, 390, 85);
                view.countdown = Label(root, "DAILY RESET", 0, 415, 800, 45, 22, Pale);
                view.rows = Rect(root, "Cards", 800, 760, 0, -10);
                var layout = view.rows.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 20; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                view.empty = Label(root, "No items", 0, 100, 800, 80, 28, Pale).gameObject;
                view.previous = Button(root, "Previous", "<", -320, -435, 120, 65); view.next = Button(root, "Next", ">", 320, -435, 120, 65); view.pageNumber = Label(root, "1 / 1", 0, -435, 300, 65, 24, Pale);
                view.message = Label(root, "", 0, -510, 800, 65, 23, new Color(1, .79f, .4f));
                view.restore = Button(root, "Restore", "RESTORE PURCHASES", -205, -595, 390, 65); view.retryPending = Button(root, "Retry delivery", "RETRY DELIVERY", 205, -595, 390, 65);
                view.close = Button(root, "Close", "CLOSE", 0, -675, 420, 65);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<HubPageView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        private static Sprite Art(string relative) => AssetDatabase.LoadAssetAtPath<Sprite>(Pack + "Sprite/" + relative);
        private static RectTransform Rect(Transform parent, string name, float w, float h, float x, float y)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); if (parent) rect.SetParent(parent, false); rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); return rect; }
        private static RectTransform Box(Transform parent, string name, float w, float h, float x, float y, Color color)
        { var rect = Rect(parent, name, w, h, x, y); rect.gameObject.AddComponent<Image>().color = color; return rect; }
        private static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
        { var image = Rect(parent, name, w, h, x, y).gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; image.enabled = sprite; return image; }
        private static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
        { var label = Rect(parent, "Label", w, h, x, y).gameObject.AddComponent<Text>(); label.text = text; label.font = font; label.fontSize = size; label.color = color; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label; }
        private static Button Button(Transform parent, string name, string text, float x, float y, float w, float h, bool primary = false)
        { var rect = Box(parent, name, w, h, x, y, primary ? new Color(1, .8f, .3f) : new Color(.14f, .35f, .55f)); var image = rect.GetComponent<Image>(); image.sprite = primary ? yellow : blue; image.type = Image.Type.Sliced; if (image.sprite) image.color = Color.white; var button = rect.gameObject.AddComponent<Button>(); Label(rect, text, 0, 0, w - 20, h - 10, 25, primary ? Navy : Color.white); return button; }
        private static GameObject Dot(Transform parent, bool numbered, out Text count)
        { var rect = Box(parent, "Reward badge", numbered ? 48 : 30, numbered ? 48 : 30, 160, 40, new Color(.95f, .22f, .28f)); var image = rect.GetComponent<Image>(); image.raycastTarget = false; image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); count = numbered ? Label(rect, "1", 0, 0, 45, 45, 23, Color.white) : null; return rect.gameObject; }
    }
}
