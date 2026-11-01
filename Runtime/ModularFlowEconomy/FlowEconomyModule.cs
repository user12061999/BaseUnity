using BaseUnity.Core;
using BaseUnity.Flow;
using BaseUnity.EconomyModules;
namespace BaseUnity.FlowEconomy
{
    public sealed class FlowEconomyModule : ModuleInstaller
    {
        public FlowRewardConfig config;
        public FlowEconomyBridge Bridge { get; private set; }
        private float retry;
        public override int Priority => 50;
        public override void Install(ModuleBootstrap bootstrap)
        { if(!config)throw new System.InvalidOperationException("Assign FlowRewardConfig");Bridge=new FlowEconomyBridge(bootstrap.Services.Get<GameFlowService>(),bootstrap.Services.Get<EconomyStore>(),bootstrap.Services.Get<EventBus>(),config);bootstrap.Services.Register(Bridge); }
        public override void Tick(float deltaTime,float unscaledDeltaTime) { retry-=unscaledDeltaTime;if(retry<=0) { retry=1;if(Bridge.HasPending)Bridge.RetryPending(); } }
        public void RetryPending() { Bridge?.RetryPending(); }
    }
}
