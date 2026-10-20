using System;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public enum UIThemeRole { PrimaryButton, SecondaryButton, Tab, Background, Panel, PanelArt, Artwork, Card, Title, Body, Muted, Currency, Success, Error, BarTrack, BarFill }
    [Serializable] public sealed class UIThemeStyle
    {
        public UIThemeRole role;
        public Color graphic = Color.white, text = Color.white;
        public bool replaceSprite;
        public Sprite sprite;
        public ColorBlock buttonColors = ColorBlock.defaultColorBlock;
        [Range(.5f, 2)] public float textScale = 1;
    }
    [CreateAssetMenu(menuName = "BaseUnity/UI/Theme")]
    public sealed class UIThemeConfig : ScriptableObject
    {
        public Font font;
        [HideInInspector] public Sprite barMaskSprite;
        public UIThemeStyle[] styles = Array.Empty<UIThemeStyle>();
        public UIThemeStyle Find(UIThemeRole role)
        { foreach (var style in styles ?? Array.Empty<UIThemeStyle>()) if (style != null && style.role == role) return style; return null; }
    }
}
