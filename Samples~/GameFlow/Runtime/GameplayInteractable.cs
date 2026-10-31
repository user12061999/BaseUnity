using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
namespace BaseUnity.GameFlow
{
    public enum GameplayInteractionMode { Tap, Collect, Drag }
    public sealed class GameplayInteractable : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public GameplayInteractionMode mode;
        public bool hideOnCollect = true, clampToParent = true;
        public GameObject selection;
        public UnityEvent interacted = new UnityEvent(), collected = new UnityEvent(), dropped = new UnityEvent();
        public bool Collected { get; private set; }
        public bool Dragging { get; private set; }
        private int pointer = int.MinValue;
        private Vector2 offset;
        private CanvasGroup dragGroup;
        private bool previousRaycasts;
        private bool Allowed => GameplayInputGate.Open && GameplayInputGate.Services.Gameplay.InteractionsEnabled;
        public void OnPointerClick(PointerEventData data)
        {
            if (!Allowed || Collected || mode == GameplayInteractionMode.Drag) return;
            if (mode == GameplayInteractionMode.Collect) { Collected = true; collected.Invoke(); if (hideOnCollect) gameObject.SetActive(false); }
            else { if (selection) selection.SetActive(!selection.activeSelf); interacted.Invoke(); }
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (!Allowed || mode != GameplayInteractionMode.Drag || Dragging || !(transform is RectTransform rect) || !(rect.parent is RectTransform parent)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out var local)) return;
            dragGroup = GetComponent<CanvasGroup>(); if (!dragGroup) dragGroup = gameObject.AddComponent<CanvasGroup>(); previousRaycasts = dragGroup.blocksRaycasts; dragGroup.blocksRaycasts = false;
            pointer = data.pointerId; Dragging = true; offset = (Vector2)rect.localPosition - local;
        }
        public void OnDrag(PointerEventData data)
        {
            if (!Allowed || !Dragging || pointer != data.pointerId) return;
            var rect = (RectTransform)transform; var parent = (RectTransform)rect.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out var local)) return;
            local += offset;
            if (clampToParent) { var bounds = parent.rect; local.x = Mathf.Clamp(local.x, bounds.xMin + rect.rect.width * .5f, bounds.xMax - rect.rect.width * .5f); local.y = Mathf.Clamp(local.y, bounds.yMin + rect.rect.height * .5f, bounds.yMax - rect.rect.height * .5f); }
            rect.localPosition = new Vector3(local.x, local.y, rect.localPosition.z);
        }
        public void OnEndDrag(PointerEventData data) { if (pointer != data.pointerId) return; bool notify = Dragging && Allowed; ResetDrag(); if (notify) dropped.Invoke(); }
        public bool TryAcceptDrop() { if (!Dragging || !Allowed) return false; ResetDrag(); return true; }
        private void ResetDrag() { if (Dragging && dragGroup) dragGroup.blocksRaycasts = previousRaycasts; Dragging = false; pointer = int.MinValue; }
        private void OnEnable() { Collected = false; if (selection) selection.SetActive(false); }
        private void Update() { if (!Allowed) ResetDrag(); }
        private void OnDisable() => ResetDrag();
    }
}
