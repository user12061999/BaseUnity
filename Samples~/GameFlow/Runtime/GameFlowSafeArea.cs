using UnityEngine;

namespace BaseUnity.GameFlow
{
    /// <summary>Fits the authored layout within the current safe area without clipping.</summary>
    [ExecuteAlways]
    public sealed class GameFlowSafeArea : MonoBehaviour
    {
        public RectTransform content;
        public Canvas canvas;
        public Vector2 referenceResolution = new Vector2(900, 1600);
        private void LateUpdate()
        {
            if (!content || !canvas || Screen.width == 0 || Screen.height == 0) return;
            var root = (RectTransform)canvas.transform;
            var safe = Screen.safeArea;
            var size = new Vector2(root.rect.width * safe.width / Screen.width, root.rect.height * safe.height / Screen.height);
            float scale = Mathf.Min(size.x / Mathf.Max(1, referenceResolution.x), size.y / Mathf.Max(1, referenceResolution.y));
            content.localScale = Vector3.one * scale;
            content.anchoredPosition = new Vector2(
                (safe.center.x / Screen.width - 0.5f) * root.rect.width,
                (safe.center.y / Screen.height - 0.5f) * root.rect.height);
        }
    }
}
