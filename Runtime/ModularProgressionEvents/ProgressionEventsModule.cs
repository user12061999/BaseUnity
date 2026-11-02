using System;
using BaseUnity.Core;
using BaseUnity.EconomyModules;
using BaseUnity.Progression;
namespace BaseUnity.ProgressionEvents
{
    public sealed class ProgressionEventsModule : ModuleInstaller
    {
        public long experiencePerWin=25,experiencePerLoss=5;
        private IDisposable subscription;
        public override int Priority => -30;
        public override void Install(ModuleBootstrap bootstrap)
        {
            if(experiencePerWin<0||experiencePerLoss<0)throw new ArgumentException("XP rewards cannot be negative");
            var progression=bootstrap.Services.Get<PlayerProgressionService>();
            subscription=bootstrap.Services.Get<EventBus>().Subscribe<GameplayProgressEvent>(e=> {
                long xp=e.eventId=="win"?experiencePerWin:e.eventId=="lose"?experiencePerLoss:0;
                if(xp==0||progression.IsMaximumLevel)return;EconomyStore.ValidateKey(e.receipt);if(e.amount<1)throw new ArgumentOutOfRangeException(nameof(e.amount));
                var result=progression.GrantExperience(checked(xp*e.amount),"event:"+e.receipt);
                if(result==TransactionResult.Rejected&&!progression.IsMaximumLevel)throw new InvalidOperationException("XP transaction rejected; progress event must be retried");
            });
        }
        private void OnDestroy() { subscription?.Dispose(); }
    }
}
