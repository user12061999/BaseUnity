using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Playable prefab example. Tap the target for points; avoid the red trap.</summary>
    public sealed class TapTargetGameplay : MonoBehaviour, ILevelGameplay
    {
        public Button target;
        public Button trap;
        private LevelContext context;
        private int taps;
        public void Initialize(LevelContext session)
        {
            context = session;
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
            bool enabled = context != null && context.IsPlaying;
            target.interactable = enabled; trap.interactable = enabled;
        }
    }
}
