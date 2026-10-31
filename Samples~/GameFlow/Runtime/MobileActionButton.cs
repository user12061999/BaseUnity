using UnityEngine;
using UnityEngine.EventSystems;
namespace BaseUnity.GameFlow
{
    public sealed class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public string action = "Fire";
        private int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData data)
        { if (!GameplayInputGate.Open || !GameplayInputGate.Services.Gameplay.ControlsEnabled || pointer != int.MinValue) return; pointer = data.pointerId; GameplayInputGate.Manual?.SetButton(action, true); }
        public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) Release(); }
        public void Release() { pointer = int.MinValue; GameplayInputGate.Manual?.SetButton(action, false); }
        private void Update() { if (pointer != int.MinValue && (!GameplayInputGate.Open || !GameplayInputGate.Services.Gameplay.ControlsEnabled)) Release(); }
        private void OnDisable() => Release();
    }
}
