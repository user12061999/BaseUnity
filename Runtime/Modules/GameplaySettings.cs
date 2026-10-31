using System;
namespace BaseUnity.Features
{
    public enum GiftKind { Welcome, Return, Playtime, Periodic }
    [Serializable] public sealed class GiftDefinition
    {
        public string id = "welcome", title = "Welcome gift";
        public GiftKind kind;
        public int coins = 100, energy;
        public double seconds = 3600;
    }
    [Serializable] public sealed class GameplaySettings
    {
        public bool enabled;
        public bool energy = true, progression = true, gifts = true, badges = true;
        public bool effects = true, interactions = true, mobileControls = true;
        public int maximumEnergy = 5, energyPerRun = 1;
        public double energyRefillSeconds = 300;
        public int experiencePerWin = 25, experiencePerLoss = 5, experiencePerLevel = 100, maximumPlayerLevel = 100, levelUpCoins = 50;
        public GiftDefinition[] giftCatalog = {
            new GiftDefinition(),
            new GiftDefinition { id = "return", title = "Welcome back", kind = GiftKind.Return, seconds = 172800, coins = 150 },
            new GiftDefinition { id = "playtime", title = "Play for five minutes", kind = GiftKind.Playtime, seconds = 300, coins = 75 },
            new GiftDefinition { id = "free", title = "Free gift", kind = GiftKind.Periodic, seconds = 14400, coins = 25 }
        };
    }
    [Serializable] public sealed class AdvancedSettings
    {
        public bool enabled;
        public int defaultFrameRate = 60;
        public int batteryFrameRate = 30;
    }
}
