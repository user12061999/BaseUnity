using UnityEngine;
namespace BaseUnity.GameFlow
{
    /// <summary>Scale only. Does not change layout anchors, alpha, raycasts or interaction policy.</summary>
    public sealed class UIFeedbackMotion : MonoBehaviour
    {
        private Vector3 rest;
        private float elapsed, seconds, start;
        private bool running;
        public void Enter(float duration)
        {
            if (running) transform.localScale = rest;
            rest = transform.localScale; elapsed = 0; seconds = Mathf.Clamp(duration, .08f, .6f); start = .94f; running = true;
            transform.localScale = rest * start;
        }
        private void Update()
        {
            if (!running) return;
            elapsed += UnityEngine.Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / seconds);
            transform.localScale = rest * Mathf.Lerp(start, 1, 1 - Mathf.Pow(1 - t, 3));
            if (t >= 1) { transform.localScale = rest; running = false; }
        }
        private void OnDisable() { if (running) transform.localScale = rest; running = false; }
    }
}
