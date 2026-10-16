using System;
using UnityEngine;

namespace BaseUnity.GameFlow
{
    /// <summary>Implement on prefab components. Initialize runs before the prefab is activated.</summary>
    public interface ILevelGameplay { void Initialize(LevelContext context); }

    /// <summary>A session-scoped connection: callbacks from an old level cannot affect a new one.</summary>
    public sealed class LevelContext
    {
        private readonly LevelManager manager;
        private readonly GameFlowBootstrap flow;
        public int Number { get; }
        public GameLevel Definition { get; }
        public bool IsPlaying => manager && manager.Context == this && flow && flow.Phase == FlowPhase.Playing;
        internal LevelContext(LevelManager manager, GameFlowBootstrap flow)
        { this.manager = manager; this.flow = flow; Number = flow.CurrentLevel; Definition = flow.CurrentDefinition; }
        public void AddScore(int amount) { if (IsPlaying) flow.AddScore(amount); }
        public void Win() { if (IsPlaying) flow.WinLevel(); }
        public void Lose(string reason = "Try again!") { if (IsPlaying) flow.LoseLevel(reason); }
    }

    /// <summary>Owns a scene-local gameplay root and one instantiated level prefab.</summary>
    public sealed class LevelManager : MonoBehaviour
    {
        public GameObject Instance { get; private set; }
        public Transform GameplayRoot { get; private set; }
        public LevelContext Context { get; private set; }
        private GameFlowBootstrap flow;
        internal void Initialize(GameFlowBootstrap owner) { flow = owner; }
        public void StartLevel(int number) => flow.StartLevel(number);
        public void RetryLevel() => flow.RetryLevel();
        public void NextLevel() => flow.NextLevel();

        internal void LoadGameplay()
        {
            Clear();
            var root = new GameObject("Level Gameplay Root");
            root.SetActive(false);
            GameplayRoot = root.transform; // In the loaded scene, not the persistent bootstrap hierarchy.
            Context = new LevelContext(this, flow);
            try
            {
                var prefab = flow.CurrentDefinition?.gameplayPrefab;
                if (prefab)
                {
                    Instance = Instantiate(prefab, GameplayRoot);
                    foreach (var behaviour in Instance.GetComponentsInChildren<MonoBehaviour>(true))
                        if (behaviour is ILevelGameplay gameplay) gameplay.Initialize(Context);
                }
                root.SetActive(true);
            }
            catch { Clear(); throw; }
        }
        internal void Clear()
        {
            Context = null;
            Instance = null;
            if (GameplayRoot)
            {
                GameplayRoot.gameObject.SetActive(false);
                Destroy(GameplayRoot.gameObject);
            }
            GameplayRoot = null;
        }
        private void OnDestroy() => Clear();
    }
}
