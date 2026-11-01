using BaseUnity.Core;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.Localization.UI
{
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        public string key="Play";
        private void LateUpdate() { var root=ModuleBootstrap.Instance;LocalizationService service;if(root&&root.IsReady&&root.Services.TryGet(out service))GetComponent<Text>().text=service.Text(key); }
    }
}
