using BaseUnity.Core;
using BaseUnity.Save;
using UnityEngine;
namespace BaseUnity.EconomyModules
{
    public sealed class WalletModule : ModuleInstaller
    {
        public string saveSlot="baseunity_modular_economy";
        public override int Priority => -80;
        public override void Install(ModuleBootstrap bootstrap)
        {
            var store=new EconomyStore(bootstrap.Services.Get<ISaveStore>(),saveSlot);
            store.ObserverError+=error=>Debug.LogException(error,this);bootstrap.Services.Register(store);
        }
    }
}
