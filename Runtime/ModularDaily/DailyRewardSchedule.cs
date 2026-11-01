using UnityEngine;
namespace BaseUnity.EconomyModules {
    [CreateAssetMenu(menuName="BaseUnity/Modules/Daily Reward Schedule")]
    public sealed class DailyRewardSchedule : ScriptableObject
    {
        public string id="daily";
        [Min(1)] public int cooldownSeconds=86400;
        public DailyRewardDay[] days={new DailyRewardDay(),new DailyRewardDay(),new DailyRewardDay()};
    }
}
