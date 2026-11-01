using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace BaseUnity.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Range(.8f,1f)] public float pressedScale=.94f;
        public bool reducedMotion;
        public UnityEvent clicked=new UnityEvent();
        private Vector3 rest;
        private Button button;
        private void Awake() { rest=transform.localScale; button=GetComponent<Button>(); }
        private void OnEnable() { if(!button)button=GetComponent<Button>(); button.onClick.AddListener(Click); }
        private void OnDisable() { if(button)button.onClick.RemoveListener(Click); transform.localScale=rest; }
        private void Click() { if(button.IsInteractable())clicked.Invoke(); }
        public void OnPointerDown(PointerEventData data) { if(!reducedMotion&&button.IsInteractable())transform.localScale=rest*pressedScale; }
        public void OnPointerUp(PointerEventData data) { transform.localScale=rest; }
        public void OnPointerExit(PointerEventData data) { transform.localScale=rest; }
    }
}
