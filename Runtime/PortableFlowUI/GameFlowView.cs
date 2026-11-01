using BaseUnity.Core;
using BaseUnity.Flow;
using BaseUnity.UI;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.FlowUI
{
    /// <summary>Optional bridge. Keep this component on an always-active UI root.</summary>
    public sealed class GameFlowView : MonoBehaviour
    {
        public UIPanel home,ingame,loading,pause,win,lose;
        public Text levelLabel,resultStatus;
        public Button nextButton;
        private GameFlowService flow;
        private UIService ui;
        private void OnEnable() { Bind(); }
        private void Update() { if(flow==null)Bind(); }
        private void Bind()
        {
            var root=ModuleBootstrap.Instance;
            if(!root||!root.IsReady||!root.Services.TryGet<GameFlowService>(out flow))return;
            if(!root.Services.TryGet<UIService>(out ui)) { flow=null;return; }
            flow.Changed+=Refresh; Refresh();
        }
        private void OnDisable() { if(flow!=null)flow.Changed-=Refresh; flow=null;ui=null; }
        private void Refresh()
        {
            if(levelLabel)levelLabel.text="Level "+flow.Level+" / "+flow.LevelCount;
            if(nextButton)nextButton.interactable=flow.CanNext;
            if(resultStatus)resultStatus.text=flow.CanLeaveResult?"":"Saving rewards... retrying";
            var phase=flow.Phase;
            if(phase==GamePhase.Home) { if(home)ui.ShowScreen(home); }
            else if(phase==GamePhase.Loading||phase==GamePhase.Bootstrap) { if(loading)ui.ShowScreen(loading); }
            else {
                if(ingame)ui.ShowScreen(ingame);
                var popup=phase==GamePhase.Paused?pause:phase==GamePhase.Won?win:phase==GamePhase.Lost?lose:null;
                if(popup)ui.PushPopup(popup);
            }
        }
    }
}
