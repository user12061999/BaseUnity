using System;
using BaseUnity.Core;
using UnityEngine.UI;
namespace BaseUnity.EconomyModules.UI
{
    public sealed class DailyRewardView : EconomyViewBase
    {
        public Text dayLabel,rewardLabel,timerLabel;
        public Button claimButton;
        private DailyRewardService daily;
        protected override bool Bind(ServiceRegistry services) => services.TryGet(out daily);
        protected override void RefreshView()
        {
            if(dayLabel)dayLabel.text="Day "+(daily.NextDay+1)+" / "+daily.DayCount;
            if(rewardLabel)rewardLabel.text=string.Join(" + ",Array.ConvertAll(daily.RewardsAt(daily.NextDay),r=>r.amount+" "+r.id));
            if(timerLabel)timerLabel.text=daily.CanClaim?"Ready":"Wait "+TimeSpan.FromSeconds(daily.RemainingSeconds).ToString(@"dd\.hh\:mm\:ss");
            if(claimButton)claimButton.interactable=daily.CanClaim;
        }
        public void Claim() { Execute(()=>daily.Claim()); }
    }
}
