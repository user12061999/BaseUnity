using UnityEngine;
using UnityEngine.EventSystems;
namespace BaseUnity.MobileInput
{
    public sealed class TouchJoystick : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public TouchInputContext context;
        public RectTransform area,handle;
        public string action="Move";
        [Range(0,.9f)] public float deadZone=.1f;
        [Range(.5f,2)] public float sensitivity=1;
        private int pointer=int.MinValue;
        public Vector2 Value { get; private set; }
        public void OnPointerDown(PointerEventData e) { if(!context || !context.IsOpen || pointer!=int.MinValue) return; pointer=e.pointerId; OnDrag(e); }
        public void OnDrag(PointerEventData e)
        {
            if(!context || !context.IsOpen || !area || pointer!=e.pointerId) return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(area,e.position,e.pressEventCamera,out var p)) return;
            float radius=Mathf.Max(1,Mathf.Min(area.rect.width,area.rect.height)*.5f); var raw=Vector2.ClampMagnitude((p-area.rect.center)/radius,1);
            Value=raw.magnitude<deadZone?Vector2.zero:Vector2.ClampMagnitude(raw*sensitivity,1); if(handle)handle.anchoredPosition=raw*radius; context.Source.SetVector2(action,Value);
        }
        public void OnPointerUp(PointerEventData e) { if(pointer==e.pointerId) Release(); }
        public void Release() { pointer=int.MinValue; Value=Vector2.zero; if(handle) handle.anchoredPosition=Vector2.zero; if(context) context.Source?.SetVector2(action,Vector2.zero); }
        private void Update() { if(pointer!=int.MinValue && (!context || !context.IsOpen)) Release(); }
        private void OnDisable() => Release();
    }
}
