using UnityEngine;
using UnityEngine.EventSystems;
namespace BaseUnity.GameFlow
{
    public sealed class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform area, handle;
        public string action = "Move";
        [Range(0, .9f)] public float deadZone = .1f;
        private MobileControlLayout layout;
        private void OnEnable() { if(Application.isPlaying) { layout=GetComponent<MobileControlLayout>(); if(!layout) layout=gameObject.AddComponent<MobileControlLayout>(); } }
        private int pointer = int.MinValue;
        public Vector2 Value { get; private set; }
        private bool Allowed => GameplayInputGate.Open && GameplayInputGate.Services.Gameplay.ControlsEnabled;
        public void OnPointerDown(PointerEventData data) { if (!Allowed || pointer != int.MinValue) return; pointer = data.pointerId; OnDrag(data); }
        public void OnDrag(PointerEventData data)
        {
            if (!Allowed || data.pointerId != pointer || !area) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, data.position, data.pressEventCamera, out var local)) return;
            var radius = Mathf.Max(1, Mathf.Min(area.rect.width, area.rect.height) * .5f);
            var raw = Vector2.ClampMagnitude((local - area.rect.center) / radius, 1);
            float zone = layout ? layout.Preferences.deadZone : deadZone;
            Value = raw.magnitude < zone ? Vector2.zero : Vector2.ClampMagnitude(raw * (layout ? layout.Preferences.sensitivity : 1),1);
            if (handle) handle.anchoredPosition = raw * radius;
            GameplayInputGate.Manual?.SetVector2(action, Value);
        }
        public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) ResetInput(); }
        public void ResetInput() { pointer = int.MinValue; Value = Vector2.zero; if (handle) handle.anchoredPosition = Vector2.zero; GameplayInputGate.Manual?.SetVector2(action, Vector2.zero); }
        private void Update() { if (!Allowed && pointer != int.MinValue) ResetInput(); }
        private void OnDisable() => ResetInput();
    }
}
