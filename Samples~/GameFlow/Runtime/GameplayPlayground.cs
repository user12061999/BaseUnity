using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class GameplayPlayground : MonoBehaviour, ILevelGameplay
    {
        public GameplayInteractable tap, collectible, draggable;
        public GameplayDropZone dropZone;
        public MobileSwipeSurface swipe;
        public Text status;
        public RectTransform actor;
        private LevelContext context;
        private GameplayEffects effects;
        private Vector2 original;
        public void Initialize(LevelContext value)
        {
            context = value; original = actor.anchoredPosition;
            effects = FindFirstObjectByType<GameplayEffects>();
            tap.interacted.AddListener(() => { var hit = tap.GetComponent<GameplayHitFeedback>(); if (hit) hit.Play(); Score(10, tap.transform); });
            collectible.collected.AddListener(() => Score(20, collectible.transform));
            dropZone.accepted.AddListener(item => { Score(15, item.transform); item.SetActive(false); });
            swipe.swiped.AddListener(direction => { if (context.IsPlaying) status.text = "SWIPE " + direction; });
        }
        private void Score(int amount, Transform source)
        {
            if (!context.IsPlaying) return;
            if (effects) effects.Show("+" + amount, RectTransformUtility.WorldToScreenPoint(null, source.position), Color.yellow);
            context.AddScore(amount);
        }
        private void Update()
        {
            if (context == null || !context.IsPlaying) return;
            var input = BaseUnity.Core.GameBootstrap.Instance.Services.Get<BaseUnity.Input.InputRouter>();
            actor.anchoredPosition = Vector2.ClampMagnitude(actor.anchoredPosition - original + input.ReadVector2("Move") * UnityEngine.Time.deltaTime * 220, 160) + original;
            if (input.WasPressedThisFrame("Fire")) Score(5, actor);
        }
    }
}
