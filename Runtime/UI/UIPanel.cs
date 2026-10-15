using UnityEngine;

namespace BaseUnity.UI
{
    /// <summary>Attach to a screen or popup root. Put UI animation hooks in overrides.</summary>
    public class UIPanel : MonoBehaviour
    {
        public bool IsVisible => gameObject.activeSelf;
        public virtual void Show() => gameObject.SetActive(true);
        public virtual void Hide() => gameObject.SetActive(false);
    }
}
