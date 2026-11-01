using System.Linq;
using BaseUnity.Core;
using UnityEngine.UI;
namespace BaseUnity.EconomyModules.UI
{
    public sealed class AchievementItemView : EconomyViewBase
    {
        public string objectiveId="win_3";
        public Text titleLabel,progressLabel,rewardLabel;
        public Button claimButton;
        private AchievementService service;
        protected override bool Bind(ServiceRegistry services) => services.TryGet(out service);
        protected override void RefreshView()
        {
            var status=service.Statuses.FirstOrDefault(x=>x.id==objectiveId);
            if(claimButton)claimButton.interactable=status!=null&&status.CanClaim;
            if(status==null) { if(titleLabel)titleLabel.text="Unknown objective: "+objectiveId;return; }
            if(titleLabel)titleLabel.text=status.title;
            if(rewardLabel)rewardLabel.text=string.Join(" + ",status.rewards.Select(r=>r.amount+" "+r.id));
            if(progressLabel)progressLabel.text=status.claimed?"Claimed":status.progress+" / "+status.target;
        }
        public void RecordDemoWin() { Execute(()=>service.Record("win",1,System.Guid.NewGuid().ToString("N"))); }
        public void Claim() { Execute(()=>service.Claim(objectiveId)); }
    }
}
