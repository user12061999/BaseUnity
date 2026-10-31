using UnityEngine;
using UnityEngine.EventSystems;
namespace BaseUnity.MobileInput
{
    public sealed class TouchButton : MonoBehaviour,IPointerDownHandler,IPointerUpHandler
    {
        public TouchInputContext context;
        public string action="Fire";
        private int pointer=int.MinValue;
        public void OnPointerDown(PointerEventData e) { if(!context || !context.IsOpen || pointer!=int.MinValue) return; pointer=e.pointerId; context.Source.SetButton(action,true); }
        public void OnPointerUp(PointerEventData e) { if(pointer==e.pointerId) Release(); }
        public void Release() { pointer=int.MinValue; if(context) context.Source?.SetButton(action,false); }
        private void Update() { if(pointer!=int.MinValue && (!context || !context.IsOpen)) Release(); }
        private void OnDisable() => Release();
    }
}
