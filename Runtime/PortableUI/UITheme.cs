using UnityEngine;
namespace BaseUnity.UI
{
    [CreateAssetMenu(menuName="BaseUnity/UI/Theme")]
    public sealed class UITheme : ScriptableObject
    {
        public Color background=new Color(.06f,.09f,.17f);
        public Color primary=new Color(.18f,.55f,.95f);
        public Color text=Color.white;
        public Font font;
        [Range(12,60)] public int fontSize=28;
    }
}
