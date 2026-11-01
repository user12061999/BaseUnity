using BaseUnity.Core;
using BaseUnity.Features;
namespace BaseUnity.IntegrationModules
{
    public sealed class ConsentModule : ModuleInstaller
    {
        public ConsentSettings settings=new ConsentSettings();
        public ConsentProvider provider;
        public bool initializeOnStart=true;
        private ConsentService service;
        private bool started;
        public override int Priority => -50;
        public override void Install(ModuleBootstrap bootstrap)
        {
            service=new ConsentService(settings,provider?(System.Func<IConsentBackend>)provider.Create:null);bootstrap.Services.Register(service);
        }
        public override void Tick(float deltaTime,float unscaledDeltaTime) { if(started||!initializeOnStart)return;started=true;Initialize(); }
        public async void Initialize() { if(service!=null)await service.InitializeAsync(); }
    }
}
