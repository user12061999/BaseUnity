using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class InGameHUDView : MonoBehaviour
    {
        public GameObject gameplay, boosterBar, modal;
        public Text level, target, timer, message, modalTitle, modalMessage, acceptLabel;
        public Image progress;
        public Transform slots;
        public BoosterSlotView slotPrefab;
        public Button accept, cancel;
    }
}
