using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class GameplayHitFeedback : MonoBehaviour
    {
        public Graphic graphic;
        public RectTransform target;
        public Color hitColor = Color.white;
        public float seconds = .18f, punch = .12f;
        private Color originalColor;
        private Vector3 originalScale;
        private float elapsed;
        private bool active, reduce;
        public void Play()
        {
            var services = GameplayInputGate.Services;
            if (services == null || !services.Gameplay.EffectsEnabled || (services.Preferences.Enabled && !services.Preferences.Current.gameplayEffects)) return;
            if (!active) { originalColor = graphic ? graphic.color : Color.white; originalScale = target ? target.localScale : Vector3.one; }
            reduce = services.Preferences.Enabled && services.Preferences.Current.reducedMotion; active = true; elapsed = 0;
        }
        private void Update()
        {
            if (!active) return; elapsed += UnityEngine.Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / Mathf.Max(.02f, seconds));
            if (graphic) graphic.color = Color.Lerp(hitColor, originalColor, t);
            if (target && !reduce) target.localScale = originalScale * (1 + Mathf.Sin(t * Mathf.PI) * Mathf.Clamp(punch,0,.5f));
            if (t >= 1) Restore();
        }
        private void Restore() { if (!active) return; if (graphic) graphic.color = originalColor; if (target) target.localScale = originalScale; active = false; }
        private void OnDisable() => Restore();
    }
}
