using BaseUnity.Core;
using BaseUnity.EconomyModules;
namespace BaseUnity.Energy
{
    public sealed class EnergyModule : ModuleInstaller
    {
        public EnergyConfig config;
        public override int Priority => -60;
        public override void Install(ModuleBootstrap bootstrap) { bootstrap.Services.Register(new EnergyService(bootstrap.Services.Get<EconomyStore>(),config)); }
    }
}
