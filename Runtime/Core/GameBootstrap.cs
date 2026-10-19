using System;
using UnityEngine;
using BaseUnity.Audio;
using BaseUnity.Input;
using BaseUnity.Save;
using BaseUnity.Scenes;
using BaseUnity.Time;
using BaseUnity.UI;
using BaseUnity.Features;

namespace BaseUnity.Core
{
    /// <summary>Place once on a dedicated scene root. Derive to register game services before Ready.</summary>
    [DefaultExecutionOrder(-10000)]
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }
        public ServiceRegistry Services { get; private set; }
        public bool IsReady { get; private set; }
        public string StartupScenePath { get; private set; }
        private TimerService timers;
        private StateMachine states;
        [SerializeField] private FeatureConfig featureConfig;
        public FeatureConfig FeatureConfig { get => featureConfig; set => featureConfig = value; }
        protected virtual FeatureConfig GetFeatureConfig() => featureConfig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        protected virtual void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            if (transform.parent) { Debug.LogError("GameBootstrap must be on a dedicated root GameObject.", this); enabled = false; return; }
            Instance = this;
            StartupScenePath = gameObject.scene.path;
            DontDestroyOnLoad(gameObject);
            Services = new ServiceRegistry();
            try
            {
                Services.Register(new EventBus());
                Services.Register(states = new StateMachine());
                Services.Register(timers = new TimerService());
                Services.Register<ISaveStore>(new JsonSaveStore(BuildEnvironment.SaveDirectory));
                Services.Register(new SceneService());
                Services.Register(new UIService());
                Services.Register(new AudioService(transform, Services.Get<ISaveStore>()));
                var manual = new ManualInputSource();
                Services.Register(manual);
                Services.Register(new InputRouter(CreateInputSource(manual)));
                Configure(Services);
                var features = new FeatureServices(GetFeatureConfig());
                features.Register(Services);
                IsReady = true;
                _ = features.InitializeAsync();
                OnReady();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                IsReady = false;
                enabled = false;
                try { Services.Dispose(); } catch (Exception cleanup) { Debug.LogException(cleanup, this); }
                Instance = null;
            }
        }

        protected virtual IInputSource CreateInputSource(ManualInputSource fallback) => fallback;
        protected virtual void Configure(ServiceRegistry services) { }
        protected virtual void OnReady() { }
        protected virtual void Update()
        {
            if (!IsReady) return;
            timers.Tick(UnityEngine.Time.deltaTime, UnityEngine.Time.unscaledDeltaTime);
            states.Tick(UnityEngine.Time.deltaTime);
        }
        protected virtual void OnApplicationFocus(bool focus)
        { if (!focus && IsReady) Services.Get<ManualInputSource>().Reset(); }
        protected virtual void OnDestroy()
        {
            if (Instance != this) return;
            IsReady = false;
            Instance = null;
            try { Services?.Dispose(); } catch (Exception error) { Debug.LogException(error); }
        }
    }
}
