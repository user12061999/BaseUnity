using BaseUnity.Core;
using UnityEngine;
namespace BaseUnity.EconomyModules
{
    public sealed class AchievementService : ObjectiveService
    {
        public AchievementService(EconomyStore store,ObjectiveDefinition[] definitions,EventBus events=null,System.Func<long> utcSeconds=null)
            : base(store,"achievements",definitions,false,events,utcSeconds) { }
    }
    public sealed class AchievementModule : ModuleInstaller
    {
        public AchievementCatalog catalog;
        public override int Priority => -60;
        public override void Install(ModuleBootstrap bootstrap)
        { bootstrap.Services.Register(new AchievementService(bootstrap.Services.Get<EconomyStore>(),catalog?catalog.objectives:new ObjectiveDefinition[0],bootstrap.Services.Get<EventBus>())); }
    }
}
