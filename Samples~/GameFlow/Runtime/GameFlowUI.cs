using System;
using BaseUnity.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Editable sample UI. Replace this view with project prefabs while retaining GameFlowBootstrap.</summary>
    public sealed class GameFlowUI : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private Transform canvas;
        private GameObject page;
        private Text loadingLabel;
        private Text timerLabel;
        private Font font;
        private bool settings;
        private float volume;
        private bool muted;
        private string settingsError;
        private BaseUnity.Features.ConsentService consent;
        private readonly Color ink = new Color(0.055f, 0.08f, 0.14f);
        private readonly Color accent = new Color(0.34f, 0.91f, 0.75f);

        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Game Flow Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.transform;
            var component = root.GetComponent<Canvas>();
            component.renderMode = RenderMode.ScreenSpaceOverlay;
            component.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(900, 1600);
            scaler.matchWidthOrHeight = 0.5f;
            var safeContent = Rect("Safe Area Content", root.transform, new Vector2(900, 1600), Vector2.zero);
            var safe = root.AddComponent<GameFlowSafeArea>(); safe.canvas = component; safe.content = safeContent;
            canvas = safeContent;
            var input = new GameObject("Game Flow EventSystem", typeof(EventSystem));
            input.transform.SetParent(transform, false);
            // Keep the sample source independent of a required Input System assembly reference.
            var module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
#if ENABLE_INPUT_SYSTEM
            if (module != null) input.AddComponent(module);
            else input.AddComponent<StandaloneInputModule>();
#else
            input.AddComponent<StandaloneInputModule>();
