#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BaseUnity.Core;
using BaseUnity.Features;
using BaseUnity.Pooling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Entire presenter and test actions are excluded from non-development players.</summary>
    public sealed class GameFlowDebugPanel : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private FeatureServices services;
        private RectTransform root;
        private GameObject panel, launcher;
        private Text status;
        private Font font;
        private IDisposable suspension;
        private readonly Queue<string> errors = new Queue<string>();
        private readonly List<Button> controls = new List<Button>();
        private bool opened, pausedByPanel, confirmReset, resetting;
        private float refreshAt, frameSeconds;
        public bool IsOpen => opened;
        public string Snapshot { get; private set; }
        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner; services = owner.Services.Get<FeatureServices>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Development Debug Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = .5f;
            root = Rect(canvasObject.transform, "Safe area", 900, 1600, 0, 0);
            var safe = canvasObject.AddComponent<GameFlowSafeArea>(); safe.canvas = canvas; safe.content = root;
            launcher = Button(root, "DEBUG", -300, 660, 220, Toggle).gameObject;
            panel = Rect(root, "Debug panel", 900, 1600, 0, 0).gameObject;
            var shade = panel.AddComponent<Image>(); shade.color = new Color(.025f, .04f, .065f, .99f);
            Label(panel.transform, "BASEUNITY • " + BuildEnvironment.Name, 0, 730, 830, 60, 32);
            var viewport = Rect(panel.transform, "Diagnostics scroll", 830, 990, 0, 170);
            viewport.gameObject.AddComponent<Image>().color = new Color(.07f, .09f, .13f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.viewport = viewport;
            status = Label(viewport, "", 0, 0, 800, 0, 25); status.alignment = TextAnchor.UpperLeft;
            var content = status.rectTransform; content.anchorMin = content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero; content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            AddControl("WIN", -270, -420, () => { Close(); flow.WinLevel(); });
            AddControl("LOSE", 0, -420, () => { Close(); flow.LoseLevel(); });
            AddControl("NEXT", 270, -420, () => { Close(); flow.NextLevel(); });
            AddControl("PLAY / RETRY", -270, -510, () => { Close(); flow.Play(); });
            AddControl("HOME", 0, -510, () => { Close(); flow.GoHome(); });
            Button(panel.transform, "COPY STATUS", 270, -510, 250, () => GUIUtility.systemCopyBuffer = Snapshot ?? "");
            Button(panel.transform, "CLEAR ERRORS", -210, -610, 380, () => errors.Clear());
            Button(panel.transform, "RESET TEST DATA", 210, -610, 380, () => { confirmReset = !confirmReset; Refresh(); });
            Button(panel.transform, "CLOSE", -210, -720, 380, Close);
            var reset = Button(panel.transform, "CONFIRM RESET", 210, -720, 380, ResetTestData); controls.Add(reset);
            panel.SetActive(false);
            Application.logMessageReceived += CaptureLog;
            Refresh();
        }
        private RectTransform Rect(Transform parent, string name, float w, float h, float x, float y)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); return rect;
        }
        private Text Label(Transform parent, string text, float x, float y, float w, float h, int size)
        {
            var label = Rect(parent, "Text", w, h, x, y).gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size;
            label.text = text; label.supportRichText = false; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
        private Button Button(Transform parent, string text, float x, float y, float width, Action action)
        {
            var rect = Rect(parent, text, width, 72, x, y); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .28f, .4f);
            var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => action()); Label(rect, text, 0, 0, width - 12, 68, 23); return button;
        }
        private void AddControl(string name, float x, float y, Action action) => controls.Add(Button(panel.transform, name, x, y, 250, () => {
            if (!NativeBusy && flow.Phase != FlowPhase.Loading) action();
        }));
        private bool NativeBusy => services.Consent.IsBusy || services.Ads.IsBusy || flow.Features.Busy || services.Purchases.IsBusy || services.Notifications.IsBusy;
        public void Toggle()
        {
            if (opened) { Close(); return; }
            if (NativeBusy || flow.Phase == FlowPhase.Loading || flow.Phase == FlowPhase.Boot) return;
            opened = true; confirmReset = false; pausedByPanel = flow.Phase == FlowPhase.Playing;
            suspension = services.Popups.Suspend();
            if (pausedByPanel) flow.Pause();
            panel.SetActive(true); launcher.SetActive(false); Refresh();
        }
        public void Close()
        {
            if (!opened) return;
            opened = false; confirmReset = false;
            panel.SetActive(false); launcher.SetActive(true);
            suspension?.Dispose(); suspension = null;
            // Release only our pause; popup presenter can acquire its own pause in LateUpdate.
            if (pausedByPanel && flow.Phase == FlowPhase.Paused) flow.Resume();
            pausedByPanel = false;
        }
        private void Update()
        {
            frameSeconds = Mathf.Lerp(frameSeconds, UnityEngine.Time.unscaledDeltaTime, .1f);
            if (flow == null || !flow.IsReady) return;
            if (!opened) launcher.SetActive(!NativeBusy && flow.Phase != FlowPhase.Loading && flow.Phase != FlowPhase.Boot);
            if (opened && UnityEngine.Time.unscaledTime >= refreshAt) { refreshAt = UnityEngine.Time.unscaledTime + .5f; Refresh(); }
        }
        private void CaptureLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (errors.Count == 12) errors.Dequeue();
            errors.Enqueue(type + ": " + Clip(message));
        }
        private static string Clip(string value) => string.IsNullOrEmpty(value) ? "—" : value.Substring(0, Math.Min(500, value.Length));
        private void Refresh()
        {
            if (!flow || !flow.IsReady) return;
            var text = new StringBuilder();
            text.AppendLine(Application.identifier + "  v" + Application.version + " • " + Application.unityVersion);
            text.AppendLine("Scene: " + SceneManager.GetActiveScene().name + " • " + flow.Phase + " • level " + flow.CurrentLevel + " • score " + flow.Score);
            text.AppendLine("FPS " + (frameSeconds > 0 ? (1 / frameSeconds).ToString("F0") : "—") + " • managed " + (GC.GetTotalMemory(false) / 1048576f).ToString("F1") + " MB • reserved " + (Profiler.GetTotalReservedMemoryLong() / 1048576f).ToString("F1") + " MB");
            text.AppendLine("GC collections " + GC.CollectionCount(0) + "/" + GC.CollectionCount(1) + "/" + GC.CollectionCount(2));
            text.AppendLine("Ads: " + services.Ads.Status + " • no ads=" + services.Ads.NoAds);
            text.AppendLine("Ready B/I/R: " + services.Ads.IsReady(AdFormat.Banner) + "/" + services.Ads.IsReady(AdFormat.Interstitial) + "/" + services.Ads.IsReady(AdFormat.Rewarded));
            text.AppendLine("Consent: " + services.Consent.Status + " • may request ads=" + services.Consent.CanRequestAds);
            text.AppendLine("Firebase: " + services.Firebase.Status + " • IAP: " + services.Purchases.Status + " • pending=" + services.Purchases.PendingCount);
            text.AppendLine("Assets enabled=" + services.Assets.Enabled + " • leases=" + services.Assets.ActiveLeases);
            foreach (var pool in PoolDiagnostics.Capture()) text.AppendLine("Pool " + pool.Name + ": " + pool.Active + " active / " + pool.Inactive + " idle / " + pool.Total + " total");
            text.AppendLine("Popup queue=" + services.Popups.PendingCount + " • active=" + services.Popups.Active?.Id + " • loading=" + services.Feedback.IsLoading);
            text.AppendLine("Tutorial=" + services.Tutorial.ActiveId + " • haptics=" + services.Haptics.Enabled);
            text.AppendLine("Notifications=" + services.Notifications.Enabled + " • opted in=" + services.Notifications.OptedIn + " • permission=" + services.Notifications.Permission);
            text.AppendLine("Coins=" + services.Offline.Coins + " • gems=" + services.Offline.Gems + " • recovered backup=" + services.Offline.RecoveredFromBackup);
            text.AppendLine("Save: " + BuildEnvironment.SaveDirectory);
            AppendError(text, "Ads", services.Ads.LastError); AppendError(text, "Consent", services.Consent.LastError);
            AppendError(text, "Firebase", services.Firebase.LastError); AppendError(text, "IAP", services.Purchases.LastError);
            AppendError(text, "Assets", services.Assets.LastError); AppendError(text, "Tutorial", services.Tutorial.LastError);
            AppendError(text, "Haptics", services.Haptics.LastError); AppendError(text, "Notifications", services.Notifications.LastError);
            AppendError(text, "Save", services.Offline.LastError); AppendError(text, "Flow", flow.ErrorMessage);
            foreach (var error in errors) text.AppendLine(error);
            if (confirmReset) text.AppendLine("RESET: return Home, then Confirm. Archives this test save folder and restarts Bootstrap. SDK consent / store purchases / PlayerPrefs remain external.");
            Snapshot = text.ToString(); status.text = Snapshot;
            foreach (var button in controls) button.interactable = !NativeBusy && flow.Phase != FlowPhase.Loading;
            controls[0].interactable &= flow.Phase == FlowPhase.Playing || (pausedByPanel && flow.Phase == FlowPhase.Paused);
            controls[1].interactable = controls[0].interactable;
            controls[2].interactable &= flow.CanNextLevel;
            controls[3].interactable &= flow.Phase == FlowPhase.Home || flow.Phase == FlowPhase.Result;
            controls[controls.Count - 1].interactable &= confirmReset && flow.Phase == FlowPhase.Home && services.Purchases.PendingCount == 0 && !string.IsNullOrEmpty(flow.StartupScenePath);
        }
        private static void AppendError(StringBuilder text, string name, string error)
        { if (!string.IsNullOrEmpty(error)) text.AppendLine(name + ": " + Clip(error)); }
        private void ResetTestData()
        {
            if (resetting || !confirmReset || !BuildEnvironment.IsDevelopment || NativeBusy || flow.Phase != FlowPhase.Home || services.Purchases.PendingCount > 0) return;
            string scene = flow.StartupScenePath;
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return;
            if (!services.Notifications.Disable()) { CaptureLog("Could not cancel notification reminders; reset cancelled.", "", LogType.Error); return; }
            resetting = true; panel.SetActive(false); launcher.SetActive(false); enabled = false;
            var runner = new GameObject("Test save reset").AddComponent<GameFlowTestReset>();
            DontDestroyOnLoad(runner.gameObject); runner.Begin(flow.gameObject, scene);
        }
        private void OnDestroy()
        { Application.logMessageReceived -= CaptureLog; suspension?.Dispose(); suspension = null; }
    }
    public sealed class GameFlowTestReset : MonoBehaviour
    {
        public void Begin(GameObject owner, string scene) => StartCoroutine(Reset(owner, scene));
        private IEnumerator Reset(GameObject owner, string scene)
        {
            Destroy(owner);
            yield return null; // All service owners dispose before archiving; late saves cannot recreate old state.
            try
            {
                if (!BuildEnvironment.IsDevelopment) throw new InvalidOperationException("Reset is only available for test saves.");
                string backup = BuildEnvironment.ArchiveTestSaves(Application.persistentDataPath, BuildEnvironment.Name);
                Debug.Log("Test save reset; backup=" + (backup ?? "no previous test data"));
            }
            catch (Exception error) { Debug.LogException(error); }
            UnityEngine.Time.timeScale = 1;
            var loading = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (loading != null) yield return loading;
            Destroy(gameObject);
        }
    }
}
#endif
