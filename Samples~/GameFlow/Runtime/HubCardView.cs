using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    public sealed class HubCardView : MonoBehaviour
    {
        public Image icon, progressFill;
        public GameObject progressRoot;
        public Text title, description, reward, progress, actionLabel;
        public Button action;
    }
}
