using System;
using UnityEngine;

namespace BaseUnity.GameFlow
{
    [Serializable]
    public sealed class GameLevel
    {
        [Tooltip("Optional asset; when assigned its values replace the inline definition below.")]
        public LevelData data;
        public GameObject gameplayPrefab;
        [Tooltip("Optional Addressables prefab address. Takes precedence over the direct prefab.")]
        public string addressablePrefab;
        public string title = "Level";
        [Min(1)] public int targetScore = 30;
        [Min(0)] public float timeLimitSeconds = 60;
        [Tooltip("Optional scene path. Empty uses the shared InGame scene.")]
        public string sceneOverride;
    }

    [CreateAssetMenu(menuName = "BaseUnity/Game Flow Config")]
    public sealed class GameFlowConfig : ScriptableObject
    {
        public string homeScene;
        public string inGameScene;
        public string saveSlot = "gameflow_progress";
        public string gameTitle = "YOUR GAME";
        public GameFlowView uiPrefab;
        public OfflineMenuView offlineMenuPrefab;
        public BaseUnity.Features.FeatureConfig features;
        public bool autoWinAtTarget = true;
        public GameLevel[] levels =
        {
            new GameLevel { title = "First steps", targetScore = 30, timeLimitSeconds = 60 },
            new GameLevel { title = "Keep going", targetScore = 50, timeLimitSeconds = 60 },
            new GameLevel { title = "Final challenge", targetScore = 80, timeLimitSeconds = 60 }
        };
        public int LevelCount => levels?.Length ?? 0;
        public GameLevel GetLevel(int number)
        {
            if (number < 1 || number > LevelCount) return null;
            var entry = levels[number - 1];
            return entry != null && entry.data ? entry.data.CreateDefinition() : entry;
        }
    }
}
