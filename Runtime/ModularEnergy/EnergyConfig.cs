using System;
using UnityEngine;
namespace BaseUnity.Energy
{
    [CreateAssetMenu(menuName="BaseUnity/Modules/Energy Config")]
    public sealed class EnergyConfig : ScriptableObject
    {
        public string id="main";
        [Min(1)] public int capacity=5;
        [Min(0)] public int initialEnergy=5;
        [Min(1)] public int refillSeconds=300;
        public void Validate()
        {
            BaseUnity.EconomyModules.EconomyStore.ValidateId(id);
            if(capacity<1||capacity>1000000||initialEnergy<0||initialEnergy>capacity||refillSeconds<1)throw new ArgumentException("Invalid energy capacity/initial value/refill interval");
        }
    }
}
