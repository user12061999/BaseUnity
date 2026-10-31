using System.Collections.Generic;
using BaseUnity.Pooling;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class GameplayEffects : MonoBehaviour
    {
        private sealed class Effect { public GameObject item; public Text text; public Vector3 start, end; public float age; public Color color; }
        public Text floatingTextPrefab;
        public int maximumActive = 24;
        public float seconds = .65f;
        private PrefabPool pool;
        private readonly List<Effect> active = new List<Effect>();
        public int ActiveCount => active.Count;
        public void Show(string message, Vector2 screenPosition, Color color, Vector2? destination = null)
        {
            var services = GameplayInputGate.Services;
            if (services == null || !services.Gameplay.EffectsEnabled || (services.Preferences.Enabled && !services.Preferences.Current.gameplayEffects) || !floatingTextPrefab || active.Count >= Mathf.Clamp(maximumActive, 1, 64)) return;
            if (pool == null) pool = new PrefabPool(floatingTextPrefab.gameObject, transform, 0, Mathf.Clamp(maximumActive, 1, 64));
            var rect = (RectTransform)transform; var canvas = GetComponentInParent<Canvas>(); var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, camera, out var local)) return;
            var end = local + new Vector2(0, 100);
            if (destination.HasValue) RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, destination.Value, camera, out end);
            bool reduced = services.Preferences.Enabled && services.Preferences.Current.reducedMotion;
            var item = pool.Rent(Vector3.zero, Quaternion.identity, transform); item.transform.localPosition = local;
            var text = item.GetComponent<Text>(); text.text = message; text.color = color;
            active.Add(new Effect { item = item, text = text, start = local, end = reduced ? local : end, color = color });
        }
        private void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--) { var effect = active[i]; effect.age += UnityEngine.Time.unscaledDeltaTime; float t = Mathf.Clamp01(effect.age / Mathf.Max(.05f, seconds)); effect.item.transform.localPosition = Vector3.Lerp(effect.start, effect.end, 1 - (1-t)*(1-t)); var color = effect.color; color.a *= 1-t; effect.text.color = color; if (t >= 1) { pool.Return(effect.item); active.RemoveAt(i); } }
        }
        private void OnDisable() { if (pool != null) foreach (var effect in active) pool.Return(effect.item); active.Clear(); }
        private void OnDestroy() { pool?.Dispose(); }
    }
}