#endif
            consent = flow.Services.Get<BaseUnity.Features.ConsentService>();
            consent.Changed += ConsentChanged;
            flow.Changed += Rebuild;
            Rebuild();
            LevelSelectionView.Create(flow, canvas, font);
        }

        private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
        private Text Label(string text, float y, int size = 26, Color? color = null)
        {
            var label = Rect("Label", page.transform, new Vector2(800, 70), new Vector2(0, y)).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.text = text;
            label.alignment = TextAnchor.MiddleCenter; label.color = color ?? Color.white;
            label.raycastTarget = false;
            return label;
        }
        private Button Button(string name, float y, Action click, bool primary = false)
        {
            var rect = Rect(name, page.transform, new Vector2(340, 52), new Vector2(0, y));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = primary ? accent : new Color(0.18f, 0.23f, 0.33f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            var text = Rect("Text", rect, rect.sizeDelta, Vector2.zero).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = 21; text.text = name;
            text.color = primary ? ink : Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            if (EventSystem.current && !EventSystem.current.currentSelectedGameObject) EventSystem.current.SetSelectedGameObject(button.gameObject);
            return button;
        }

        private void Rebuild()
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            if (page) { page.SetActive(false); Destroy(page); }
            loadingLabel = null;
            timerLabel = null;
            page = Rect("Page", canvas, Vector2.zero, Vector2.zero).gameObject;
            var full = page.GetComponent<RectTransform>();
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            page.AddComponent<Image>().color = ink;
            Label("BASE UNITY  /  GAME FLOW", 295, 18, accent);
            Label("A starting point for your next game", -310, 17, new Color(0.58f, 0.65f, 0.76f));
            if (settings && flow.Phase == FlowPhase.Home) { DrawSettings(); return; }
            switch (flow.Phase)
            {
                case FlowPhase.Home:
                    Label(flow.Config.gameTitle, 180, 56);
                    Label($"BEST {flow.Progress.bestScore}    /    SESSIONS {flow.Progress.sessions}", 95, 22);
                    Label("LEVEL " + flow.HomeLevel, 40, 24, accent);
                    Button("Play", -20, flow.Play, true);
                    Button("Settings", -70, OpenSettings);
                    Button("Choose Level", -140, flow.ShowLevels);
                    break;
                case FlowPhase.Playing:
                    if (flow.Levels && flow.Levels.Instance)
                    {
                        page.GetComponent<Image>().color = Color.clear;
                        page.GetComponent<Image>().raycastTarget = false;
                        foreach (var text in page.GetComponentsInChildren<Text>()) text.gameObject.SetActive(false);
                        Label("LEVEL " + flow.CurrentLevel + "  /  SCORE " + flow.Score, 700, 36);
                        timerLabel = Label("", 610, 28);
                        Button("Pause", 520, flow.Pause);
                        break;
                    }
                    Label("LEVEL " + flow.CurrentLevel, 180, 48);
                    Label($"SCORE  {flow.Score}", 100, 32, accent);
                    timerLabel = Label("TARGET " + (flow.CurrentDefinition?.targetScore ?? 0), 40, 22);
                    Button("Collect +10", -30, () => flow.AddScore(10), true);
                    Button("Finish session", -100, flow.CompleteSession);
                    Button("Pause", -170, flow.Pause);
                    Button("Lose level", -240, flow.LoseLevel);
                    break;
                case FlowPhase.Paused:
                    Label("PAUSED", 170, 48);
                    Label("Take your time", 95, 24);
                    Button("Resume", 0, flow.Resume, true);
                    Button("Home", -70, flow.GoHome);
                    break;
                case FlowPhase.Result:
                    Label(flow.Outcome == LevelOutcome.Win ? (flow.CanNextLevel ? "LEVEL COMPLETE" : "ALL LEVELS COMPLETE") : "LEVEL FAILED", 180, 40);
                    Label($"SCORE {flow.Score}    /    BEST {flow.Progress.bestScore}", 95, 30, accent);
                    if (flow.CanNextLevel) Button("Next Level", 0, flow.NextLevel, true);
                    Button("Retry", -70, flow.RetryLevel, !flow.CanNextLevel);
                    Button("Home", -140, flow.GoHome);
                    if (!string.IsNullOrEmpty(flow.SaveWarning)) Label(flow.SaveWarning, -180, 18);
                    break;
                case FlowPhase.Error:
                    Label("Unable to continue", 130, 40);
                    Label(flow.ErrorMessage, 30, 20);
                    Button("Return Home", -90, flow.GoHome, true);
                    break;
                case FlowPhase.LevelSelect:
                    page.SetActive(false);
                    break;
                default:
                    Label("Getting ready", 80, 48);
                    loadingLabel = Label("Loading...", -10, 26, accent);
                    break;
            }
        }

        private void OpenSettings()
        {
            var audio = flow.Services.Get<AudioService>();
            volume = audio.MasterVolume; muted = audio.Muted;
            settings = true; settingsError = null; Rebuild();
        }
        private void DrawSettings()
        {
            Label("SETTINGS", 200, 44);
            Label($"Master volume  {Mathf.RoundToInt(volume * 100)}%", 125, 26, accent);
            Button("Volume +", 55, () => { volume = Mathf.Clamp01(volume + 0.1f); Rebuild(); });
            Button("Volume -", -10, () => { volume = Mathf.Clamp01(volume - 0.1f); Rebuild(); });
            Button(muted ? "Sound: Off" : "Sound: On", -75, () => { muted = !muted; Rebuild(); });
            Button("Save & Back", -140, () =>
            {
                try
                {
                    var audio = flow.Services.Get<AudioService>();
                    audio.SetVolume(volume, audio.MusicVolume, audio.EffectsVolume, muted);
                    audio.SaveSettings(); settings = false;
                }
                catch (Exception error) { settingsError = error.Message; }
                Rebuild();
            }, true);
            Button("Cancel", -205, () => { settings = false; Rebuild(); });
            var consent = flow.Services.Get<BaseUnity.Features.ConsentService>();
            if (consent.PrivacyOptionsRequired || (consent.Enabled && consent.Status == BaseUnity.Features.FeatureStatus.Failed))
                Button(consent.PrivacyOptionsRequired ? "Privacy options" : "Retry privacy", -280, () => { _ = OpenPrivacy(); }).interactable = !consent.IsBusy;
            if (!string.IsNullOrEmpty(settingsError)) Label(settingsError, -365, 16);
        }
        private void ConsentChanged() { if (settings && flow && flow.IsReady && flow.Services.TryGet<BaseUnity.Features.ConsentService>(out _)) Rebuild(); }
        private async System.Threading.Tasks.Task OpenPrivacy()
        {
            var consent = flow.Services.Get<BaseUnity.Features.ConsentService>();
            bool success = consent.PrivacyOptionsRequired ? await consent.ShowPrivacyOptionsAsync() : await consent.RetryAsync();
            if (!this) return;
            if (!flow || !flow.IsReady) return;
            settingsError = success ? null : consent.LastError ?? "Privacy options are unavailable.";
            Rebuild();
        }
        private void Update()
        {
            if (loadingLabel) loadingLabel.text = $"Loading  {Mathf.RoundToInt(flow.LoadingProgress * 100)}%";
            if (timerLabel) timerLabel.text = "TARGET " + (flow.CurrentDefinition?.targetScore ?? 0) +
                (flow.HasTimeLimit ? "  /  TIME " + Mathf.CeilToInt(flow.RemainingTime) + "s" : "");
        }
        private void OnDestroy() { if (flow) { flow.Changed -= Rebuild; if (consent != null) consent.Changed -= ConsentChanged; } }
    }
}
