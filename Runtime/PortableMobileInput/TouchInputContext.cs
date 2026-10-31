using BaseUnity.Core;
using BaseUnity.Input;
using UnityEngine;
namespace BaseUnity.MobileInput
{
    /// <summary>Can bind custom sources without a bootstrap. Never owns or persists player preferences.</summary>
    public sealed class TouchInputContext : MonoBehaviour
    {
        private ManualInputSource source;
        private InputRouter router;
        private ModuleBootstrap owner;
        private bool autoBound;
        public bool Enabled { get; set; } = true;
        public bool IsOpen => isActiveAndEnabled && Enabled && Source!=null && (router==null || router.Enabled);
        public ManualInputSource Source
        {
            get {
                if(autoBound && (!owner || !owner.IsReady)) { source=null; router=null; owner=null; autoBound=false; }
                if(source==null && ModuleBootstrap.Instance && ModuleBootstrap.Instance.IsReady) { ModuleBootstrap.Instance.Services.TryGet(out source); ModuleBootstrap.Instance.Services.TryGet(out router); owner=ModuleBootstrap.Instance; autoBound=true; }
                return source;
            }
        }
        public void Bind(ManualInputSource manual,InputRouter input=null) { source=manual; router=input; owner=null; autoBound=false; }
        private void OnDisable() { source?.Reset(); }
        private void OnApplicationFocus(bool focused) { if(!focused) source?.Reset(); }
        private void OnApplicationPause(bool paused) { if(paused) source?.Reset(); }
    }
}
