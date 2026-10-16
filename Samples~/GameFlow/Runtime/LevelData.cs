using UnityEngine;

namespace BaseUnity.GameFlow
{
    [CreateAssetMenu(menuName = "BaseUnity/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        public string title = "Level";
        [Min(1)] public int targetScore = 30;
        [Min(0)] public float timeLimitSeconds = 60;
        [Tooltip("Leave empty to use the shared InGame scene.")]
        public string sceneOverride;
        public GameObject gameplayPrefab;

        public GameLevel CreateDefinition() => new GameLevel
        {
            title = title, targetScore = targetScore, timeLimitSeconds = timeLimitSeconds,
            sceneOverride = sceneOverride, gameplayPrefab = gameplayPrefab
        };
    }
}
