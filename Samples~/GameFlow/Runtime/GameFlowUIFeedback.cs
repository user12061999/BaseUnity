using System;
using System.Collections.Generic;
using BaseUnity.Core;
using BaseUnity.Features;
using BaseUnity.Pooling;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>All effects are decorative, unscaled, bounded and never block input or game transitions.</summary>
    public sealed class GameFlowUIFeedback : MonoBehaviour
    {
        private sealed class Particle
        {
            public GameObject item; public RectTransform rect; public Image image;
            public Vector2 start, control, end; public float age, seconds, size; public Color color; public bool fly;
        }
        private GameFlowBootstrap flow;
        private UIFeedbackService service;
        private RectTransform canvasRect;
        private GameObject template;
        private PrefabPool pool;
        private Sprite dot;
        private Texture2D texture;
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Selectable> controls = new List<Selectable>();
        private Dictionary<string, RectTransform> previous = new Dictionary<string, RectTransform>();
        private Dictionary<string, RectTransform> current = new Dictionary<string, RectTransform>();
        private float scanAt;
        private FlowPhase phase;
        public int ActiveParticles => particles.Count;
        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner; service = owner.Services.Get<FeatureServices>().UI;
            if (!service.Enabled) { enabled = false; return; }
            if (service.MotionEnabled)
            {
            var root = new GameObject("UI Feedback Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = .5f;
            canvasRect = (RectTransform)root.transform;
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); texture.name = "Feedback disc";
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Clamp01((15.5f - Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f))) / 1.5f));
            texture.SetPixels(pixels); texture.Apply(false, true); dot = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f));
            template = new GameObject("UI feedback particle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); template.transform.SetParent(canvasRect, false);
            var image = template.GetComponent<Image>(); image.sprite = dot; image.raycastTarget = false; template.SetActive(false);
            pool = new PrefabPool(template, canvasRect, 12, 32);
            }
            service.Played += Played; flow.Changed += FlowChanged; ScanButtons();
        }
        private void ScanButtons()
        {
            buttons.Clear(); flow.GetComponentsInChildren(true, buttons);
            foreach (var button in buttons)
            {
                var feedback = button.GetComponent<UIButtonFeedback>(); if (!feedback) feedback = button.gameObject.AddComponent<UIButtonFeedback>();
                feedback.Bind();
            }
            controls.Clear(); flow.GetComponentsInChildren(true, controls);
            foreach (var control in controls)
            {
                if (!(control is Slider) && !(control is Toggle) && !(control is Dropdown)) continue;
                var feedback = control.GetComponent<UIValueFeedback>(); if (!feedback) feedback = control.gameObject.AddComponent<UIValueFeedback>(); feedback.Bind();
            }
        }
        private void FlowChanged()
        {
            if (flow.Phase == phase) return; phase = flow.Phase;
            if (phase == FlowPhase.Result) service.Play(flow.Outcome == LevelOutcome.Win ? UICue.Win : UICue.Lose);
        }
        private void LateUpdate()
        {
            if (!flow || !flow.IsReady) return;
            if (UnityEngine.Time.unscaledTime >= scanAt) { scanAt = UnityEngine.Time.unscaledTime + .15f; ScanButtons(); }
            var visible = current; visible.Clear();
            var hub = GetComponent<GameFlowHub>();
            if (hub && hub.View && hub.View.gameObject.activeInHierarchy)
            {
                Add(visible, "store", hub.View.storePage.gameObject); Add(visible, "quests", hub.View.questPage.gameObject);
                Add(visible, "confirm", hub.View.confirmation);
            }
            var art = GetComponentInChildren<GameFlowView>();
            if (art) { Add(visible, "settings", art.settings); Add(visible, "pause", art.pause); }
            else
            {
                var fallback = GetComponent<GameFlowUI>(); if (fallback && fallback.SettingsOpen) visible["settings"] = null;
                if (flow.Phase == FlowPhase.Paused) visible["pause"] = null;
            }
            var modules = GetComponent<GameFlowMobileModules>();
            if (modules && modules.IsMenuOpen) visible["more"] = null;
            if (modules && modules.IsPopupVisible) visible["popup:" + flow.Services.Get<FeatureServices>().Popups.Active?.Id] = null;
            var offline = GetComponent<GameFlowOfflineUI>(); if (offline && offline.LegacyIsOpen) visible["offline"] = null;
            if (flow.Phase == FlowPhase.LevelSelect) visible["levels"] = null;
            bool opened = false, closed = false;
            foreach (var entry in visible) if (!previous.ContainsKey(entry.Key))
            {
                opened = true;
                if (service.MotionEnabled && entry.Value)
                { var motion = entry.Value.GetComponent<UIFeedbackMotion>(); if (!motion) motion = entry.Value.gameObject.AddComponent<UIFeedbackMotion>(); motion.Enter(service.Profile.panelSeconds); }
            }
            foreach (var entry in previous) if (!visible.ContainsKey(entry.Key)) closed = true;
            if (opened) service.Play(UICue.Open); else if (closed && flow.Phase != FlowPhase.Loading) service.Play(UICue.Close);
            current = previous; previous = visible;
            float delta = UnityEngine.Time.unscaledDeltaTime;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i]; particle.age += delta; float t = Mathf.Clamp01(particle.age / particle.seconds);
                if (!service.MotionEnabled || t >= 1)
                { pool.Return(particle.item); particles.RemoveAt(i); continue; }
                particle.rect.anchoredPosition = particle.fly ? (1 - t) * (1 - t) * particle.start + 2 * (1 - t) * t * particle.control + t * t * particle.end : particle.start;
                particle.rect.sizeDelta = Vector2.one * particle.size * (particle.fly ? 1 - .3f * t : 1 + 2 * t);
                particle.image.color = new Color(particle.color.r, particle.color.g, particle.color.b, particle.color.a * (1 - t));
            }
        }
        private static void Add(Dictionary<string, RectTransform> entries, string key, GameObject panel)
        { if (panel && panel.activeInHierarchy) entries[key] = panel.transform as RectTransform; }
        private Vector2 Local(Vector2 screen) { RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local); return local; }
        private void Played(UICue cue, Vector2? screen)
        {
            if (!service.MotionEnabled) return;
            if (cue == UICue.Click && screen.HasValue) Spawn(Local(screen.Value), Vector2.zero, false, 35, new Color(.65f, .9f, 1, .3f), .25f);
            if (cue == UICue.Close) Spawn(Vector2.zero, Vector2.zero, false, 60, new Color(.65f, .9f, 1, .15f), .18f);
            if (cue != UICue.Reward && cue != UICue.Success && cue != UICue.Error && cue != UICue.Purchase && cue != UICue.Win && cue != UICue.Lose) return;
            bool reward = cue == UICue.Reward;
            Vector2 origin = screen.HasValue ? Local(screen.Value) : Vector2.zero;
            Vector2 target = new Vector2(0, canvasRect.rect.height * .42f);
            var hub = GetComponent<GameFlowHub>();
            if (hub && hub.View && hub.View.wallet.gameObject.activeInHierarchy)
                target = Local(RectTransformUtility.WorldToScreenPoint(null, hub.View.wallet.rectTransform.position));
            int count = reward || cue == UICue.Win ? 8 : 4;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count; var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 80;
                var color = cue == UICue.Error || cue == UICue.Lose ? new Color(1, .3f, .25f, .8f) : new Color(1, .8f, .2f, 1);
                Spawn(origin + offset * .25f, reward ? target : origin + offset * 2, true, reward ? 30 : 14, color, reward ? Mathf.Clamp(service.Profile.rewardSeconds, .2f, 2) : .45f);
            }
        }
        private void Spawn(Vector2 start, Vector2 end, bool fly, float size, Color color, float seconds)
        {
            if (particles.Count >= 32) return;
            var item = pool.Rent(Vector3.zero, Quaternion.identity, canvasRect); var rect = (RectTransform)item.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = start; rect.sizeDelta = Vector2.one * size;
            var image = item.GetComponent<Image>(); image.sprite = fly && service.Profile.rewardSprite ? service.Profile.rewardSprite : dot; image.color = color;
            particles.Add(new Particle { item = item, rect = rect, image = image, start = start, end = end, control = start + new Vector2((end.x - start.x) * .4f + 60, 180), seconds = seconds, size = size, color = color, fly = fly });
        }
        private void OnDestroy()
        {
            if (service != null) service.Played -= Played; if (flow) flow.Changed -= FlowChanged;
            pool?.Dispose(); particles.Clear(); if (template) Destroy(template); if (dot) Destroy(dot); if (texture) Destroy(texture);
        }
    }
}
