using BaseUnity.Core;
using BaseUnity.EconomyModules;
namespace BaseUnity.Progression
{
    public sealed class PlayerProgressionModule : ModuleInstaller
    {
        public PlayerProgressionConfig config;
        public override int Priority => -60;
        public override void Install(ModuleBootstrap bootstrap) { bootstrap.Services.Register(new PlayerProgressionService(bootstrap.Services.Get<EconomyStore>(),config)); }
    }
}
