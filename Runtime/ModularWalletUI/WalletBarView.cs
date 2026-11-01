using BaseUnity.Core;
using UnityEngine.UI;
namespace BaseUnity.EconomyModules.UI
{
    public sealed class WalletBarView : EconomyViewBase
    {
        public Text coinLabel,gemLabel;
        public string coinId="coin",gemId="gem";
        public void GrantDemoStarter() { Execute(()=>Store.Grant("demo.starter.v1",new EconomyReward { id="coin",amount=1000 },new EconomyReward { id="gem",amount=10 })); }
        protected override bool Bind(ServiceRegistry services) { return true; }
        protected override void RefreshView() { if(coinLabel)coinLabel.text=coinId+": "+Store.Balance(coinId);if(gemLabel)gemLabel.text=gemId+": "+Store.Balance(gemId); }
    }
}
