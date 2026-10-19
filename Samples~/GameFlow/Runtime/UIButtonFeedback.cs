using BaseUnity.Core;
using BaseUnity.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        private Button button;
        private Button.ButtonClickedEvent hooked;
        private UIFeedbackService service;
        private Vector3 rest;
        private bool pressed, gesture, ownsScale;
        private int releaseFrame = -1;
        private float pulseUntil;
        private void OnEnable() { rest = transform.localScale; Bind(); }
        public void Bind()
        {
            button = GetComponent<Button>();
            var owner = GameBootstrap.Instance;
            if (owner && owner.IsReady) owner.Services.TryGet(out service);
            if (button && !ReferenceEquals(hooked, button.onClick))
            { hooked?.RemoveListener(Clicked); hooked = button.onClick; hooked.AddListener(Clicked); }
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !button || !button.IsInteractable()) return;
            pressed = gesture = true; releaseFrame = -1;
            if (service != null && service.MotionEnabled) { if (!ownsScale) rest = transform.localScale; ownsScale = true; }
            service?.Play(UICue.Click, eventData.position);
        }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; releaseFrame = UnityEngine.Time.frameCount; }
        public void OnPointerExit(PointerEventData eventData) { pressed = false; }
        public void OnPointerEnter(PointerEventData eventData)
        { if (!Application.isMobilePlatform && button && button.IsInteractable()) service?.Play(UICue.Hover, eventData.position); }
        private void Clicked()
        {
            if (!button || !button.interactable || service == null) return;
            bool alreadyPlayed = gesture && (pressed || releaseFrame == UnityEngine.Time.frameCount);
            gesture = false;
            if (!alreadyPlayed)
            {
                var rect = transform as RectTransform; var canvas = GetComponentInParent<Canvas>();
                Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, rect ? rect.TransformPoint(rect.rect.center) : transform.position);
                service.Play(UICue.Click, point); pulseUntil = UnityEngine.Time.unscaledTime + .07f;
                if (service.MotionEnabled) { if (!ownsScale) rest = transform.localScale; ownsScale = true; }
            }
        }
        private void Update()
        {
            if (service == null || !service.MotionEnabled) { Restore(); return; }
            if (!ownsScale) return;
            float multiplier = pressed || UnityEngine.Time.unscaledTime < pulseUntil ? Mathf.Clamp(service.Profile.pressedScale, .8f, 1) : 1;
            float duration = Mathf.Clamp(service.Profile.buttonSeconds, .04f, .4f);
            transform.localScale = Vector3.Lerp(transform.localScale, rest * multiplier, 1 - Mathf.Exp(-UnityEngine.Time.unscaledDeltaTime * 5 / duration));
            if (multiplier == 1 && (transform.localScale - rest).sqrMagnitude < .000001f) Restore();
        }
        private void Restore() { if (ownsScale) transform.localScale = rest; ownsScale = false; }
        private void OnDisable() { Restore(); pressed = false; pulseUntil = 0; }
        private void OnDestroy() { hooked?.RemoveListener(Clicked); }
    }
}
