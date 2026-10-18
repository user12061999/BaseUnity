using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    public sealed class OfflinePanelView : MonoBehaviour
    {
        public string key;
        public Text title, empty, pageLabel;
        public Transform rows;
        public OfflineRowView rowPrefab;
        [Min(1)] public int pageSize = 3;
        public Button previous, next;
    }
}
