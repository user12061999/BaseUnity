using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
namespace BaseUnity.GameFlow
{
    public sealed class GameplayDropZone : MonoBehaviour, IDropHandler
    {
        public UnityEvent<GameObject> accepted = new UnityEvent<GameObject>();
        public void OnDrop(PointerEventData data)
        {
            if (!GameplayInputGate.Open || !GameplayInputGate.Services.Gameplay.InteractionsEnabled || !data.pointerDrag) return;
            var source = data.pointerDrag.GetComponent<GameplayInteractable>();
            if (source && source.TryAcceptDrop()) accepted.Invoke(source.gameObject);
        }
    }
}
