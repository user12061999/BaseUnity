using System;
using UnityEngine;
namespace BaseUnity.Demo
{
    [CreateAssetMenu(menuName="BaseUnity/Modules/Playable Demo Config")]
    public sealed class PlayableDemoConfig : ScriptableObject
    {
        [Min(1)] public int firstTargetCount=5;
        [Min(0)] public int additionalTargetsPerLevel=1;
        [Range(1,3600)] public float roundSeconds=30;
        [Min(1)] public int energyPerRun=1;
        [Min(1)] public int refillAmount=5;
        public long refillCoinPrice=50;
        public void Validate()
        {
            if(firstTargetCount<1||firstTargetCount>10000||additionalTargetsPerLevel<0||additionalTargetsPerLevel>10000||float.IsNaN(roundSeconds)||float.IsInfinity(roundSeconds)||roundSeconds<1||roundSeconds>3600||energyPerRun<1||refillAmount<1||refillCoinPrice<0)throw new ArgumentException("Invalid playable demo settings");
        }
    }
}
