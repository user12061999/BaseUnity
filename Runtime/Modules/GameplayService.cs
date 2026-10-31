using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BaseUnity.Features
{
    [Serializable] public sealed class GameplayState
    {
        public bool energyInitialized;
        public int energy;
        public long experience;
        public double energyAnchor, lastVisit, playSeconds;
        public List<string> claimedGifts = new List<string>(), returnGifts = new List<string>();
        public List<GiftClock> giftClocks = new List<GiftClock>();
    }
    [Serializable] public sealed class GiftClock { public string id; public double next; }
    /// <summary>Shares the wallet's atomic profile commit. Local clock is monotonic per saved anchor, not authoritative anti-cheat time.</summary>
    public sealed class GameplayService
    {
        private readonly OfflineService offline;
        private readonly GameplaySettings settings;
        private readonly Func<DateTime> clock;
        private double pendingPlay;
        public string ConfigurationError { get; private set; }
        private bool busy;
        public bool Enabled => settings.enabled && offline.LoadError == null && ConfigurationError == null;
        public bool EffectsEnabled => Enabled && settings.effects;
        public bool InteractionsEnabled => Enabled && settings.interactions;
        public bool ControlsEnabled => Enabled && settings.mobileControls;
        public bool BadgesEnabled => Enabled && settings.badges;
        public bool EnergyEnabled => Enabled && settings.energy;
        public bool ProgressionEnabled => Enabled && settings.progression;
        public bool GiftsEnabled => Enabled && settings.gifts;
        public GameplaySettings Settings => JsonUtility.FromJson<GameplaySettings>(JsonUtility.ToJson(settings));
        public string LastError { get; private set; }
        public event Action Changed;
        private GameplayState State => offline.GameplaySnapshot;
        private double Now => (clock().ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        public GameplayService(GameplaySettings source, OfflineService offline, Func<DateTime> clock = null)
        {
            settings = JsonUtility.FromJson<GameplaySettings>(JsonUtility.ToJson(source ?? new GameplaySettings()));
            this.offline = offline ?? throw new ArgumentNullException(nameof(offline)); this.clock = clock ?? (() => DateTime.UtcNow);
            if (settings.energy && (settings.maximumEnergy < 1 || settings.energyPerRun < 1 || settings.energyPerRun > settings.maximumEnergy || !Finite(settings.energyRefillSeconds) || settings.energyRefillSeconds <= 0)) ConfigurationError = "Invalid energy configuration";
            if (settings.progression && (settings.experiencePerLevel < 1 || settings.maximumPlayerLevel < 1 || settings.experiencePerWin < 0 || settings.experiencePerLoss < 0 || settings.levelUpCoins < 0)) ConfigurationError = "Invalid progression configuration";
            if (settings.gifts) { var ids = new HashSet<string>(); foreach (var gift in settings.giftCatalog ?? Array.Empty<GiftDefinition>()) if (gift == null || string.IsNullOrWhiteSpace(gift.id) || !ids.Add(gift.id) || gift.coins < 0 || gift.energy < 0 || !Finite(gift.seconds) || !Enum.IsDefined(typeof(GiftKind), gift.kind)) ConfigurationError = "Invalid gift catalog"; }
            LastError = ConfigurationError;
        }
        internal static void Normalize(GameplayState state)
        { state.claimedGifts = state.claimedGifts ?? new List<string>(); state.returnGifts = state.returnGifts ?? new List<string>(); state.giftClocks = state.giftClocks ?? new List<GiftClock>(); }
        internal static bool Valid(GameplayState s)
        {
            Normalize(s);
            return s.energy >= 0 && s.experience >= 0 && Finite(s.energyAnchor) && Finite(s.lastVisit) && Finite(s.playSeconds) &&
                s.claimedGifts.All(x => !string.IsNullOrWhiteSpace(x)) && s.claimedGifts.Distinct().Count() == s.claimedGifts.Count &&
                s.returnGifts.All(x => !string.IsNullOrWhiteSpace(x)) && s.returnGifts.Distinct().Count() == s.returnGifts.Count &&
                s.giftClocks.All(x => x != null && !string.IsNullOrWhiteSpace(x.id) && Finite(x.next)) && s.giftClocks.Select(x => x.id).Distinct().Count() == s.giftClocks.Count;
        }
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0;
        private bool Reject(string text) { LastError = text; return false; }
        private bool Commit(Action<OfflineService.Profile> action)
        {
            if (!Enabled || busy) return Reject(offline.LoadError ?? ConfigurationError ?? "Gameplay module disabled or busy");
            busy = true;
            try {
                bool result = offline.UpdateGameplay(action); LastError = result ? null : offline.LastError;
                if (result && Changed != null) foreach (Action listener in Changed.GetInvocationList()) try { listener(); } catch (Exception error) { Debug.LogException(error); }
                return result;
            } finally { busy = false; }
        }
        public int Energy { get { var s = State; RefreshEnergy(s); return s.energy; } }
        public double RefillRemaining
        {
            get { var s = State; RefreshEnergy(s); return !EnergyEnabled || s.energy >= Capacity ? 0 : Math.Max(0, Interval - (Math.Max(Now, s.energyAnchor) - s.energyAnchor)); }
        }
        private int Capacity => Math.Max(1, settings.maximumEnergy);
        private double Interval => Finite(settings.energyRefillSeconds) && settings.energyRefillSeconds > 0 ? settings.energyRefillSeconds : 300;
        private void RefreshEnergy(GameplayState s)
        {
            double now = Math.Max(Now, s.energyAnchor);
            if (!s.energyInitialized) { s.energyInitialized = true; s.energy = Capacity; s.energyAnchor = now; return; }
            s.energy = Math.Min(Capacity, s.energy);
            if (s.energy >= Capacity) { s.energyAnchor = now; return; }
            double ticks = Math.Floor(Math.Max(0, now - s.energyAnchor) / Interval);
            if (ticks <= 0) return;
            s.energy += (int)Math.Min(Capacity - s.energy, ticks);
            s.energyAnchor = s.energy >= Capacity ? now : s.energyAnchor + ticks * Interval;
        }
        public bool CanStartRun => !EnergyEnabled || Energy >= Math.Max(1, settings.energyPerRun);
        public bool SpendRunEnergy()
        {
            if (!EnergyEnabled) return true;
            if (!CanStartRun) return Reject("Not enough energy");
            return Commit(p => { RefreshEnergy(p.gameplay); p.gameplay.energy -= Math.Max(1, settings.energyPerRun); });
        }
        public bool AddEnergy(int amount)
        {
            if (!EnergyEnabled || amount <= 0) return Reject("Energy is disabled or amount is invalid");
            return Commit(p => { RefreshEnergy(p.gameplay); p.gameplay.energy = (int)Math.Min(Capacity, (long)p.gameplay.energy + amount); });
        }
        public long Experience => State.experience;
        public int PlayerLevel => LevelFor(Experience, settings);
        public static int LevelFor(long xp, GameplaySettings settings) => (int)(1 + Math.Min((long)Math.Max(1, settings.maximumPlayerLevel) - 1, Math.Max(0, xp) / Math.Max(1, settings.experiencePerLevel)));
        internal static void AwardExperience(OfflineService.Profile p, GameplaySettings s, bool won)
        {
            if (!s.enabled || !s.progression) return;
            int before = LevelFor(p.gameplay.experience, s);
            long amount = Math.Max(0, won ? s.experiencePerWin : s.experiencePerLoss);
            p.gameplay.experience = Math.Min(p.gameplay.experience, long.MaxValue - amount) + amount;
            p.coins = checked(p.coins + (long)(LevelFor(p.gameplay.experience, s) - before) * Math.Max(0, s.levelUpCoins));
        }
        public GiftDefinition[] Gifts => GiftsEnabled ? (settings.giftCatalog ?? Array.Empty<GiftDefinition>()).Where(x => x != null).Select(x => JsonUtility.FromJson<GiftDefinition>(JsonUtility.ToJson(x))).ToArray() : Array.Empty<GiftDefinition>();
        public bool Visit()
        {
            if (!GiftsEnabled) return true;
            double now = Math.Max(Now, State.lastVisit);
            return Commit(p => {
                foreach (var gift in Gifts) if (gift.kind == GiftKind.Return && p.gameplay.lastVisit > 0 && now - p.gameplay.lastVisit >= Math.Max(1, gift.seconds) && !p.gameplay.returnGifts.Contains(gift.id)) p.gameplay.returnGifts.Add(gift.id);
                p.gameplay.lastVisit = now;
            });
        }
        public void TickPlaytime(double seconds)
        { if (GiftsEnabled && Finite(seconds)) pendingPlay = Math.Min(315360000, pendingPlay + seconds); }
        public bool FlushPlaytime()
        {
            if (pendingPlay <= 0 || !GiftsEnabled) return true;
            double captured = pendingPlay;
            bool ok = Commit(p => p.gameplay.playSeconds += captured);
            if (ok) pendingPlay -= captured; return ok;
        }
        public bool Heartbeat()
        {
            if (!GiftsEnabled) return true;
            double captured = pendingPlay, now = Now;
            bool ok = Commit(p => { p.gameplay.playSeconds += captured; p.gameplay.lastVisit = Math.Max(now, p.gameplay.lastVisit); });
            if (ok) pendingPlay -= captured; return ok;
        }
        public bool CanClaim(string id)
        {
            var gift = Gifts.FirstOrDefault(x => x.id == id); if (gift == null || string.IsNullOrWhiteSpace(id) || gift.coins < 0 || gift.energy < 0 || !Finite(gift.seconds)) return false;
            var s = State;
            switch (gift.kind)
            {
                case GiftKind.Welcome: return !s.claimedGifts.Contains(id);
                case GiftKind.Return: return s.returnGifts.Contains(id);
                case GiftKind.Playtime: return !s.claimedGifts.Contains(id) && s.playSeconds + pendingPlay >= gift.seconds;
                case GiftKind.Periodic: var stamp = s.giftClocks.FirstOrDefault(x => x.id == id); return stamp == null || Now >= stamp.next;
                default: return false;
            }
        }
        public bool Claim(string id)
        {
            if (!CanClaim(id)) return Reject("Gift is not ready");
            var gift = Gifts.First(x => x.id == id); double captured = pendingPlay, now = Now;
            bool ok = Commit(p => {
                p.coins = checked(p.coins + gift.coins);
                if (settings.energy && gift.energy > 0) { RefreshEnergy(p.gameplay); p.gameplay.energy = (int)Math.Min(Capacity, (long)p.gameplay.energy + gift.energy); }
                p.gameplay.playSeconds += captured;
                if (gift.kind == GiftKind.Periodic) { var stamp = p.gameplay.giftClocks.FirstOrDefault(x => x.id == id); if (stamp == null) { stamp = new GiftClock { id = id }; p.gameplay.giftClocks.Add(stamp); } stamp.next = now + Math.Max(1, gift.seconds); }
                else if (gift.kind == GiftKind.Return) p.gameplay.returnGifts.Remove(id);
                else p.gameplay.claimedGifts.Add(id);
            });
            if (ok) pendingPlay -= captured; return ok;
        }
    }
}
