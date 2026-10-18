using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class OfflineMenuView : MonoBehaviour
    {
        [Serializable] public sealed class Tab
        {
            public string key;
            public Button button;
            public GameObject selected;
            public OfflinePanelView panel;
        }
        public Text balance, message, languageLabel;
        public Button language, close;
        public Tab[] tabs;
        public GameObject loading;
        public RectTransform animatedContent;
        [Min(0)] public float animationSeconds = .16f;
        public Color successColor = new Color(.4f, 1, .65f), errorColor = new Color(1, .48f, .4f);
        private CanvasGroup group;
        private bool closing;
        private void OnEnable()
        { group = GetComponent<CanvasGroup>(); closing = false; StartCoroutine(Animate(true, null)); }
        public void Close(Action completed)
        {
            if (closing) return;
            closing = true; StopAllCoroutines(); StartCoroutine(Animate(false, completed));
        }
        private IEnumerator Animate(bool opening, Action completed)
        {
            group.interactable = false; group.blocksRaycasts = true;
            float elapsed = 0;
            do
            {
                float t = animationSeconds <= 0 ? 1 : Mathf.Clamp01(elapsed / animationSeconds);
                float value = opening ? t : 1 - t;
                group.alpha = value;
                if (animatedContent) animatedContent.localScale = Vector3.one * Mathf.Lerp(.94f, 1, 1 - Mathf.Pow(1 - value, 3));
                if (t >= 1) break;
                yield return null; elapsed += UnityEngine.Time.unscaledDeltaTime;
            } while (true);
            group.interactable = opening;
            completed?.Invoke();
        }
    }
}
