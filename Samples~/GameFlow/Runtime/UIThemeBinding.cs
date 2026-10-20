using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    /// <summary>Semantic theme binding. Never changes content, navigation, layout, price or enabled state.</summary>
    [DisallowMultipleComponent]
    public sealed class UIThemeBinding : MonoBehaviour
    {
        public UIThemeConfig theme;
        public UIThemeRole role = UIThemeRole.Body;
        public Image graphic;
        public Text label;
        public Selectable control;
        public bool useFont = true, useTextColor = true, useGraphicColor = true, useSprite = true, useButtonColors = true;
        [SerializeField] private int authoredSize, authoredMinimum, authoredMaximum;
        [ContextMenu("Capture Authored Typography")]
        public void CaptureTypography()
        { if (!label) return; authoredSize = label.fontSize; authoredMinimum = label.resizeTextMinSize; authoredMaximum = label.resizeTextMaxSize; }
        public void Apply(UIThemeConfig value)
        {
            if (!value) return; var style = value.Find(role); if (style == null) return;
            if (graphic)
            { if (useGraphicColor) graphic.color = style.graphic; if (useSprite && style.replaceSprite && (style.sprite || graphic.type != Image.Type.Filled)) graphic.sprite = style.sprite; }
            if (control && useButtonColors) control.colors = style.buttonColors;
            if (!label) return;
            if (useFont && value.font) label.font = value.font;
            if (useTextColor) label.color = style.text;
            if (authoredSize <= 0) CaptureTypography();
            float scale = Mathf.Clamp(style.textScale, .5f, 2);
            label.fontSize = Mathf.Max(1, Mathf.RoundToInt(authoredSize * scale));
            label.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(authoredMinimum * scale));
            label.resizeTextMaxSize = Mathf.Max(label.resizeTextMinSize, Mathf.RoundToInt(authoredMaximum * scale));
        }
        private void OnEnable()
        { var owner = GetComponentInParent<GameFlowThemeController>(); Apply(owner && owner.Theme ? owner.Theme : theme); }
    }
}
