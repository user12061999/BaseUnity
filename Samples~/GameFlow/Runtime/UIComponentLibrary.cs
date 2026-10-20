using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    [CreateAssetMenu(menuName = "BaseUnity/UI/Component Library")]
    public sealed class UIComponentLibrary : ScriptableObject
    {
        public Button primaryButton, secondaryButton, tabButton;
        public CurrencyBarView currencyBar;
        public HubCardView itemCard, questCard;
    }
}
