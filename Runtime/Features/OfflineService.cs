using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BaseUnity.Save;
using UnityEngine;

namespace BaseUnity.Features
{
    [Serializable] public class PlayerProgress
    {
        public int bestScore, sessions, highestCompletedLevel, wins, losses;
        public int highestUnlockedLevel = 1;
    }
    [Serializable] public sealed class SavedResult
    {
        public string session;
        public int level, score;
        public bool won, acknowledged, bonusVerified;
    }

    /// <summary>Local, main-thread economy. Save succeeds before any transaction becomes visible.</summary>
    public sealed class OfflineService
    {
        [Serializable] public sealed class Count { public string id; public long value; }
        [Serializable] public sealed class Profile
        {
            public int schemaVersion;
            public bool progressMigrated;
            public PlayerProgress progress = new PlayerProgress();
            public SavedResult result;
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
        public string LoadError { get; private set; }
        public bool RecoveredFromBackup { get; private set; }
        public bool HasProgress => profile.progressMigrated;
        public bool UsesUnifiedProgress => Enabled || HasProgress;
        public PlayerProgress Progress => JsonUtility.FromJson<PlayerProgress>(JsonUtility.ToJson(profile.progress));
        public SavedResult PendingResult => profile.result != null && !profile.result.acknowledged ? JsonUtility.FromJson<SavedResult>(JsonUtility.ToJson(profile.result)) : null;
        public bool PendingBonus => profile.result != null && profile.result.bonusVerified && !profile.doubled;
        public string RestoredSession => activeSession;
        public OfflineSettings Settings => JsonUtility.FromJson<OfflineSettings>(JsonUtility.ToJson(settings));
        public string LastError { get; private set; }
        public event Action Changed;
        public long Coins => profile.coins;
        public long Gems => profile.gems;
        public int LastReward => profile.rewardSession == activeSession ? profile.lastReward : 0;
        public bool CanDouble => Enabled && settings.levelRewards && settings.rewardedMultiplier && LastReward > 0 && !profile.doubled && (profile.result == null || !profile.result.acknowledged);
        public bool ShopEnabled => Enabled && settings.shop;
        public bool MissionsEnabled => Enabled && settings.missions;
        public bool DailyEnabled => Enabled && settings.dailyReward;
        private string Today => utcNow().ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public OfflineService(OfflineSettings source, ISaveStore saves, Func<DateTime> utcNow = null)
        {
            settings = JsonUtility.FromJson<OfflineSettings>(JsonUtility.ToJson(source ?? new OfflineSettings()));
            this.saves = saves ?? throw new ArgumentNullException(nameof(saves)); this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            profile = new Profile { coins = Math.Max(0, settings.startingCoins), gems = Math.Max(0, settings.startingGems) };
            try
            {
                Profile loaded;
                if (saves is JsonSaveStore json)
                {
                    var state = json.LoadValidated(settings.saveSlot, out loaded, ValidateProfile);
                    RecoveredFromBackup = state == SaveLoadState.RecoveredFromBackup;
                    if (state != SaveLoadState.Missing) profile = loaded;
                }
                else if (saves.TryLoad<Profile>(settings.saveSlot, out loaded) && loaded != null)
                { if (!ValidateProfile(loaded)) throw new InvalidOperationException("Offline save contains invalid data."); profile = loaded; }
                Normalize(profile);
                if (PendingResult != null) { activeSession = profile.result.session; sessionFinished = true; }
            }
            catch (Exception e) { LoadError = e.Message; LastError = LoadError; }
        }
        private static void Normalize(Profile p)
        {
            p.inventory = p.inventory ?? new List<Count>(); p.purchases = p.purchases ?? new List<Count>();
            p.completedLevels = p.completedLevels ?? new List<int>(); p.claimedMissions = p.claimedMissions ?? new List<string>();
            p.progress = p.progress ?? new PlayerProgress();
            // JsonUtility represents null nested serializable objects as zero-valued objects.
            if (p.result != null && string.IsNullOrEmpty(p.result.session) && p.result.level == 0 && p.result.score == 0 && !p.result.won && !p.result.acknowledged && !p.result.bonusVerified) p.result = null;
        }
        private static bool ValidateProfile(Profile p)
        {
            if (p.schemaVersion > 2) throw new NotSupportedException("Offline profile needs a newer BaseUnity version.");
            Normalize(p);
            return p.schemaVersion >= 0 && p.coins >= 0 && p.gems >= 0 && p.dailyClaims >= 0 && p.lastReward >= 0 &&
                p.inventory.All(x => x != null && !string.IsNullOrWhiteSpace(x.id) && x.value >= 0) &&
                p.purchases.All(x => x != null && !string.IsNullOrWhiteSpace(x.id) && x.value >= 0) &&
                p.inventory.Select(x => x.id).Distinct().Count() == p.inventory.Count &&
                p.purchases.Select(x => x.id).Distinct().Count() == p.purchases.Count &&
                p.gamesPlayed >= 0 && p.gamesWon >= 0 && p.score >= 0 && p.progress.sessions >= 0 && p.progress.bestScore >= 0 &&
                (p.result == null || (!string.IsNullOrEmpty(p.result.session) && p.result.level > 0 && p.result.score >= 0));
        }
        public bool InitializeProgress(PlayerProgress legacy)
        {
            if (LoadError != null) return Reject(LoadError);
            if (HasProgress) return true;
            return Commit(p => { p.progress = JsonUtility.FromJson<PlayerProgress>(JsonUtility.ToJson(legacy ?? new PlayerProgress())); p.progressMigrated = true; });
        }
        public bool AcknowledgeResult()
        {
            if (PendingBonus) return Reject("Pending reward must be saved first");
            if (PendingResult == null) return true;
            return Commit(p => p.result.acknowledged = true, true);
        }
        // Call only after the SDK's verified reward callback. The durable receipt is retried on startup.
        public bool RecordRewardedCompletion(string session)
        {
            if (session != activeSession || !CanDouble || profile.result == null) return Reject("Reward already claimed or expired");
            if (PendingBonus) return true;
            return Commit(p => p.result.bonusVerified = true);
        }
        public bool RecoverPendingBonus()
        {
            if (!PendingBonus) return true;
            return Commit(p => { Credit(p, Currency.Coin, p.lastReward); p.doubled = true; }, true);
        }
        private bool Commit(Action<Profile> change, bool progressOnly = false)
        {
            if (LoadError != null) return Reject(LoadError);
            if (!Enabled && !(progressOnly && HasProgress)) return Reject("Feature is disabled");
            try
            {
                var next = JsonUtility.FromJson<Profile>(JsonUtility.ToJson(profile));
                Normalize(next); change(next); next.schemaVersion = 2;
                saves.Save(settings.saveSlot, next); profile = next; LastError = null;
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
        public bool FinishSession(string session, int level, bool won, int score, int rewardOverride = -1, int levelCount = int.MaxValue)
        {
            if (session != activeSession || string.IsNullOrEmpty(session) || sessionFinished || level < 1) return Reject("Session already finished or expired");
            sessionFinished = true; // Block reentrant completion from Changed callbacks.
            bool ok = Commit(p => {
                p.progress.bestScore = Math.Max(p.progress.bestScore, score);
                p.progress.sessions = (int)Math.Min(int.MaxValue, (long)p.progress.sessions + 1);
                if (won) {
                    p.progress.wins = (int)Math.Min(int.MaxValue, (long)p.progress.wins + 1);
                    p.progress.highestCompletedLevel = Math.Max(p.progress.highestCompletedLevel, level);
                    p.progress.highestUnlockedLevel = Math.Max(p.progress.highestUnlockedLevel, (int)Math.Min(levelCount, (long)level + 1));
                } else p.progress.losses = (int)Math.Min(int.MaxValue, (long)p.progress.losses + 1);
                p.result = new SavedResult { session = session, level = level, won = won, score = Math.Max(0, score) };
                if (Enabled && settings.missions) { RotateMissions(p); p.gamesPlayed = checked(p.gamesPlayed + 1); if (won) p.gamesWon = checked(p.gamesWon + 1); p.score = checked(p.score + Math.Max(0, score)); }
                p.rewardSession = session; p.doubled = false; p.lastReward = 0;
                if (Enabled && won && settings.levelRewards)
                {
                    int reward = Math.Max(0, rewardOverride >= 0 ? rewardOverride : settings.winCoins);
                    if (!p.completedLevels.Contains(level)) { reward = checked(reward + Math.Max(0, settings.firstWinBonus)); p.completedLevels.Add(level); }
                    Credit(p, Currency.Coin, reward); p.lastReward = reward;
                }
            }, true);
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
