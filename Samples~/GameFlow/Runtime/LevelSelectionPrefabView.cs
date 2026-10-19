using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class LevelSelectionPrefabView : MonoBehaviour
    {
        public Text completed, pageNumber;
        public Transform cells;
        public Button cellPrefab, previous, next, back;
    }
}
