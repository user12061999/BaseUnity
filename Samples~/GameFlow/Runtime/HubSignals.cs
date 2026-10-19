using System;
using System.Linq;
using BaseUnity.Features;

namespace BaseUnity.GameFlow
{
    public static class HubSignals
    {
        public static int ClaimableQuests(OfflineService economy)
        {
            if (economy.LoadError != null) return 0;
            var settings = economy.Settings;
            return (economy.MissionsEnabled ? (settings.dailyMissions ?? Array.Empty<DailyMission>()).Count(goal => goal != null && !economy.MissionClaimed(goal.id) && economy.MissionProgress(goal) >= goal.target) : 0) +
                (economy.AchievementsEnabled ? (settings.achievementGoals ?? Array.Empty<DailyMission>()).Count(goal => goal != null && !economy.AchievementClaimed(goal.id) && economy.AchievementProgress(goal) >= goal.target) : 0);
        }
        public static bool DailyReady(OfflineService economy) => economy.LoadError == null && economy.CanClaimDaily;
    }
}
