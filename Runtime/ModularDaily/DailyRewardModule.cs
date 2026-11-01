using BaseUnity.Core;
using UnityEngine;
namespace BaseUnity.EconomyModules
{
    public sealed class DailyRewardModule : ModuleInstaller
    {
        public DailyRewardSchedule schedule;
        public override int Priority => -60;
        public override void Install(ModuleBootstrap bootstrap)
        {
            if(!schedule)throw new System.InvalidOperationException("Assign a DailyRewardSchedule to DailyRewardModule");
            bootstrap.Services.Register(new DailyRewardService(bootstrap.Services.Get<EconomyStore>(),schedule));
        }
    }
}
