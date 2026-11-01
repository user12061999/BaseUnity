using System.Collections.Generic;
using BaseUnity.Core;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.Localization.UI
{
    /// <summary>Tracks source text separately so switching back never translates an already translated string.</summary>
    public sealed class LocalizedCanvas : MonoBehaviour
    {
        private LocalizationService service;
        private string lastLanguage;
        private readonly Dictionary<Text,string> source=new Dictionary<Text,string>(),output=new Dictionary<Text,string>();
        private void OnEnable() { lastLanguage=null;Capture(); }
        public void Capture() { foreach(var text in GetComponentsInChildren<Text>(true))if(!source.ContainsKey(text)) { source[text]=text.text;output[text]=text.text; } }
        private void LateUpdate()
        {
            if(service==null) { var root=ModuleBootstrap.Instance;if(!root||!root.IsReady||!root.Services.TryGet(out service))return; }
            foreach(var pair in source) {
                var label=pair.Key;if(!label)continue;
                if(label.text!=output[label])sourceUpdates.Add(label);
            }
            foreach(var label in sourceUpdates)source[label]=label.text;sourceUpdates.Clear();
            foreach(var pair in source) { var label=pair.Key;if(!label)continue;if(lastLanguage==service.Language&&label.text==output[label])continue;var translated=service.Text(pair.Value);if(label.text!=translated)label.text=translated;output[label]=translated; }
            lastLanguage=service.Language;
        }
        private readonly List<Text> sourceUpdates=new List<Text>();
        public void English() { service?.SetLanguage("en"); }
        public void Vietnamese() { service?.SetLanguage("vi"); }
        private void OnDisable() { service=null; }
    }
}
