using BaseUnity.Core;
namespace BaseUnity.UI
{
    public sealed class UIModule : ModuleInstaller
    {
        public override void Install(ModuleBootstrap bootstrap) { bootstrap.Services.Register(new UIService()); }
    }
}
