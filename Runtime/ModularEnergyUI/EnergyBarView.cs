using BaseUnity.Core;
using BaseUnity.EconomyModules.UI;
using UnityEngine.UI;
namespace BaseUnity.Energy.UI
{
    public sealed class EnergyBarView : EconomyViewBase
    {
        public Text energyLabel,timerLabel;
        private EnergyService energy;
        protected override bool Bind(ServiceRegistry services) => services.TryGet(out energy);
        protected override void RefreshView()
        {
            if(energyLabel)energyLabel.text="Energy "+energy.Current+" / "+energy.Capacity;
            if(timerLabel)timerLabel.text=energy.Current>=energy.Capacity?"Energy full":"Next energy in "+energy.RemainingSeconds+"s";
        }
    }
}
