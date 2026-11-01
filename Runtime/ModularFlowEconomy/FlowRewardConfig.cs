using UnityEngine;
using BaseUnity.EconomyModules;
namespace BaseUnity.FlowEconomy
{
    [CreateAssetMenu(menuName="BaseUnity/Modules/Flow Reward Config")]
    public sealed class FlowRewardConfig : ScriptableObject
    {
        public EconomyReward[] winRewards={new EconomyReward { id="coin",amount=100 }};
        public EconomyReward[] loseRewards=new EconomyReward[0];
        public bool publishProgress=true;
    }
}
