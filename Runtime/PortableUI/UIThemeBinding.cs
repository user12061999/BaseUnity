using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.UI
{
    [ExecuteAlways]
    public sealed class UIThemeBinding : MonoBehaviour
    {
        public UITheme theme;
        public bool background;
        private void OnEnable() { Apply(); }
        private void OnValidate() { Apply(); }
        public void Apply()
        {
            if(!theme)return;
            var label=GetComponent<Text>();
            if(label) { label.color=theme.text; label.fontSize=theme.fontSize; if(theme.font)label.font=theme.font; }
            else { var image=GetComponent<Image>(); if(image)image.color=background?theme.background:theme.primary; }
        }
    }
}
