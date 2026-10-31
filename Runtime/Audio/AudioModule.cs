using BaseUnity.Core;
using BaseUnity.Save;
namespace BaseUnity.Audio
{
    public sealed class AudioModule : ModuleInstaller
    {
        private AudioService service;
        public override void Install(ModuleBootstrap bootstrap) { bootstrap.Services.Register(service=new AudioService(transform,bootstrap.Services.Get<ISaveStore>())); }
        public override void Tick(float deltaTime,float unscaledDeltaTime) { service?.Tick(unscaledDeltaTime); }
    }
}
