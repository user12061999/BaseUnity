using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    /// <summary>Editable prefab shown only during preparation, countdown and ending.</summary>
    public sealed class SessionOverlayView : MonoBehaviour
    {
        public GameObject content;
        public Text heading, value;
        public string preparingText = "GET READY", countdownText = "STARTING IN", finishingText = "LEVEL COMPLETE", losingText = "TRY AGAIN";
        private GameFlowBootstrap flow;
        public void Initialize(GameFlowBootstrap owner) { flow = owner; flow.Changed += Refresh; Refresh(); }
        private void Update() { if (flow && flow.IsSessionTransition) UpdateText(); }
        private void Refresh() { if (!flow) return; content.SetActive(flow.IsSessionTransition); UpdateText(); }
        private void UpdateText()
        {
            if (heading) heading.text = flow.Phase == FlowPhase.Preparing ? preparingText : flow.Phase == FlowPhase.Countdown ? countdownText : flow.Outcome == LevelOutcome.Win ? finishingText : losingText;
            if (value) value.text = flow.Phase == FlowPhase.Countdown ? Mathf.Max(1, Mathf.CeilToInt(flow.StageRemaining)).ToString() : "";
        }
        private void OnDestroy() { if (flow) flow.Changed -= Refresh; }
    }
}
