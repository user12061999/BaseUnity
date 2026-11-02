using System;
using UnityEngine;
using BaseUnity.EconomyModules;
namespace BaseUnity.Progression
{
    [CreateAssetMenu(menuName="BaseUnity/Modules/Player Progression Config")]
    public sealed class PlayerProgressionConfig : ScriptableObject
    {
        public string id="player";
        public long experiencePerLevel=100;
        [Range(1,10000)] public int maximumLevel=100;
        public EconomyReward[] levelUpRewards={new EconomyReward { id="coin",amount=25 }};
        public void Validate()
        {
            EconomyStore.ValidateId(id);if(experiencePerLevel<1||maximumLevel<1||maximumLevel>10000||levelUpRewards==null)throw new ArgumentException("Invalid progression thresholds");
            try { checked { long cap=(maximumLevel-1)*experiencePerLevel; } }catch(OverflowException) { throw new ArgumentException("Progression XP cap exceeds Int64"); }
            foreach(var reward in levelUpRewards) { if(reward==null)throw new ArgumentException("Null level reward");reward.Validate(); }
        }
    }
}
