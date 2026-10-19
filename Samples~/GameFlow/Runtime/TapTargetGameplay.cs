using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Playable prefab example. Tap the target for points; avoid the red trap.</summary>
    public sealed class TapTargetGameplay : MonoBehaviour, ILevelGameplay, IBoosterEffect
    {
        public Button target;
        public Button trap;
        private LevelContext context;
        private int taps;
        private float hintUntil;
        private Color targetColor;
        private bool hintVisible;
        public string Id => "hint";
        public void Initialize(LevelContext session)
        {
            context = session;
            targetColor = target.image.color;
            target.onClick.AddListener(Collect);
            trap.onClick.AddListener(() => context.Lose("You hit the trap. Try again!"));
        }
        private void Collect()
        {
            if (!context.IsPlaying) return;
            context.AddScore(10);
            taps++;
            // Deterministic movement keeps the trap and target apart and the example testable.
            ((RectTransform)target.transform).anchoredPosition = new Vector2(taps % 2 == 0 ? -210 : 210, 180 - (taps % 3) * 160);
            if (context.IsPlaying && taps * 10 >= context.Definition.targetScore) context.Win();
        }
        private void Update()
        {
            bool highlighted = UnityEngine.Time.time < hintUntil;
            if (target && (highlighted || hintVisible)) target.image.color = highlighted ? Color.Lerp(targetColor, Color.green, .7f) : targetColor;
            hintVisible = highlighted;
            bool enabled = context != null && context.IsPlaying;
            target.interactable = enabled; trap.interactable = enabled;
        }
        public bool CanUse(BoosterContext owner, BoosterDefinition definition) => owner.IsPlaying && owner.Level == context && target;
        public IBoosterAction Prepare(BoosterContext owner, BoosterDefinition definition) => new Hint(this, definition.amount);
        private sealed class Hint : IBoosterAction
        {
            private readonly TapTargetGameplay target; private readonly float before, after;
            public Hint(TapTargetGameplay target, float seconds) { this.target = target; before = target.hintUntil; after = UnityEngine.Time.time + Mathf.Min(60, seconds); }
            public void Apply() { if (!target || target.context == null || !target.context.IsPlaying) throw new System.InvalidOperationException("Booster session expired"); target.hintUntil = after; }
            public void Revert() { if (target) target.hintUntil = before; }
        }
    }
}
