using System;
using System.Collections.Generic;
using BaseUnity.Features;
using UnityEngine;
namespace BaseUnity.GameFlow
{
    public interface IBoosterInventory { long Count(string id); bool Consume(string id); string LastError { get; } }
    public sealed class OfflineBoosterInventory : IBoosterInventory
    {
        private readonly OfflineService economy;
        public OfflineBoosterInventory(OfflineService economy) { this.economy = economy; }
        public long Count(string id) => economy.ItemCount(id);
        public bool Consume(string id) => economy.ConsumeItem(id);
        public string LastError => economy.LastError;
    }
    /// <summary>Prepared actions change reversible transient gameplay only. Never save, win, grant rewards or change scenes.</summary>
    public interface IBoosterAction { void Apply(); void Revert(); }
    public interface IBoosterEffect
    {
        string Id { get; }
        bool CanUse(BoosterContext context, BoosterDefinition definition);
        IBoosterAction Prepare(BoosterContext context, BoosterDefinition definition);
    }
    public sealed class BoosterContext
    {
        public GameFlowBootstrap Flow { get; }
        public LevelContext Level { get; }
        public bool IsCurrent => Flow && Flow.IsReady && Flow.Levels.Context == Level;
        public bool IsPlaying => IsCurrent && Flow.Phase == FlowPhase.Playing;
        public BoosterContext(GameFlowBootstrap flow) { Flow = flow; Level = flow.Levels.Context; }
    }
    public enum BoosterAvailability { Ready, Empty, Cooldown, LimitReached, Unavailable }
    /// <summary>Main thread, synchronous activation: reversible effect -> durable debit -> usage/cooldown.</summary>
    public sealed class BoosterController
    {
        private sealed class Usage { public int count; public float readyAt; }
        private readonly GameFlowBootstrap flow;
        private readonly IBoosterInventory inventory;
        private readonly Dictionary<string, IBoosterEffect> effects = new Dictionary<string, IBoosterEffect>(StringComparer.Ordinal);
        private readonly Dictionary<string, Usage> usage = new Dictionary<string, Usage>(StringComparer.Ordinal);
        private LevelContext session;
        private bool busy;
        public string LastError { get; private set; }
        public BoosterController(GameFlowBootstrap flow, IBoosterInventory inventory)
        { if (!flow) throw new ArgumentNullException(nameof(flow)); this.flow = flow; this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory)); Register(new ExtraTimeBoosterEffect()); }
        public void Register(IBoosterEffect effect)
        { if (effect == null || string.IsNullOrWhiteSpace(effect.Id)) throw new ArgumentException("Booster effect ID is required."); effects[effect.Id] = effect; }
        private void Session()
        { if (session == flow.Levels.Context) return; session = flow.Levels.Context; usage.Clear(); }
        public float Cooldown(BoosterDefinition definition)
        { Session(); return usage.TryGetValue(definition.itemId, out var value) ? Mathf.Max(0, value.readyAt - UnityEngine.Time.time) : 0; }
        public BoosterAvailability Check(BoosterDefinition definition)
        {
            if (!flow || !flow.IsReady || !flow.Levels) return BoosterAvailability.Unavailable;
            Session();
            if (busy || flow.Phase != FlowPhase.Playing || session == null || definition == null || string.IsNullOrWhiteSpace(definition.itemId) || string.IsNullOrWhiteSpace(definition.effectId) ||
                !Finite(definition.amount) || definition.amount <= 0 || !Finite(definition.cooldownSeconds) || definition.cooldownSeconds < 0 || definition.maxUsesPerLevel < 0 ||
                !effects.TryGetValue(definition.effectId, out var effect)) return BoosterAvailability.Unavailable;
            try { if (!effect.CanUse(new BoosterContext(flow), definition)) return BoosterAvailability.Unavailable; }
            catch (Exception error) { LastError = error.Message; return BoosterAvailability.Unavailable; }
            if (usage.TryGetValue(definition.itemId, out var value))
            {
                if (definition.maxUsesPerLevel > 0 && value.count >= definition.maxUsesPerLevel) return BoosterAvailability.LimitReached;
                if (UnityEngine.Time.time < value.readyAt) return BoosterAvailability.Cooldown;
            }
            return inventory.Count(definition.itemId) > 0 ? BoosterAvailability.Ready : BoosterAvailability.Empty;
        }
        public bool TryUse(BoosterDefinition definition)
        {
            if (Check(definition) != BoosterAvailability.Ready) { LastError = "Booster is not ready"; return false; }
            busy = true; IBoosterAction action = null; bool applied = false;
            try
            {
                var context = new BoosterContext(flow); action = effects[definition.effectId].Prepare(context, definition);
                if (action == null || !context.IsPlaying) throw new InvalidOperationException("Booster is unavailable");
                applied = true; action.Apply();
                if (!context.IsPlaying) throw new InvalidOperationException("Booster changed the game session before saving");
                if (!inventory.Consume(definition.itemId)) throw new InvalidOperationException(inventory.LastError ?? "Could not save booster use");
                if (!usage.TryGetValue(definition.itemId, out var value)) usage[definition.itemId] = value = new Usage();
                value.count = Math.Min(int.MaxValue - 1, value.count) + 1; value.readyAt = UnityEngine.Time.time + definition.cooldownSeconds;
                LastError = null; return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                if (applied) try { action.Revert(); } catch (Exception revert) { LastError += "; rollback failed: " + revert.Message; }
                return false;
            }
            finally { busy = false; }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    public sealed class ExtraTimeBoosterEffect : IBoosterEffect
    {
        public string Id => "extra_time";
        public bool CanUse(BoosterContext context, BoosterDefinition definition) => context.IsPlaying && context.Flow.HasTimeLimit && context.Flow.RemainingTime > 0 && context.Flow.RemainingTime < 86400;
        public IBoosterAction Prepare(BoosterContext context, BoosterDefinition definition) => new Operation(context, definition.amount);
        private sealed class Operation : IBoosterAction
        {
            private readonly BoosterContext context; private readonly float before, after;
            public Operation(BoosterContext context, float seconds) { this.context = context; before = context.Flow.RemainingTime; after = Mathf.Min(86400, before + seconds); }
            public void Apply() { if (!context.IsPlaying) throw new InvalidOperationException("Booster session expired"); context.Flow.SetBoosterTime(after); }
            public void Revert() { if (context.IsCurrent) context.Flow.SetBoosterTime(before); }
        }
    }
}
