using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor
{
    public enum UIPreviewScreen { Home, Store, Quests, DailyReward, Inventory, InGame, Settings, Pause, Win, Lose, Loading, Error, LevelSelection, PurchaseConfirmation, QueuedPopup, Tutorial, Toast, MoreMenu }
    public enum UIPreviewLanguage { English, Vietnamese, LongVietnamese }
    [Serializable] public sealed class UIPreviewDevice
    {
        public string name; public int width, height, top, bottom;
        public UIPreviewDevice(string name, int width, int height, int top = 0, int bottom = 0) { this.name = name; this.width = width; this.height = height; this.top = top; this.bottom = bottom; }
        public static UIPreviewDevice[] Defaults => new[] {
            new UIPreviewDevice("Phone 16:9", 720, 1280), new UIPreviewDevice("Phone 19.5:9 + notch", 1080, 2340, 110, 80),
            new UIPreviewDevice("Phone 20:9 + notch", 1080, 2400, 110, 80), new UIPreviewDevice("Tablet 3:4", 1536, 2048) };
    }
    /// <summary>Isolated editor-only preview. No Bootstrap, service initialization, SDK, input simulation or player saves.</summary>
    public sealed class UIPreviewSession : IDisposable
    {
        private Scene scene;
        private Camera camera;
        private RenderTexture texture;
        public GameObject Root { get; private set; }
        public Rect SafeArea { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        private bool vi, longText;
        private string L(string english, string vietnamese) => vi ? vietnamese : english;
        public UIPreviewSession(GameFlowConfig config, UIThemeConfig theme, UIPreviewScreen screen, UIPreviewDevice device, bool safe, UIPreviewLanguage language)
        {
            if (!config) throw new ArgumentNullException(nameof(config));
            Width = Mathf.Clamp(device.width, 320, 4096); Height = Mathf.Clamp(device.height, 320, 4096);
            vi = language != UIPreviewLanguage.English; longText = language == UIPreviewLanguage.LongVietnamese;
            SafeArea = new Rect(0, safe ? device.bottom : 0, Width, Height - (safe ? device.top + device.bottom : 0));
            scene = EditorSceneManager.NewPreviewScene();
            EditorSceneManager.SetSceneCullingMask(scene, EditorSceneManager.CalculateAvailableSceneCullingMask());
            try { Build(config, theme, screen); }
            catch { Dispose(); throw; }
        }
        private void Build(GameFlowConfig config, UIThemeConfig theme, UIPreviewScreen screen)
        {
            Root = new GameObject("BaseUnity UI Preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(Root, scene);
            var outer = Root.GetComponent<RectTransform>(); outer.sizeDelta = new Vector2(Width, Height);
            var canvas = Root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var cameraObject = new GameObject("Preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene); camera.cameraType = CameraType.Game; camera.enabled = false; camera.orthographic = true; camera.orthographicSize = Height / 2f; camera.aspect = (float)Width / Height;
            camera.transform.position = new Vector3(0, 0, -1000); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.018f, .025f, .05f); camera.nearClipPlane = .1f; camera.farClipPlane = 2000; camera.cullingMask = 1 << 30;
            Component source;
            if (screen == UIPreviewScreen.Home || screen == UIPreviewScreen.Store || screen == UIPreviewScreen.Quests || screen == UIPreviewScreen.PurchaseConfirmation) source = config.homeHubPrefab;
            else if (screen == UIPreviewScreen.DailyReward || screen == UIPreviewScreen.Inventory) source = config.offlineMenuPrefab;
            else if (screen == UIPreviewScreen.LevelSelection) source = config.levelSelectionPrefab;
            else if (screen == UIPreviewScreen.QueuedPopup || screen == UIPreviewScreen.Tutorial || screen == UIPreviewScreen.Toast || screen == UIPreviewScreen.MoreMenu) source = config.mobileModulesPrefab;
            else source = config.uiPrefab;
            if (!source) throw new InvalidOperationException("Generate editable UI prefabs first: missing " + screen);
            var instance = UnityEngine.Object.Instantiate(source.gameObject, Root.transform); instance.name = source.name;
            float fit = Mathf.Min(SafeArea.width / 900, SafeArea.height / 1600);
            Vector2 center = SafeArea.center - new Vector2(Width / 2f, Height / 2f);
            if (instance.GetComponent<Canvas>())
            {
                ((RectTransform)instance.transform).sizeDelta = new Vector2(Width, Height);
                foreach (var area in instance.GetComponentsInChildren<GameFlowSafeArea>(true))
                { area.enabled = false; area.content.localScale = Vector3.one * fit; area.content.anchoredPosition = center; }
            }
            else { instance.transform.localScale = Vector3.one * fit; ((RectTransform)instance.transform).anchoredPosition = center; }
            foreach (var c in Root.GetComponentsInChildren<Canvas>(true)) { c.renderMode = RenderMode.WorldSpace; c.worldCamera = camera; }
            foreach (var scaler in Root.GetComponentsInChildren<CanvasScaler>(true)) scaler.enabled = false;
            var instanceRect = (RectTransform)instance.transform;
            instanceRect.anchorMin = instanceRect.anchorMax = instanceRect.pivot = new Vector2(.5f, .5f);
            instanceRect.localRotation = Quaternion.identity;
            instanceRect.anchoredPosition3D = instance.GetComponent<Canvas>() ? Vector3.zero : (Vector3)center;
            instanceRect.localScale = Vector3.one * (instance.GetComponent<Canvas>() ? 1 : fit);

            foreach (var t in Root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            var hub = instance.GetComponent<HomeHubView>(); if (hub) Hub(hub, screen);
            var flow = instance.GetComponent<GameFlowView>(); if (flow) Main(flow, screen);
            var menu = instance.GetComponent<OfflineMenuView>(); if (menu) Offline(menu, screen);
            var levels = instance.GetComponent<LevelSelectionPrefabView>(); if (levels) Levels(levels);
            var mobile = instance.GetComponent<MobileModulesView>(); if (mobile) Mobile(mobile, screen);
            // Newly instantiated preview rows are assigned to the camera's isolated layer too.
            foreach (var t in Root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            foreach (var binding in Root.GetComponentsInChildren<UIThemeBinding>(true)) binding.Apply(theme);
            TranslateButtons(); Canvas.ForceUpdateCanvases();
        }
        private void Hub(HomeHubView view, UIPreviewScreen screen)
        {
            bool store = screen == UIPreviewScreen.Store, quests = screen == UIPreviewScreen.Quests;
            view.home.SetActive(!store && !quests); view.modal.SetActive(store || quests); view.SelectPage(!quests);
            view.confirmation.SetActive(screen == UIPreviewScreen.PurchaseConfirmation);
            view.wallet.text = "12,345 Coin    120 Gem"; view.playerName.text = L("PLAYER", "NGƯỜI CHƠI"); view.level.text = L("LEVEL 12", "MÀN 12"); view.gameTitle.text = longText ? "CHUYẾN PHIÊU LƯU KỲ DIỆU" : L("YOUR GAME", "GAME CỦA BẠN");
            view.questCount.text = "3"; view.questDot.SetActive(true); view.dailyDot.SetActive(true);
            view.confirmMessage.text = L("Buy this item for 50 Coin?", "Bạn muốn mua vật phẩm này với giá 50 xu?");
            if (!store && !quests) return;
            var page = quests ? view.questPage : view.storePage; page.heading.text = L(quests ? "QUESTS" : "STORE", quests ? "NHIỆM VỤ" : "CỬA HÀNG");
            page.pageNumber.text = "1 / 2"; page.message.text = ""; page.empty.SetActive(false);
            if (page.countdown) page.countdown.text = L("Resets in 08:24:15", "Làm mới sau 08:24:15");
            page.restore.gameObject.SetActive(store); page.retryPending.gameObject.SetActive(false);
            var cardPrefab = quests && view.questCardPrefab ? view.questCardPrefab : view.cardPrefab;
            for (int i = 0; i < 3; i++)
            {
                var card = UnityEngine.Object.Instantiate(cardPrefab, page.rows); card.gameObject.SetActive(true);
                card.title.text = L(quests ? "Complete three levels" : "Energy pack", quests ? "Hoàn thành ba màn chơi" : "Gói năng lượng");
                card.description.text = longText ? "Hoàn thành những thử thách hằng ngày để nhận thêm nhiều phần thưởng đặc biệt cho hành trình tiếp theo của bạn." : L("A reward for your next adventure", "Phần thưởng cho chuyến phiêu lưu tiếp theo");
                card.reward.text = quests ? "+100 Coin" : "50 Coin"; card.progress.text = "1 / 3"; card.progressRoot.SetActive(quests); card.progressFill.fillAmount = .33f;
                card.actionLabel.text = L(quests ? "CLAIM" : "BUY", quests ? "NHẬN" : "MUA"); card.action.interactable = i != 2;
            }
        }
        private void Main(GameFlowView view, UIPreviewScreen screen)
        {
            foreach (var page in new[] { view.home, view.gameplay, view.loading, view.settings, view.pause, view.result, view.error }) page.SetActive(false);
            view.fullScreenBackground.SetActive(screen == UIPreviewScreen.Loading || screen == UIPreviewScreen.Error);
            if (screen == UIPreviewScreen.InGame) { view.gameplay.SetActive(true); if (view.gameplayDemo) view.gameplayDemo.SetActive(true); view.score.text = "1,250"; view.levelLabel.text = L("LEVEL 12", "MÀN 12"); view.targetLabel.text = L("TARGET 1500", "MỤC TIÊU 1500"); view.timerLabel.text = "00:45"; }
            if (screen == UIPreviewScreen.Settings) { view.settings.SetActive(true); view.masterValue.text = "75%"; view.musicValue.text = "50%"; view.effectsValue.text = "80%"; view.masterSlider.value = .75f; view.musicSlider.value = .5f; view.effectsSlider.value = .8f; view.settingsError.text = ""; view.privacyButton.gameObject.SetActive(true); }
            if (screen == UIPreviewScreen.Pause) view.pause.SetActive(true);
            if (screen == UIPreviewScreen.Win || screen == UIPreviewScreen.Lose)
            {
                bool win = screen == UIPreviewScreen.Win; view.result.SetActive(true); view.resultStars.SetActive(win); view.resultFailure.SetActive(!win); view.nextLevelButton.gameObject.SetActive(win);
                view.resultTitle.text = L(win ? "LEVEL COMPLETE!" : "LEVEL FAILED", win ? "HOÀN THÀNH MÀN!" : "CHƯA HOÀN THÀNH"); view.resultScore.text = "1,250"; view.resultBest.text = "8,888"; view.resultSubtitle.text = L(win ? "Well played!" : "Try again!", win ? "Bạn đã làm rất tốt!" : "Hãy thử lại nhé!"); view.saveWarning.text = "";
            }
            if (screen == UIPreviewScreen.Loading) { view.loading.SetActive(true); view.loadingFill.fillAmount = .65f; view.loadingText.text = L("LOADING 65%", "ĐANG TẢI 65%"); }
            if (screen == UIPreviewScreen.Error) { view.error.SetActive(true); view.errorText.text = L("Unable to load this level. Please try again.", "Không thể tải màn chơi này. Vui lòng thử lại."); }
        }
        private void Offline(OfflineMenuView view, UIPreviewScreen screen)
        {
            view.enabled = false; var group = view.GetComponent<CanvasGroup>(); group.alpha = 1; group.interactable = true;
            view.balance.text = "12,345 Coin    120 Gem"; view.message.text = ""; view.loading.SetActive(false);
            string key = screen == UIPreviewScreen.DailyReward ? "DAILY REWARD" : "INVENTORY";
            foreach (var tab in view.tabs)
            {
                bool active = tab.key == key; tab.panel.gameObject.SetActive(active); tab.selected.SetActive(active); if (!active) continue;
                var panel = tab.panel; panel.title.text = L(key, key == "INVENTORY" ? "TÚI ĐỒ" : "QUÀ HẰNG NGÀY"); panel.empty.gameObject.SetActive(false); panel.pageLabel.text = "1 / 1";
                for (int i = 0; i < 2; i++) { var row = UnityEngine.Object.Instantiate(panel.rowPrefab, panel.rows); row.title.text = L(key == "INVENTORY" ? "Energy x5" : "Day " + (i + 1) + "   +100 Coin", key == "INVENTORY" ? "Năng lượng x5" : "Ngày " + (i + 1) + "   +100 xu"); row.actionLabel.text = L("CLAIM", "NHẬN QUÀ"); row.action.gameObject.SetActive(key != "INVENTORY"); }
            }
        }
        private void Levels(LevelSelectionPrefabView view)
        {
            view.completed.text = L("12 / 30 COMPLETED", "ĐÃ HOÀN THÀNH 12 / 30"); view.pageNumber.text = "3 / 5";
            for (int i = 0; i < 6; i++) { var cell = UnityEngine.Object.Instantiate(view.cellPrefab, view.cells); cell.gameObject.SetActive(true); cell.GetComponentInChildren<Text>().text = (i + 13) + "\n" + L(i == 0 ? "PLAY" : "LOCKED", i == 0 ? "CHƠI" : "CHƯA MỞ"); cell.interactable = i == 0; }
        }
        private void Mobile(MobileModulesView view, UIPreviewScreen screen)
        {
            view.menu.gameObject.SetActive(screen == UIPreviewScreen.MoreMenu); view.menuPanel.gameObject.SetActive(true);
            view.popup.gameObject.SetActive(screen == UIPreviewScreen.QueuedPopup); view.tutorial.gameObject.SetActive(screen == UIPreviewScreen.Tutorial); view.toast.gameObject.SetActive(screen == UIPreviewScreen.Toast); view.loading.gameObject.SetActive(false);
            view.popupTitle.text = L("CONFIRM", "XÁC NHẬN"); view.popupMessage.text = L("Continue with this action?", "Bạn có muốn tiếp tục thực hiện thao tác này không?"); view.toastMessage.text = L("Reward received!", "Đã nhận phần thưởng!"); view.tutorialMessage.text = L("Tap Play to begin your adventure.", "Bấm Chơi để bắt đầu chuyến phiêu lưu của bạn.");
            foreach (var mask in view.masks) mask.gameObject.SetActive(false); foreach (var border in view.borders) border.gameObject.SetActive(false);
            if (screen == UIPreviewScreen.MoreMenu)
            {
                for (int i = 0; i < 4; i++) { var button = UnityEngine.Object.Instantiate(view.buttonTemplate, view.menuPanel); button.gameObject.SetActive(true); ((RectTransform)button.transform).anchoredPosition = new Vector2(0, 450 - i * 120); button.GetComponentInChildren<Text>().text = new[] { L("HAPTICS ON", "RUNG ĐANG BẬT"), L("NOTIFICATIONS", "THÔNG BÁO"), L("RESTORE PURCHASES", "KHÔI PHỤC GIAO DỊCH"), L("CLOSE", "ĐÓNG") }[i]; }
            }
        }
        private void TranslateButtons()
        {
            if (!vi) return;
            var values = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "PLAY", "CHƠI" }, { "CHOOSE LEVEL", "CHỌN MÀN" }, { "SETTINGS", "CÀI ĐẶT" }, { "STORE", "CỬA HÀNG" }, { "QUESTS", "NHIỆM VỤ" }, { "DAILY REWARD", "QUÀ HẰNG NGÀY" }, { "INVENTORY", "TÚI ĐỒ" }, { "MORE", "THÊM" }, { "CANCEL", "HỦY" }, { "SAVE", "LƯU" }, { "HOME", "TRANG CHỦ" }, { "BACK HOME", "VỀ TRANG CHỦ" }, { "RESUME", "TIẾP TỤC" }, { "NEXT LEVEL", "MÀN TIẾP THEO" }, { "RETRY", "CHƠI LẠI" }, { "PRIVACY OPTIONS", "QUYỀN RIÊNG TƯ" }, { "DAILY QUESTS", "NHIỆM VỤ NGÀY" }, { "ACHIEVEMENTS", "THÀNH TỰU" }, { "CONFIRM", "XÁC NHẬN" }, { "CLOSE", "ĐÓNG" } };
            foreach (var label in Root.GetComponentsInChildren<Text>(true)) if (values.TryGetValue(label.text ?? "", out var value)) label.text = value;
        }
        public RenderTexture Render(int maxHeight = 1000)
        {
            int height = Mathf.Min(Height, maxHeight), width = Mathf.Max(1, Mathf.RoundToInt(height * (float)Width / Height));
            if (!texture || texture.width != width || texture.height != height)
            { if (texture) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); } texture = new RenderTexture(width, height, 24) { name = "BaseUnity UI Preview" }; texture.Create(); }
            camera.targetTexture = texture; Canvas.ForceUpdateCanvases();
            foreach (var graphic in Root.GetComponentsInChildren<Graphic>()) graphic.Rebuild(CanvasUpdate.PreRender);
            camera.Render(); return texture;
        }
        public void ExportPng(string path)
        {
            var rendered = Render(1600); var previous = RenderTexture.active; Texture2D image = null;
            try { RenderTexture.active = rendered; image = new Texture2D(rendered.width, rendered.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0); image.Apply(); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))); System.IO.File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; if (image) UnityEngine.Object.DestroyImmediate(image); }
        }
        public void Dispose()
        {
            if (camera) camera.targetTexture = null;
            if (texture) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null; }
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            Root = null;
        }
    }
}
