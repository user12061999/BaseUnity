using BaseUnity.Core;
namespace BaseUnity.EconomyModules
{
    public sealed class ShopModule : ModuleInstaller
    {
        public ShopCatalog catalog;
        public override int Priority => -60;
        public override void Install(ModuleBootstrap bootstrap)
        { bootstrap.Services.Register(new ShopService(bootstrap.Services.Get<EconomyStore>(),catalog?catalog.offers:new ShopOffer[0])); }
    }
}
