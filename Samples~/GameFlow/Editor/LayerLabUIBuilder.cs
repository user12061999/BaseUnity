using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow.Editor
{
    /// <summary>Creates an editable project-owned prefab from an installed Layer Lab pack; distributes no art.</summary>
    public static class LayerLabUIBuilder
    {
        private const string Pack = "Assets/Layer Lab/GUI Pro-CasualGame/ResourcesData/";
        private static Font font;
        private static Sprite yellow, blue, grey, panel, star, coin, trophy, gear, close, pauseIcon, hero, floor, backdrop, ribbon;
        private static readonly Color Navy = new Color(0.025f, 0.1f, 0.22f);
        private static readonly Color Pale = new Color(0.65f, 0.88f, 1);

        [MenuItem("Tools/BaseUnity/Game Flow/Apply Layer Lab UI")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var config = Selection.activeObject as GameFlowConfig;
            if (!config)
            {
                var configs = AssetDatabase.FindAssets("t:GameFlowConfig", new[] { "Assets" });
                if (configs.Length != 1) throw new InvalidOperationException("Select the destination GameFlowConfig asset first.");
                config = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(configs[0]));
            }
            font = AssetDatabase.LoadAssetAtPath<Font>(Pack + "Fonts/LilitaOne-Regular.ttf");
            if (!font) throw new InvalidOperationException("Import Layer Lab GUI Pro-CasualGame into Assets/Layer Lab first.");
            yellow = Sprite("Components/Button/Button01_Yellow.Png");
            blue = Sprite("Components/Button/Button01_Blue.Png");
            grey = Sprite("Components/Button/Button01_Gray.Png");
            panel = Sprite("Components/Popup/Popup01_Single_Navy.png");
            star = Sprite("Components/IconMisc/Icon_ImageIcon_Star02_l.png");
            coin = Sprite("Components/UI_Etc/ResourceBar_Icon_Gold.png");
            trophy = Sprite("Components/IconMisc/Icon_MenuIcon04_Trophy.Png");
            gear = Sprite("Components/IconMisc/Icon_PictoIcon_Setting02.png");
            close = Sprite("Components/IconMisc/Icon_PictoIcon_Close.png");
            pauseIcon = Sprite("Components/IconMisc/Icon_PictoIcon_Pause.png");
            hero = Sprite("Demo/Demo_Character/Character_Sample01_xl.png");
            floor = Sprite("Demo/Demo_Background/Background_02.png");
            backdrop = Sprite("Demo/Demo_Background/Background_06.png");
            ribbon = Sprite("Components/Label/Title_Ribbon_Bg_Orange.png");
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/') + "/UI";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            // Each run creates a new variant so manual prefab edits are never overwritten.
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/LayerLabGameFlow.prefab");
            var root = new GameObject("Layer Lab Game Flow", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = 0.5f;
                var view = root.AddComponent<GameFlowView>();
                var background = Picture("Full screen background", root.transform, backdrop, 0, 0, 0, 0, true);
                background.rectTransform.anchorMin = Vector2.zero;
                background.rectTransform.anchorMax = Vector2.one;
                background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
                view.fullScreenBackground = background.gameObject;
                var content = Rect("Safe Area Content", root.transform, 900, 1600, 0, 0);
                var safe = root.AddComponent<GameFlowSafeArea>(); safe.content = content; safe.canvas = canvas;
                safe.referenceResolution = new Vector2(900, 1600);
                view.home = Screen("Home", content);
                Home(view);
                view.gameplay = Screen("InGame", content);
                Gameplay(view);
                view.loading = Screen("Loading", content);
                Loading(view);
                view.settings = Screen("Settings Modal", content);
                Settings(view);
                view.pause = Screen("Pause Modal", content);
                Pause(view);
                view.result = Screen("Result Modal", content);
                Result(view);
                view.error = Screen("Error Modal", content);
                Error(view);
                // Home is visible in prefab mode for layout editing.
                foreach (var page in new[] { view.gameplay, view.loading, view.settings, view.pause, view.result, view.error }) page.SetActive(false);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Undo.RecordObject(config, "Apply Layer Lab UI");
                config.uiPrefab = prefab.GetComponent<GameFlowView>();
                EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
                Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
                Debug.Log("Layer Lab UI ready: " + path + ". Press Play from Bootstrap. Artwork stays in the game project.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Sprite Sprite(string relative)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Sprite>(Pack + "Sprite/" + relative);
            if (!asset) throw new FileNotFoundException("Missing Layer Lab sprite: " + relative);
            return asset;
        }
        private static RectTransform Rect(string name, Transform parent, float w, float h, float x, float y)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }
        private static GameObject Screen(string name, Transform parent)
        {
            var root = Rect(name, parent, 900, 1600, 0, 0).gameObject;
            root.AddComponent<CanvasGroup>(); return root;
        }
        private static Image Picture(string name, Transform parent, Sprite sprite, float w, float h, float x, float y, bool sliced = false, Color? color = null)
        {
            var image = Rect(name, parent, w, h, x, y).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color ?? Color.white;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !sliced && sprite;
            image.raycastTarget = false; return image;
        }
        private static Text Label(string name, Transform parent, string value, float w, float h, float x, float y, int size, Color? color = null)
        {
            var text = Rect(name, parent, w, h, x, y).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = color ?? Color.white;
            text.alignment = TextAnchor.MiddleCenter; text.text = value; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0.01f, 0.03f, 0.08f, 0.85f); outline.effectDistance = new Vector2(1.5f, -2);
            return text;
        }
        private static Button Button(string name, Transform parent, string caption, Sprite sprite, float w, float h, float x, float y, int size = 32)
        {
            var image = Picture(name, parent, sprite, w, h, x, y, true);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1, 0.96f, 0.8f); colors.pressedColor = new Color(0.75f, 0.8f, 0.85f); button.colors = colors;
            Label("Caption", image.transform, caption, w - 24, h - 12, 0, 4, size);
            return button;
        }
        private static Button IconButton(string name, Transform parent, Sprite icon, float x, float y)
        {
            var button = Button(name, parent, "", grey, 80, 76, x, y);
            Picture("Icon", button.transform, icon, 40, 40, 0, 4);
            return button;
        }
        private static void Background(Transform root)
        {
            Picture("Blue background", root, backdrop, 900, 1600, 0, 0, true);
            Picture("Arena floor", root, floor, 1600, 900, 0, 0);
        }
        private static void TopBar(Transform root, string section)
        {
            Picture("Top shade", root, null, 900, 150, 0, 725, false, new Color(0.02f, 0.08f, 0.18f, 0.75f));
            Label("Brand", root, "BASE UNITY", 270, 48, -270, 750, 34);
            Label("Section", root, section, 270, 34, -270, 708, 22, Pale);
        }
        private static void Home(GameFlowView v)
        {
            var root = v.home.transform; Background(root); TopBar(root, "HOME");
            Picture("Best icon", root, trophy, 70, 70, -270, 580);
            Label("Best label", root, "PERSONAL BEST", 220, 35, -100, 610, 21, Pale);
            v.homeBest = Label("Best value", root, "0", 220, 50, -100, 563, 40);
            Label("Sessions label", root, "SESSIONS", 200, 35, 220, 610, 21, Pale);
            v.sessions = Label("Sessions value", root, "0", 200, 50, 220, 563, 40);
            v.settingsButton = IconButton("Settings", root, gear, 350, 730);
            v.title = Label("Game title", root, "YOUR GAME", 800, 110, 0, 430, 76);
            Label("Subtitle", root, "A NEW ADVENTURE AWAITS", 800, 50, 0, 340, 28, Pale);
            var card = Picture("Adventure card", root, panel, 730, 210, 0, -280, true);
            Picture("Star", card.transform, star, 68, 68, -175, 20);
            Label("Ready", card.transform, "READY TO PLAY?", 330, 52, 55, 42, 30);
            Label("Description", card.transform, "Jump in. Collect points.\nBeat your personal best.", 360, 90, 45, -32, 23, Pale);
            Picture("Hero", root, hero, 370, 420, 0, 70);
            v.playButton = Button("Play", root, "PLAY LEVEL 1", yellow, 700, 130, 0, -505, 48);
            v.homeLevel = v.playButton.GetComponentInChildren<Text>();
            Label("Hint", root, "Win levels to unlock the next challenge", 800, 50, 0, -630, 27, Pale);
        }
        private static void Gameplay(GameFlowView v)
        {
            var root = v.gameplay.transform;
            // Remove this single group to expose the real scene behind the HUD.
            v.gameplayDemo = Rect("Demo Backdrop - remove for real gameplay", root, 900, 1600, 0, 0).gameObject;
            Background(v.gameplayDemo.transform);
            Picture("Demo hero", v.gameplayDemo.transform, hero, 380, 440, 0, 100);
            TopBar(root, "IN GAME");
            v.levelLabel = Label("Level", root, "LEVEL 1", 360, 60, 0, 590, 44);
            Picture("Score icon", root, coin, 60, 60, -75, 735);
            v.score = Label("Score value", root, "0", 170, 65, 70, 735, 48);
            v.pauseButton = IconButton("Pause", root, pauseIcon, 350, 730);
            v.targetLabel = Label("Target", root, "TARGET 30", 400, 55, -200, 490, 32, Pale);
            v.timerLabel = Label("Timer", root, "TIME 60s", 400, 55, 200, 490, 32, Pale);
            Label("Demo instruction", root, "REACH THE TARGET TO WIN", 800, 60, 0, -200, 32);
            v.collectButton = Button("Collect +10", root, "COLLECT +10", yellow, 700, 125, 0, -375, 44);
            Label("Demo label", root, "DEMO CONTROLS", 800, 40, 0, -495, 22, Pale);
            v.finishButton = Button("Finish session", root, "WIN", blue, 330, 110, -185, -590, 38);
            v.loseButton = Button("Lose level", root, "LOSE", grey, 330, 110, 185, -590, 38);
        }
        private static Transform Modal(Transform root, string title, float w, float h)
        {
            var blocker = Picture("Dim and raycast blocker", root, null, 3200, 3200, 0, 0, false, new Color(0.01f, 0.025f, 0.065f, 0.84f));
            blocker.raycastTarget = true;
            var box = Picture("Panel", root, panel, w, h, 0, 0, true).transform;
            Label("Title", box, title, w - 100, 70, 0, h / 2 - 65, 44);
            return box;
        }
        private static void Settings(GameFlowView v)
        {
            var box = Modal(v.settings.transform, "SETTINGS", 820, 1200);
            v.closeSettingsButton = IconButton("Close settings", box, close, 350, 535);
            v.masterSlider = Slider(box, "Master", 300, out v.masterValue);
            v.musicSlider = Slider(box, "Music", 100, out v.musicValue);
            v.effectsSlider = Slider(box, "Effects", -100, out v.effectsValue);
            v.volumeDownButton = Button("Volume -", box, "-", blue, 55, 65, -355, 300, 28);
            v.volumeUpButton = Button("Volume +", box, "+", blue, 55, 65, 355, 300, 28);
            v.soundButton = Button("Sound toggle", box, "", grey, 380, 100, 0, -280);
            v.soundLabel = v.soundButton.GetComponentInChildren<Text>();
            v.soundLabel.text = "SOUND ON";
            v.cancelButton = Button("Cancel", box, "CANCEL", blue, 330, 110, -185, -465, 34);
            v.saveButton = Button("Save & Back", box, "SAVE", yellow, 330, 110, 185, -465, 34);
            v.settingsError = Label("Save error", box, "", 760, 65, 0, -370, 20, new Color(1, 0.7f, 0.55f));
        }
        private static Slider Slider(Transform parent, string name, float y, out Text value)
        {
            Label(name + " label", parent, name.ToUpperInvariant(), 210, 35, -220, y + 30, 22);
            value = Label(name + " value", parent, "100%", 120, 35, 260, y + 30, 23, new Color(1, 0.85f, 0.3f));
            var root = Rect(name + " slider", parent, 570, 45, 0, y - 12);
            var bg = Picture("Track", root, grey, 570, 23, 0, 0, true);
            var fillArea = Rect("Fill area", root, 550, 14, 0, 0);
            var fill = Picture("Fill", fillArea, yellow, 550, 14, 0, 0, true);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = Rect("Handle area", root, 550, 45, 0, 0);
            var handle = Picture("Handle", handleArea, Sprite("Components/Button/Button_Circle128_White.png"), 38, 38, 0, 0);
            handle.raycastTarget = true; bg.raycastTarget = true;
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            return slider;
        }
        private static void Pause(GameFlowView v)
        {
            var box = Modal(v.pause.transform, "TAKE A BREAK", 800, 1000);
            Picture("Pause icon", box, pauseIcon, 110, 110, 0, 235);
            Label("Message", box, "Your adventure will be right here.", 730, 80, 0, 80, 29, Pale);
            v.resumeButton = Button("Resume", box, "RESUME", yellow, 620, 120, 0, -110, 42);
            v.pauseHomeButton = Button("Home", box, "BACK HOME", blue, 620, 110, 0, -290, 36);
        }
        private static void Result(GameFlowView v)
        {
            var box = Modal(v.result.transform, "", 820, 1220);
            Picture("Result ribbon", box, ribbon, 800, 135, 0, 490, true);
            v.resultTitle = Label("Result title", box, "LEVEL COMPLETE!", 740, 90, 0, 495, 40);
            v.resultStars = Rect("Victory stars", box, 600, 200, 0, 280).gameObject;
            Picture("Star left", v.resultStars.transform, star, 100, 100, -135, 0);
            Picture("Star center", v.resultStars.transform, star, 140, 140, 0, 20);
            Picture("Star right", v.resultStars.transform, star, 100, 100, 135, 0);
            v.resultFailure = Rect("Failure badge", box, 400, 200, 0, 280).gameObject;
            Picture("Failure icon", v.resultFailure.transform, close, 110, 110, 0, 0, false, new Color(1, 0.5f, 0.5f));
            Label("Score label", box, "YOUR SCORE", 330, 40, -185, 110, 27, Pale);
            Label("Best label", box, "PERSONAL BEST", 330, 40, 185, 110, 27, Pale);
            v.resultScore = Label("Score", box, "0", 330, 90, -185, 30, 66);
            v.resultBest = Label("Best", box, "0", 330, 90, 185, 30, 66, new Color(1, 0.84f, 0.2f));
            v.resultSubtitle = Label("Message", box, "LEVEL COMPLETED!", 740, 70, 0, -85, 29, Pale);
            v.nextLevelButton = Button("Next Level", box, "NEXT LEVEL", yellow, 650, 110, 0, -200, 40);
            v.retryButton = Button("Retry", box, "RETRY", blue, 650, 110, 0, -325, 38);
            v.resultHomeButton = Button("Home", box, "HOME", grey, 650, 110, 0, -450, 36);
            v.saveWarning = Label("Save warning", box, "", 760, 60, 0, -545, 18);
        }
        private static void Loading(GameFlowView v)
        {
            var root = v.loading.transform; Background(root);
            Picture("Star", root, star, 160, 160, 0, 270);
            Label("Loading title", root, "GET READY!", 900, 90, 0, 90, 64);
            Picture("Progress track", root, grey, 660, 44, 0, -110, true);
            v.loadingFill = Picture("Progress fill", root, yellow, 640, 30, 0, -106);
            v.loadingFill.preserveAspect = false; v.loadingFill.type = Image.Type.Filled; v.loadingFill.fillMethod = Image.FillMethod.Horizontal;
            v.loadingText = Label("Progress text", root, "LOADING 0%", 800, 50, 0, -190, 25, Pale);
        }
        private static void Error(GameFlowView v)
        {
            var box = Modal(v.error.transform, "LET'S TRY AGAIN", 820, 900);
            v.errorText = Label("Error message", box, "", 740, 280, 0, 40, 27, Pale);
            v.errorHomeButton = Button("Return Home", box, "RETURN HOME", yellow, 650, 110, 0, -260, 38);
        }
    }
}
