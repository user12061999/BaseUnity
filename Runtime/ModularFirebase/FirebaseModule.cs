using BaseUnity.Core;
using BaseUnity.Features;
namespace BaseUnity.IntegrationModules
{
    public sealed class FirebaseModule : ModuleInstaller
    {
        public FirebaseSettings settings=new FirebaseSettings { enabled=true };
        public FirebaseProvider provider;
        public bool initializeOnStart=true;
        private FirebaseService service;
        private bool started;
        public override int Priority => -40;
        public override void Install(ModuleBootstrap bootstrap)
        {
            service=new FirebaseService(settings,provider?(System.Func<IFirebaseBackend>)provider.Create:null);bootstrap.Services.Register(service);
        }
        public override void Tick(float deltaTime,float unscaledDeltaTime) { if(started||!initializeOnStart)return;started=true;Initialize(); }
        public async void Initialize() { if(service!=null)await service.InitializeAsync(); }
    }
}
