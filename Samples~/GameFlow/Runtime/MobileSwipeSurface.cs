using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
namespace BaseUnity.GameFlow
{
    public sealed class MobileSwipeSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float minimumDistance = 60, maximumSeconds = 1;
        public UnityEvent<Vector2> swiped = new UnityEvent<Vector2>();
        public UnityEvent tapped = new UnityEvent();
        private int pointer = int.MinValue;
        private Vector2 start;
        private float began;
        public void OnPointerDown(PointerEventData data) { if (!GameplayInputGate.Open || !GameplayInputGate.Services.Gameplay.ControlsEnabled || pointer != int.MinValue) return; pointer = data.pointerId; start = data.position; began = UnityEngine.Time.unscaledTime; }
        public void OnPointerUp(PointerEventData data)
        {
            if (pointer != data.pointerId) return; pointer = int.MinValue;
            if (!GameplayInputGate.Open || !GameplayInputGate.Services.Gameplay.ControlsEnabled || UnityEngine.Time.unscaledTime - began > maximumSeconds) return;
            var delta = data.position - start; if (delta.magnitude >= Mathf.Max(1, minimumDistance)) swiped.Invoke(delta.normalized); else tapped.Invoke();
        }
        private void Update() { if (!GameplayInputGate.Open) pointer = int.MinValue; }
        private void OnDisable() { pointer = int.MinValue; }
    }
}
