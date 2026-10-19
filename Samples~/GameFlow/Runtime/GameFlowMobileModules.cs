using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Portable presenter for popup, feedback, tutorial and optional mobile settings/store.</summary>
    public sealed class GameFlowMobileModules : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private MobileModulesView prefab;
        private readonly List<GameObject> menuItems = new List<GameObject>();
        private FeatureServices services;
        private RectTransform root, modal, tutorialRoot, toastRoot, loadingRoot;
        private GameObject menu;
        private Text toastText, loadingText;
        private Font font;
        private Sprite art;
        private IDisposable presenter;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Queue<ToastMessage> toasts = new Queue<ToastMessage>();
        private readonly HashSet<int> wired = new HashSet<int>();
        private float toastUntil, wireAt;
        private bool dirty = true, menuOpen, pausedForPopup, tutorialAttempted;
        private FlowPhase previous;
        private string activePopup;
        private int storePage;
        private RectTransform[] masks, borders;
        private Text tutorialText;
        private Button tutorialNext, tutorialSkip;
        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner; services = flow.Services.Get<FeatureServices>(); font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var view = GetComponentInChildren<GameFlowView>(); if (view && view.playButton) art = view.playButton.image.sprite;
            if (flow.Config.mobileModulesPrefab)
            {
                prefab = Instantiate(flow.Config.mobileModulesPrefab, transform); root = prefab.content;
                menu = prefab.menu.gameObject; modal = prefab.popup;
                toastRoot = prefab.toast; toastText = prefab.toastMessage;
                loadingRoot = prefab.loading; loadingText = prefab.loadingMessage;
            }
            else
            {
            var canvasObject = new GameObject("Mobile Modules Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 180;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = .5f;
            root = Rect(canvasObject.transform, "Safe Area", 900, 1600, 0, 0);
            var safe = canvasObject.AddComponent<GameFlowSafeArea>(); safe.canvas = canvas; safe.content = root;
            }
            presenter = services.Popups.AttachPresenter();
            services.Popups.Changed += Dirty; services.Feedback.Changed += Dirty; services.Feedback.ToastRequested += Toast;
            services.Tutorial.Changed += Dirty; services.Purchases.Changed += Dirty; services.Localization.Changed += Dirty;
            flow.Changed += FlowChanged;
            if (!prefab) { toastRoot = Box(root, "Toast", 800, 90, 0, 600, false); toastText = Label(toastRoot, "", 0, 0, 760, 80, 26); toastRoot.gameObject.SetActive(false);
            loadingRoot = Box(root, "Loading indicator", 700, 100, 0, -500, false); loadingText = Label(loadingRoot, "", 0, 0, 660, 90, 28); loadingRoot.gameObject.SetActive(false);
            }
            BuildTutorial(); WireTargets();
        }
        private string T(string text) => services.Localization.Text(text ?? "");
        public bool IsMenuOpen => menuOpen;
        public bool IsPopupVisible => modal && modal.gameObject.activeInHierarchy;
        public void OpenMenu() { if (flow.Phase == FlowPhase.Home) { menuOpen = true; Dirty(); } }
        private RectTransform Rect(Transform parent, string name, float w, float h, float x, float y)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); return rect;
        }
        private RectTransform Box(Transform parent, string name, float w, float h, float x, float y, bool raycast = true)
        {
            if (prefab) { var panel = Instantiate(prefab.panelTemplate, parent); panel.gameObject.SetActive(true); panel.name = name; panel.sizeDelta = new Vector2(w, h); panel.anchoredPosition = new Vector2(x, y); panel.GetComponent<Image>().raycastTarget = raycast; return panel; }
            var rect = Rect(parent, name, w, h, x, y); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.04f, .07f, .13f, .96f); image.raycastTarget = raycast; return rect;
        }
        private Text Label(Transform parent, string text, float x, float y, float w = 740, float h = 80, int size = 30)
        {
            if (prefab) { var textView = Instantiate(prefab.labelTemplate, parent); textView.gameObject.SetActive(true); textView.text = T(text); textView.rectTransform.anchoredPosition = new Vector2(x, y); textView.rectTransform.sizeDelta = new Vector2(w, h); menuItems.Add(textView.gameObject); return textView; }
            var label = Rect(parent, "Text", w, h, x, y).gameObject.AddComponent<Text>(); label.font = font; label.text = T(text); label.fontSize = size; label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
        private Button Button(Transform parent, string text, float x, float y, Action action, float width = 660)
        {
            if (prefab) { var control = Instantiate(prefab.buttonTemplate, parent); control.gameObject.SetActive(true); control.name = text; ((RectTransform)control.transform).anchoredPosition = new Vector2(x, y); ((RectTransform)control.transform).sizeDelta = new Vector2(width, ((RectTransform)prefab.buttonTemplate.transform).sizeDelta.y); control.GetComponentInChildren<Text>().text = T(text); control.onClick.AddListener(() => { services.Haptics.Play(); action(); }); menuItems.Add(control.gameObject); return control; }
            var rect = Rect(parent, text, width, 80, x, y); var image = rect.gameObject.AddComponent<Image>(); image.sprite = art; image.type = Image.Type.Sliced; image.color = art ? Color.white : new Color(.15f, .38f, .55f);
            var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => { services.Haptics.Play(); action(); }); Label(rect, text, 0, 0, width - 24, 70, 26); return button;
        }
        private void Dirty() { dirty = true; }
        private void FlowChanged()
        {
            if (!this || !flow || !flow.IsReady) return;
            if (previous != flow.Phase)
            {
                previous = flow.Phase; menuOpen = false;
                if (flow.Phase == FlowPhase.Loading) services.Popups.CancelScope("scene");
                if (flow.Phase != FlowPhase.Home) tutorialAttempted = false;
                if (flow.Phase == FlowPhase.Result) services.Haptics.Play(flow.Outcome == LevelOutcome.Win ? HapticKind.Success : HapticKind.Failure);
            }
            WireTargets(); dirty = true;
        }
        private void Toast(ToastMessage toast) { if (toasts.Count < 8) toasts.Enqueue(toast); }
        private void LateUpdate()
        {
            if (services == null || !flow || !flow.IsReady) return;
            if (UnityEngine.Time.unscaledTime >= wireAt) { wireAt = UnityEngine.Time.unscaledTime + .25f; WireTargets(); }
            bool offlineOpen = GetComponent<GameFlowOfflineUI>()?.IsOpen == true;
            bool blocked = services.Consent.IsBusy || flow.Features.Busy || flow.Phase == FlowPhase.Loading;
            bool dialog = services.Popups.Active != null && !services.Popups.IsSuspended && !blocked && !offlineOpen;
            if (dialog && flow.Phase == FlowPhase.Playing) { pausedForPopup = true; flow.Pause(); }
            if (!dialog && pausedForPopup) { pausedForPopup = false; if (flow.Phase == FlowPhase.Paused) flow.Resume(); }
            if (!tutorialAttempted && flow.Phase == FlowPhase.Home && services.Tutorial.AutoStartAtHome) { tutorialAttempted = true; services.Tutorial.StartNext(); }
            if (dirty || activePopup != services.Popups.Active?.Id)
            { dirty = false; BuildMenu(); BuildPopup(); }
            if (modal) modal.gameObject.SetActive(dialog);
            if (menu) menu.SetActive(!dialog && !blocked && !offlineOpen && flow.Phase == FlowPhase.Home);
            loadingRoot.gameObject.SetActive(services.Feedback.IsLoading); loadingText.text = T(services.Feedback.LoadingMessage);
            if (!toastRoot.gameObject.activeSelf || UnityEngine.Time.unscaledTime >= toastUntil)
            {
                toastRoot.gameObject.SetActive(false);
                if (toasts.Count > 0) { var toast = toasts.Dequeue(); toastText.text = T(toast.text); if (!prefab) toastText.color = toast.error ? new Color(1, .5f, .45f) : Color.white; toastUntil = UnityEngine.Time.unscaledTime + toast.seconds; toastRoot.gameObject.SetActive(true); }
            }
            UpdateTutorial(!dialog && !menuOpen && !blocked && !offlineOpen);
            toastRoot.SetAsLastSibling(); loadingRoot.SetAsLastSibling();
        }
        private void BuildMenu()
        {
            foreach (var item in menuItems) { if (item) { item.SetActive(false); Destroy(item); } } menuItems.Clear();
            if (!prefab) { if (menu) { menu.SetActive(false); Destroy(menu); } menu = Rect(root, "Mobile settings and store", 900, 1600, 0, 0).gameObject; }
            else prefab.menuPanel.gameObject.SetActive(menuOpen);
            if (!menuOpen) { if (!flow.Config.homeHubPrefab) Button(menu.transform, "MORE", -300, -730, OpenMenu, 240); return; }
            if (!prefab) Box(menu.transform, "Blocker", 4000, 4000, 0, 0);
            var panel = prefab ? prefab.menuPanel : Box(menu.transform, "Settings", 820, 1400, 0, 0); Label(panel, "SETTINGS & STORE", 0, 600, 740, 80, 38);
            float y = 470;
            if (services.Haptics.ModuleEnabled) { Button(panel, services.Haptics.Enabled ? "HAPTICS ON" : "HAPTICS OFF", 0, y, () => { services.Haptics.SetUserEnabled(!services.Haptics.Enabled); Dirty(); }); y -= 100; }
            if (services.Notifications.Enabled) { Button(panel, services.Notifications.OptedIn ? "NOTIFICATIONS ON" : "ENABLE NOTIFICATIONS", 0, y, () => { _ = ToggleNotifications(); }).interactable = !services.Notifications.IsBusy; y -= 100; }
            if (services.Tutorial.Enabled) { Button(panel, "TUTORIAL", 0, y, () => { menuOpen = false; services.Tutorial.StartNext(); Dirty(); }); y -= 100; }
            if (services.Purchases.Enabled)
            {
                var products = services.Purchases.Products;
                foreach (var product in products.Skip(storePage * 3).Take(3))
                {
                    var captured = product;
                    var button = Button(panel, product.title + "  " + product.price, 0, y, () => { _ = Buy(captured.id); }); button.interactable = product.available && !services.Purchases.IsBusy; y -= 95;
                }
                if (products.Count == 0) { Label(panel, "Store unavailable", 0, y); y -= 90; }
                if (products.Count > 3) { Button(panel, "NEXT", 0, y, () => { storePage = (storePage + 1) % ((products.Count + 2) / 3); Dirty(); }); y -= 90; }
                Button(panel, "RESTORE PURCHASES", 0, y, () => { _ = Restore(); }); y -= 90;
                if (services.Purchases.PendingCount > 0) Button(panel, "RETRY PURCHASE DELIVERY", 0, y, () => { _ = RetryPurchases(); });
            }
            Button(panel, "CLOSE", 0, -600, () => { menuOpen = false; Dirty(); });
        }
        private void BuildPopup()
        {
            if (prefab)
            {
                activePopup = services.Popups.Active?.Id; if (activePopup == null) { modal.gameObject.SetActive(false); return; }
                var value = services.Popups.Active.Content; string popupId = activePopup;
                prefab.popupTitle.text = T(value.title); prefab.popupMessage.text = T(value.message);
                prefab.popupAccept.GetComponentInChildren<Text>().text = T(value.accept ?? "OK");
                prefab.popupCancel.GetComponentInChildren<Text>().text = T(value.cancel);
                prefab.popupCancel.gameObject.SetActive(!string.IsNullOrEmpty(value.cancel));
                prefab.popupAccept.onClick.RemoveAllListeners(); prefab.popupCancel.onClick.RemoveAllListeners();
                prefab.popupAccept.onClick.AddListener(() => services.Popups.Resolve(popupId, true));
                prefab.popupCancel.onClick.AddListener(() => services.Popups.Resolve(popupId, false)); return;
            }
            if (modal) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }
            activePopup = services.Popups.Active?.Id; if (activePopup == null) { modal = null; return; }
            modal = Rect(root, "Queued popup", 900, 1600, 0, 0); Box(modal, "Blocker", 4000, 4000, 0, 0);
            var panel = Box(modal, "Dialog", 820, 650, 0, 0); var content = services.Popups.Active.Content; string id = activePopup;
            Label(panel, content.title, 0, 210, 740, 100, 38); Label(panel, content.message, 0, 40, 740, 210, 28);
            Button(panel, content.accept ?? "OK", 0, -130, () => services.Popups.Resolve(id, true));
            if (!string.IsNullOrEmpty(content.cancel)) Button(panel, content.cancel, 0, -230, () => services.Popups.Resolve(id, false));
        }
        private async Task ToggleNotifications()
        {
            Dirty(); var notifications = services.Notifications;
            bool ok;
            if (notifications.OptedIn)
            {
                if (services.Popups.Enabled && services.Feedback.Enabled && !await services.Feedback.ConfirmAsync("Disable notifications?", "Daily reminders will be cancelled.", "notifications_opt_out")) return;
                ok = notifications.Disable();
            }
            else { ok = await notifications.EnableAsync(lifetime.Token); if (ok) ok = notifications.ScheduleDailyReminder(DateTime.UtcNow.Date.AddDays(1).AddMinutes(1)); }
            if (!this) return; services.Feedback.Toast(ok ? "Saved" : "Notifications unavailable", !ok); Dirty();
        }
        private async Task Buy(string id) { bool started = await services.Purchases.BuyAsync(id); if (this && !started) services.Feedback.Toast("Purchase unavailable", true); }
        private async Task Restore() { using (services.Feedback.Loading("Restoring purchases...")) { bool ok = await services.Purchases.RestoreAsync(); if (this) services.Feedback.Toast(ok ? "Restore completed" : "Restore unavailable", !ok); } }
        private async Task RetryPurchases() { await services.Purchases.RetryPendingAsync(); if (this) services.Feedback.Toast(services.Purchases.PendingCount == 0 ? "Saved" : "Purchase pending", services.Purchases.PendingCount != 0); }
        private void WireTargets()
        {
            var buttons = flow.GetComponentsInChildren<Button>(true); wired.IntersectWith(buttons.Select(x => x.GetInstanceID()));
            foreach (var button in buttons)
            {
                if (!wired.Add(button.GetInstanceID())) continue;
                if (!button.transform.IsChildOf(root)) button.onClick.AddListener(() => services.Haptics.Play());
                string key = button.name == "Play" || button.name == "Play game" ? "home.play" : button.name == "Collect +10" ? "gameplay.collect" : null;
                if (key != null) { var target = button.GetComponent<TutorialTarget>() ?? button.gameObject.AddComponent<TutorialTarget>(); target.Initialize(key, services.Tutorial); }
            }
            // Template view button names can differ after an art replacement.
            var view = flow.GetComponentInChildren<GameFlowView>();
            if (view) { Bind(view.playButton, "home.play"); Bind(view.collectButton, "gameplay.collect"); }
        }
        private void Bind(Button button, string key)
        { if (button && !button.GetComponent<TutorialTarget>()) button.gameObject.AddComponent<TutorialTarget>().Initialize(key, services.Tutorial); }
        private void BuildTutorial()
        {
            if (prefab)
            {
                tutorialRoot = prefab.tutorial; masks = prefab.masks; borders = prefab.borders;
                tutorialText = prefab.tutorialMessage; tutorialNext = prefab.tutorialNext; tutorialSkip = prefab.tutorialSkip;
                tutorialNext.onClick.AddListener(() => { if (!services.Tutorial.Advance()) services.Feedback.Toast("Could not save. Try again.", true); });
                tutorialSkip.onClick.AddListener(() => { if (!services.Tutorial.Skip()) services.Feedback.Toast("Could not save. Try again.", true); });
                tutorialRoot.gameObject.SetActive(false); return;
            }
            tutorialRoot = Rect(root, "Tutorial overlay", 900, 1600, 0, 0); masks = new RectTransform[4]; borders = new RectTransform[4];
            for (int i = 0; i < 4; i++) { masks[i] = Box(tutorialRoot, "Outside target", 0, 0, 0, 0); masks[i].GetComponent<Image>().color = new Color(0, 0, 0, .65f); borders[i] = Box(tutorialRoot, "Highlight", 0, 0, 0, 0, false); borders[i].GetComponent<Image>().color = new Color(.3f, 1, .7f); }
            var panel = Box(tutorialRoot, "Instructions", 820, 270, 0, 530);
            tutorialText = Label(panel, "", 0, 65, 760, 120, 28);
            tutorialNext = Button(panel, "NEXT", -185, -70, () => { if (!services.Tutorial.Advance()) services.Feedback.Toast("Could not save. Try again.", true); }, 330);
            tutorialSkip = Button(panel, "SKIP", 185, -70, () => { if (!services.Tutorial.Skip()) services.Feedback.Toast("Could not save. Try again.", true); }, 330);
            tutorialRoot.gameObject.SetActive(false);
        }
        private void UpdateTutorial(bool allowed)
        {
            var step = services.Tutorial.Current;
            tutorialRoot.gameObject.SetActive(step != null && allowed && (flow.Phase == FlowPhase.Home || flow.Phase == FlowPhase.Playing)); if (!tutorialRoot.gameObject.activeSelf) return;
            var target = flow.GetComponentsInChildren<TutorialTarget>().FirstOrDefault(x => x.key == step.target && x.gameObject.activeInHierarchy && (x.GetComponentInParent<CanvasGroup>() == null || x.GetComponentInParent<CanvasGroup>().interactable))?.transform as RectTransform;
            bool hasTarget = target != null;
            tutorialText.text = T(step.message); tutorialNext.gameObject.SetActive(!step.waitForTargetAction); tutorialSkip.gameObject.SetActive(services.Tutorial.CanSkip);
            foreach (var mask in masks) { mask.gameObject.SetActive(hasTarget); mask.GetComponent<Image>().raycastTarget = step.blockOutsideTarget; }
            foreach (var border in borders) border.gameObject.SetActive(hasTarget);
            if (!hasTarget) return; // Never lock the screen if a game-specific target is absent.
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var sourceCanvas = target.GetComponentInParent<Canvas>(); Camera camera = sourceCanvas && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera, corners[0]), null, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera, corners[2]), null, out var max);
            float left = Mathf.Clamp(min.x - 8, -450, 450), right = Mathf.Clamp(max.x + 8, -450, 450), bottom = Mathf.Clamp(min.y - 8, -800, 800), top = Mathf.Clamp(max.y + 8, -800, 800);
            Set(masks[1], -450, -800, 450, bottom); Set(masks[2], -450, bottom, left, top); Set(masks[3], right, bottom, 450, top);
            // Top mask starts above the target, leaving the target's rectangle clickable.
            Set(masks[0], -450, top, 450, 800);
            Set(borders[0], left, top - 3, right, top); Set(borders[1], left, bottom, right, bottom + 3);
            Set(borders[2], left, bottom, left + 3, top); Set(borders[3], right - 3, bottom, right, top);
        }
        private static void Set(RectTransform rect, float left, float bottom, float right, float top) { rect.sizeDelta = new Vector2(Mathf.Max(0, right - left), Mathf.Max(0, top - bottom)); rect.anchoredPosition = new Vector2((left + right) / 2, (bottom + top) / 2); }
        private void OnApplicationPause(bool paused)
        {
            if (paused && services != null && services.Notifications.OptedIn && services.Offline.DailyEnabled && !services.Offline.CanClaimDaily)
                services.Notifications.ScheduleDailyReminder(DateTime.UtcNow.Date.AddDays(1).AddMinutes(1));
        }
        private void OnDestroy()
        {
            lifetime.Cancel();
            if (services != null)
            {
                services.Popups.Changed -= Dirty; services.Feedback.Changed -= Dirty; services.Feedback.ToastRequested -= Toast;
                services.Tutorial.Changed -= Dirty; services.Purchases.Changed -= Dirty; services.Localization.Changed -= Dirty;
            }
            if (flow) flow.Changed -= FlowChanged; presenter?.Dispose(); lifetime.Dispose();
        }
    }
}
