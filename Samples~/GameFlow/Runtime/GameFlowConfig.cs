using UnityEngine;

namespace BaseUnity.GameFlow
{
    [CreateAssetMenu(menuName = "BaseUnity/Game Flow Config")]
    public sealed class GameFlowConfig : ScriptableObject
    {
        public string homeScene;
        public string inGameScene;
        public string saveSlot = "gameflow_progress";
        public string gameTitle = "YOUR GAME";
    }
}
