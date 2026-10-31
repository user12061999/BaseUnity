using System;
using System.Linq;
using BaseUnity.Time;
using UnityEngine;
namespace BaseUnity.Core
{
    public abstract class ModuleInstaller : MonoBehaviour
    {
        public virtual int Priority => 0;
        public abstract void Install(ModuleBootstrap bootstrap);
        public virtual void Tick(float deltaTime,float unscaledDeltaTime) { }
        public virtual void Focus(bool focused) { }
    }
    /// <summary>Minimal bootstrap. Only enabled installers on this root register services.</summary>
    [DefaultExecutionOrder(-10000)]
    public class ModuleBootstrap : MonoBehaviour
    {
        public static ModuleBootstrap Instance { get; private set; }
        public ServiceRegistry Services { get; private set; }
        public bool IsReady { get; private set; }
        private ModuleInstaller[] installers;
        private TimerService timers;
        private StateMachine states;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() { Instance=null; }
        protected virtual void Awake()
        {
            if(Instance && Instance!=this) { Destroy(gameObject); return; }
            if(transform.parent) { Debug.LogError("ModuleBootstrap requires a dedicated root",this); enabled=false; return; }
            Instance=this; DontDestroyOnLoad(gameObject); Services=new ServiceRegistry();
            try {
                Services.Register(new EventBus()); Services.Register(timers=new TimerService()); Services.Register(states=new StateMachine());
                installers=GetComponents<ModuleInstaller>().Where(x=>x.enabled).OrderBy(x=>x.Priority).ToArray();
                foreach(var installer in installers) installer.Install(this);
                Configure(Services); IsReady=true; OnReady();
            } catch(Exception error) { Debug.LogException(error,this); Cleanup(); enabled=false; }
        }
        protected virtual void Configure(ServiceRegistry services) { }
        protected virtual void OnReady() { }
        protected virtual void Update()
        {
            if(!IsReady) return;
            timers.Tick(UnityEngine.Time.deltaTime,UnityEngine.Time.unscaledDeltaTime); states.Tick(UnityEngine.Time.deltaTime);
            foreach(var installer in installers) if(installer && installer.enabled) installer.Tick(UnityEngine.Time.deltaTime,UnityEngine.Time.unscaledDeltaTime);
        }
        private void OnApplicationFocus(bool focused) { if(IsReady) foreach(var installer in installers) if(installer) installer.Focus(focused); }
        private void OnApplicationPause(bool paused) { if(paused) OnApplicationFocus(false); }
        private void Cleanup() { if(installers!=null) foreach(var installer in installers) if(installer) try { installer.Focus(false); } catch(Exception error) { Debug.LogException(error,installer); } IsReady=false; if(Instance==this) Instance=null; try { Services?.Dispose(); } catch(Exception error) { Debug.LogException(error); } }
        protected virtual void OnDestroy() { if(Instance==this) Cleanup(); }
    }
}
