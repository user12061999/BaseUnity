using System;

namespace BaseUnity.Features
{
    public enum Currency { Coin, Gem }
    public enum MissionMetric { GamesPlayed, GamesWon, Score }
    [Serializable] public sealed class ShopItem
    {
        public string id = "energy", title = "Energy";
        public Currency currency;
        public int price = 50, quantity = 1;
        public int purchaseLimit;
    }
    [Serializable] public sealed class DailyMission
    {
        public string id = "play", title = "Play 3 games";
        public MissionMetric metric;
        public int target = 3, coins = 100;
    }
    [Serializable] public sealed class Translation
    {
        public string english, vietnamese;
    }
    [Serializable] public sealed class OfflineSettings
    {
        public bool enabled;
        public string saveSlot = "offline_profile";
        public int startingCoins, startingGems;
        public bool levelRewards = true, shop = true, dailyReward = true, missions = true;
        public bool achievements = true;
        public DailyMission[] achievementGoals = { new DailyMission { id = "first_win", title = "Win your first game", metric = MissionMetric.GamesWon, target = 1, coins = 200 }, new DailyMission { id = "ten_games", title = "Play 10 games", target = 10, coins = 500 } };
        public int winCoins = 50, firstWinBonus = 100;
        public bool rewardedMultiplier = true;
        public int[] dailyCoins = { 50, 75, 100, 125, 150, 200, 300 };
        public ShopItem[] items = { new ShopItem(), new ShopItem { id = "ticket", title = "Ticket", price = 100 } };
        public DailyMission[] dailyMissions = { new DailyMission(), new DailyMission { id = "win", title = "Win 2 games", metric = MissionMetric.GamesWon, target = 2, coins = 150 }, new DailyMission { id = "score", title = "Score 100 points", metric = MissionMetric.Score, target = 100, coins = 100 } };
    }
    [Serializable] public sealed class LocalizationSettings
    {
        public bool enabled;
        public string defaultLanguage = "en";
        public Translation[] translations = Array.Empty<Translation>();
    }
    [Serializable] public sealed class GameIntegrationSettings
    {
        public bool analyticsEvents;
        public bool fetchRemoteConfigAtHome;
        public bool bannerAtHome;
        public bool interstitialBetweenLevels;
        public int interstitialEveryWins = 3;
    }
}
