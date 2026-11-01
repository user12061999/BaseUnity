using BaseUnity.Core;
using UnityEngine;
namespace BaseUnity.EconomyModules
{
    public sealed class QuestService : ObjectiveService
    {
        public QuestService(EconomyStore store,ObjectiveDefinition[] definitions,EventBus events=null,System.Func<long> utcSeconds=null)
            : base(store,"quests",definitions,true,events,utcSeconds) { }
    }
    public sealed class QuestModule : ModuleInstaller
    {
        public QuestCatalog catalog;
        public override int Priority => -60;
        public override void Install(ModuleBootstrap bootstrap)
        { bootstrap.Services.Register(new QuestService(bootstrap.Services.Get<EconomyStore>(),catalog?catalog.objectives:new ObjectiveDefinition[0],bootstrap.Services.Get<EventBus>())); }
    }
}
