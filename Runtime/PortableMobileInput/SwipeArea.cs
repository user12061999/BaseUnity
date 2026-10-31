using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
namespace BaseUnity.MobileInput
{
    public sealed class SwipeArea : MonoBehaviour,IPointerDownHandler,IPointerUpHandler
    {
        public TouchInputContext context;
        public float minimumDistance=60,maximumSeconds=1;
        public UnityEvent<Vector2> swiped=new UnityEvent<Vector2>();
        public UnityEvent tapped=new UnityEvent();
        private int pointer=int.MinValue;
        private Vector2 start;
        private float began;
        public void OnPointerDown(PointerEventData e) { if(!context || !context.IsOpen || pointer!=int.MinValue) return; pointer=e.pointerId; start=e.position; began=UnityEngine.Time.unscaledTime; }
        public void OnPointerUp(PointerEventData e) { if(pointer!=e.pointerId) return; pointer=int.MinValue; if(!context || !context.IsOpen || UnityEngine.Time.unscaledTime-began>maximumSeconds) return; var d=e.position-start; if(d.magnitude>=Mathf.Max(1,minimumDistance)) swiped.Invoke(d.normalized); else tapped.Invoke(); }
        private void Update() { if(!context || !context.IsOpen) pointer=int.MinValue; }
        private void OnDisable() { pointer=int.MinValue; }
    }
}
