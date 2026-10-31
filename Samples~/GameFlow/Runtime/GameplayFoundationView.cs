using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class GameplayFoundationView : MonoBehaviour
    {
        public GameObject home, modal;
        public Text status, title, message, badge, pageNumber;
        public Button open, close, gifts, achievements, settings, previous, next;
        public Button[] rows;
        public Text[] rowLabels;
        public GameplayEffects effects;
    }
}
