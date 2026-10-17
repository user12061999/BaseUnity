using System.Collections.Generic;
using BaseUnity.Features;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Translates the sample's legacy Text bindings, including dynamically rebuilt screens.</summary>
    public sealed class GameFlowLocalization : MonoBehaviour
    {
        private sealed class Binding { public Text text; public string source, rendered; }
        private readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();
        private LocalizationService language;
        private float scanAt;
        public void Initialize(LocalizationService service) { language = service; }
        private void LateUpdate()
        {
            if (language == null) return;
            if (UnityEngine.Time.unscaledTime >= scanAt)
            {
                scanAt = UnityEngine.Time.unscaledTime + .2f;
                foreach (var text in GetComponentsInChildren<Text>(true))
                    if (!bindings.ContainsKey(text.GetInstanceID())) bindings.Add(text.GetInstanceID(), new Binding { text = text });
                var stale = new List<int>();
                foreach (var pair in bindings) if (!pair.Value.text) stale.Add(pair.Key);
                foreach (int id in stale) bindings.Remove(id);
            }
            foreach (var binding in bindings.Values)
            {
                if (!binding.text) continue;
                if (binding.text.text != binding.rendered || binding.source == null) binding.source = binding.text.text;
                binding.rendered = language.Text(binding.source);
                if (binding.text.text != binding.rendered) binding.text.text = binding.rendered;
            }
        }
    }
}
