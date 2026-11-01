using BaseUnity.Core;
using BaseUnity.Features;
namespace BaseUnity.IntegrationModules
{
    public sealed class AdsModule : ModuleInstaller
    {
        public AdsSettings settings=new AdsSettings { enabled=true };
        public AdsProvider provider;
        public bool initializeOnStart=true;
        private AdsService service;
        private bool started;
        public override int Priority => -40;
        public override void Install(ModuleBootstrap bootstrap)
        {
            var consent=bootstrap.Services.Get<ConsentService>();settings.tagForUnderAgeOfConsent=consent!=null&&consent.UnderAgeOfConsent;
            service=new AdsService(settings,provider?(System.Func<IAdsBackend>)provider.Create:null,consent);bootstrap.Services.Register(service);
        }
        public override void Tick(float deltaTime,float unscaledDeltaTime) { if(started||!initializeOnStart)return;started=true;Initialize(); }
        public async void Initialize() { if(service!=null)await service.InitializeAsync(); }
    }
}
