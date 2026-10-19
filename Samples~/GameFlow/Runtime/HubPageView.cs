using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class HubPageView : MonoBehaviour
    {
        public Text heading, message, countdown, pageNumber;
        public Button primary, secondary, previous, next, close, restore, retryPending;
        public Transform rows;
        public GameObject empty;
    }
}
