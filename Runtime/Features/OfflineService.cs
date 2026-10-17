using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BaseUnity.Save;
using UnityEngine;

namespace BaseUnity.Features
{
    /// <summary>Local, main-thread economy. Save succeeds before any transaction becomes visible.</summary>
    public sealed class OfflineService
    {
        [Serializable] public sealed class Count { public string id; public long value; }
        [Serializable] public sealed class Profile
        {
            public long coins, gems;
            public List<Count> inventory = new List<Count>(), purchases = new List<Count>();
            public List<int> completedLevels = new List<int>();
            public string rewardSession, dailyClaimDate, missionDate;
            public int lastReward, dailyClaims;
            public bool doubled;
            public long gamesPlayed, gamesWon, score;
            public List<string> claimedMissions = new List<string>();
        }
        private readonly OfflineSettings settings;
        private readonly ISaveStore saves;
        private readonly Func<DateTime> utcNow;
        private Profile profile;
        private string activeSession;
        private bool sessionFinished;
        public bool Enabled => settings.enabled;
        public OfflineSettings Settings => JsonUtility.FromJson<OfflineSettings>(JsonUtility.ToJson(settings));
        public string LastError { get; private set; }
        public event Action Changed;
        public long Coins => profile.coins;
        public long Gems => profile.gems;
        public int LastReward => profile.rewardSession == activeSession ? profile.lastReward : 0;
        public bool CanDouble => Enabled && settings.levelRewards && settings.rewardedMultiplier && LastReward > 0 && !profile.doubled;
        public bool ShopEnabled => Enabled && settings.shop;
        public bool MissionsEnabled => Enabled && settings.missions;
        public bool DailyEnabled => Enabled && settings.dailyReward;
        private string Today => utcNow().ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public OfflineService(OfflineSettings source, ISaveStore saves, Func<DateTime> utcNow = null)
        {
            settings = JsonUtility.FromJson<OfflineSettings>(JsonUtility.ToJson(source ?? new OfflineSettings()));
            this.saves = saves ?? throw new ArgumentNullException(nameof(saves)); this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            profile = new Profile { coins = Math.Max(0, settings.startingCoins), gems = Math.Max(0, settings.startingGems) };
            if (Enabled && saves.TryLoad<Profile>(settings.saveSlot, out var loaded) && loaded != null) profile = loaded;
            profile.inventory = profile.inventory ?? new List<Count>(); profile.purchases = profile.purchases ?? new List<Count>();
            profile.completedLevels = profile.completedLevels ?? new List<int>(); profile.claimedMissions = profile.claimedMissions ?? new List<string>();
            if (profile.coins < 0 || profile.gems < 0 || profile.dailyClaims < 0 || profile.inventory.Any(x => x == null || x.value < 0) || profile.purchases.Any(x => x == null || x.value < 0)) throw new InvalidOperationException("Offline save contains invalid balances or counts.");
        }
        private bool Commit(Action<Profile> change)
        {
            if (!Enabled) { LastError = "Feature is disabled"; return false; }
            try
            {
                var next = JsonUtility.FromJson<Profile>(JsonUtility.ToJson(profile));
                change(next); saves.Save(settings.saveSlot, next); profile = next; LastError = null;
            }
            catch (Exception e) { LastError = e.Message; return false; }
            if (Changed != null) foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception e) { Debug.LogException(e); }
            return true;
        }
        private bool Reject(string error) { LastError = error; return false; }
        private static long CountOf(List<Count> entries, string id) => entries.FirstOrDefault(x => x.id == id)?.value ?? 0;
        private static void AddCount(List<Count> entries, string id, long amount)
        {
            var entry = entries.FirstOrDefault(x => x.id == id);
            if (entry == null) { entry = new Count { id = id }; entries.Add(entry); }
            entry.value = checked(entry.value + amount);
        }
        private static void Credit(Profile data, Currency currency, long amount)
        { if (currency == Currency.Coin) data.coins = checked(data.coins + amount); else data.gems = checked(data.gems + amount); }
        public long Balance(Currency currency) => currency == Currency.Coin ? Coins : Gems;
        public long ItemCount(string id) => CountOf(profile.inventory, id);
        public bool AddCurrency(Currency currency, long amount) => amount > 0 && Enum.IsDefined(typeof(Currency), currency) ? Commit(p => Credit(p, currency, amount)) : Reject("Amount must be positive");
        public bool Spend(Currency currency, long amount)
        {
            if (amount <= 0 || !Enum.IsDefined(typeof(Currency), currency)) return Reject("Invalid amount or currency");
            if (Balance(currency) < amount) return Reject("Not enough currency");
            return Commit(p => Credit(p, currency, -amount));
        }
        public bool ConsumeItem(string id, long amount = 1)
        {
            if (string.IsNullOrWhiteSpace(id) || amount <= 0 || ItemCount(id) < amount) return Reject("Not enough items");
            return Commit(p => AddCount(p.inventory, id, -amount));
        }
        public bool Buy(string id)
        {
            if (!ShopEnabled) return Reject("Shop is disabled");
            var item = (settings.items ?? Array.Empty<ShopItem>()).FirstOrDefault(x => x != null && x.id == id);
            if (item == null || string.IsNullOrWhiteSpace(id) || item.price < 0 || item.quantity < 1 || item.purchaseLimit < 0 || !Enum.IsDefined(typeof(Currency), item.currency)) return Reject("Invalid shop item");
            if (item.purchaseLimit > 0 && CountOf(profile.purchases, id) >= item.purchaseLimit) return Reject("Purchase limit reached");
            if (Balance(item.currency) < item.price) return Reject("Not enough currency");
            return Commit(p => { Credit(p, item.currency, -item.price); AddCount(p.inventory, id, item.quantity); AddCount(p.purchases, id, 1); });
        }
        public string BeginSession()
        { activeSession = Guid.NewGuid().ToString("N"); sessionFinished = false; return activeSession; }
        private void RotateMissions(Profile p)
        {
            if (string.CompareOrdinal(Today, p.missionDate) <= 0) return;
            p.missionDate = Today; p.gamesPlayed = p.gamesWon = p.score = 0; p.claimedMissions.Clear();
        }
        public bool FinishSession(string session, int level, bool won, int score, int rewardOverride = -1)
        {
            if (session != activeSession || string.IsNullOrEmpty(session) || sessionFinished || level < 1) return Reject("Session already finished or expired");
            sessionFinished = true; // Block reentrant completion from Changed callbacks.
            bool ok = Commit(p => {
                if (settings.missions) { RotateMissions(p); p.gamesPlayed = checked(p.gamesPlayed + 1); if (won) p.gamesWon = checked(p.gamesWon + 1); p.score = checked(p.score + Math.Max(0, score)); }
                p.rewardSession = session; p.doubled = false; p.lastReward = 0;
                if (won && settings.levelRewards)
                {
                    int reward = Math.Max(0, rewardOverride >= 0 ? rewardOverride : settings.winCoins);
                    if (!p.completedLevels.Contains(level)) { reward = checked(reward + Math.Max(0, settings.firstWinBonus)); p.completedLevels.Add(level); }
                    Credit(p, Currency.Coin, reward); p.lastReward = reward;
                }
            });
            if (!ok) sessionFinished = false;
            return ok;
        }
        public bool DoubleReward(string session)
        {
            if (session != activeSession || !CanDouble) return Reject("Reward already claimed or expired");
            return Commit(p => { Credit(p, Currency.Coin, p.lastReward); p.doubled = true; });
        }
        public bool CanClaimDaily => DailyEnabled && (settings.dailyCoins?.Length ?? 0) > 0 && string.CompareOrdinal(Today, profile.dailyClaimDate) > 0;
        public int DailyIndex => (settings.dailyCoins?.Length ?? 0) == 0 ? 0 : profile.dailyClaims % settings.dailyCoins.Length;
        public int DailyCoins => (settings.dailyCoins?.Length ?? 0) == 0 ? 0 : settings.dailyCoins[DailyIndex];
        public bool ClaimDaily()
        {
            if (!CanClaimDaily) return Reject("Daily reward already claimed");
            if (DailyCoins < 0) return Reject("Invalid daily reward");
            return Commit(p => { Credit(p, Currency.Coin, DailyCoins); p.dailyClaimDate = Today; p.dailyClaims = (DailyIndex + 1) % settings.dailyCoins.Length; });
        }
        public long MissionProgress(DailyMission mission)
        {
            if (string.CompareOrdinal(Today, profile.missionDate) > 0) return 0;
            return mission.metric == MissionMetric.GamesPlayed ? profile.gamesPlayed : mission.metric == MissionMetric.GamesWon ? profile.gamesWon : profile.score;
        }
        public bool MissionClaimed(string id) => string.CompareOrdinal(Today, profile.missionDate) <= 0 && profile.claimedMissions.Contains(id);
        public bool ClaimMission(string id)
        {
            if (!MissionsEnabled) return Reject("Missions are disabled");
            var mission = (settings.dailyMissions ?? Array.Empty<DailyMission>()).FirstOrDefault(x => x != null && x.id == id);
            if (mission == null || mission.target < 1 || mission.coins < 0 || MissionClaimed(id) || MissionProgress(mission) < mission.target) return Reject("Mission is not ready");
            return Commit(p => { Credit(p, Currency.Coin, mission.coins); p.claimedMissions.Add(id); });
        }
    }
}
