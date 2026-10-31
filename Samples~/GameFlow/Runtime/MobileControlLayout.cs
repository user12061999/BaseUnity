using BaseUnity.Features;
using UnityEngine;
namespace BaseUnity.GameFlow
{
    public sealed class MobileControlLayout : MonoBehaviour
    {
        public bool actionButton;
        private PlayerPreferencesService service;
        private RectTransform rect;
        private Vector2 origin;
        private Vector3 scale;
        private bool captured;
        public PlayerPreferencesService.Data Preferences { get; private set; } = new PlayerPreferencesService.Data();
        private void OnEnable() { Bind(); }
        private void Update() { if(service == null) Bind(); }
        private void Bind()
        {
            var modules=GameplayInputGate.Services; if(modules==null) return;
            Bind(modules.PlayerPreferences);
        }
        public void Bind(PlayerPreferencesService value)
        {
            if(service!=null) service.Changed-=Apply;
            service=value; if(service==null) return; rect=GetComponent<RectTransform>();
            if(!captured) { origin=rect.anchoredPosition; scale=rect.localScale; captured=true; }
            service.Changed+=Apply; Apply();
        }
        public void SetActionButton() { actionButton=true; if(service!=null) Apply(); }
        private void Apply()
        {
            Preferences=service.Current;
            GetComponent<MobileJoystick>()?.ResetInput(); GetComponent<MobileActionButton>()?.Release();
            rect.localScale=scale * Preferences.controlScale;
            var point=origin + (actionButton ? Preferences.actionOffset : Preferences.joystickOffset);
            if(Preferences.leftHanded) point.x=-point.x;
            if(rect.parent is RectTransform parent) { float x=Mathf.Max(0,(parent.rect.width-rect.rect.width*rect.localScale.x)*.5f), y=Mathf.Max(0,(parent.rect.height-rect.rect.height*rect.localScale.y)*.5f); point.x=Mathf.Clamp(point.x,-x,x); point.y=Mathf.Clamp(point.y,-y,y); }
            rect.anchoredPosition=point;
        }
        private void OnDisable() { if(service!=null) service.Changed-=Apply; service=null; if(captured && rect) { rect.anchoredPosition=origin; rect.localScale=scale; } }
    }
}
