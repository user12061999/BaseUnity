using System;
using System.Linq;
using System.Threading.Tasks;
using BaseUnity.Core;
using BaseUnity.Flow;
using BaseUnity.EconomyModules;
using BaseUnity.Energy;
using BaseUnity.Progression;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.Demo
{
    /// <summary>Optional sample only: tap targets before the timer expires. Uses the same services as the Home popups.</summary>
    public sealed class PlayableDemoController : MonoBehaviour
    {
        public PlayableDemoConfig config;
        public RectTransform board,target;
        public Text scoreLabel,timeLabel,statusLabel,winSummary,loseSummary;
        public Button refillButton;
        private GameFlowService flow;
        private EconomyStore store;
        private EnergyService energy;
        private PlayerProgressionService progression;
        private bool ready,busy,paid;
        private string activeRun;
        private int score,goal,cost,firstTarget,step,refillAmount;
        private float remaining,seconds,nextRefresh;
        private long beforeCoins,beforeXp,refillPrice;
        private void OnEnable() { Bind(); }
        private void Bind()
        {
            if(ready)return;var root=ModuleBootstrap.Instance;if(!root||!root.IsReady)return;
            try {
                if(!config||!board||!target)throw new InvalidOperationException("Assign demo config, board and target");config.Validate();
                flow=root.Services.Get<GameFlowService>();store=root.Services.Get<EconomyStore>();energy=root.Services.Get<EnergyService>();progression=root.Services.Get<PlayerProgressionService>();
                cost=config.energyPerRun;firstTarget=config.firstTargetCount;step=config.additionalTargetsPerLevel;seconds=config.roundSeconds;refillAmount=config.refillAmount;refillPrice=config.refillCoinPrice;
                if(cost>energy.Capacity)throw new InvalidOperationException("Run energy cost exceeds capacity");
                if(flow.Phase==GamePhase.Bootstrap||flow.Phase==GamePhase.Home) {
                    long highest=store.Keys("flow:").Where(k=>k.EndsWith(":level",StringComparison.Ordinal)&&store.Read(k.Substring(0,k.Length-6)+":won")==1).Select(store.Read).DefaultIfEmpty(0).Max();
                    int level=(int)Math.Min(flow.LevelCount,Math.Max(store.Read("demo:level"),highest>=flow.LevelCount?flow.LevelCount:highest+1));
                    flow.RestoreLevel(Math.Max(1,level));
                }
                ready=true;flow.Changed+=FlowChanged;store.Changed+=Refresh;FlowChanged();
            } catch(Exception error) { if(statusLabel)statusLabel.text=error.Message;Debug.LogException(error,this);enabled=false; }
        }
        private void OnDisable() { if(flow!=null)flow.Changed-=FlowChanged;if(store!=null)store.Changed-=Refresh;ready=false; }
        private void Update()
        {
            if(!ready) { Bind();return; }
            if(flow.CanPlay&&paid) {
                remaining=Math.Max(0,remaining-UnityEngine.Time.deltaTime);
                if(timeLabel)timeLabel.text="Time: "+Math.Ceiling(remaining)+"s";
                if(remaining<=0)flow.Lose();
            }
            if(UnityEngine.Time.unscaledTime>=nextRefresh) { nextRefresh=UnityEngine.Time.unscaledTime+.5f;Refresh(); }
        }
        private void FlowChanged()
        {
            if(flow.Phase==GamePhase.Playing||flow.Phase==GamePhase.Paused) {
                if(activeRun!=flow.RunId) {
                    activeRun=flow.RunId;score=0;remaining=seconds;goal=(int)Math.Min(10000,(long)firstTarget+(long)(flow.Level-1)*step);paid=false;
                    if(statusLabel)statusLabel.text="";
                }
                if(flow.Phase==GamePhase.Playing) { if(!paid&&!PayEntry()) { flow.Pause();return; }MoveTarget(); }
            }
            Refresh();
        }
        private bool PayEntry()
        {
            try {
                var result=store.Transact("demo.entry:"+activeRun,tx=> { if(!energy.ConsumeInto(tx,cost))return false;tx.Write("demo:level",flow.Level);return true; });
                if(result==TransactionResult.Rejected) { if(statusLabel)statusLabel.text="Not enough energy";return false; }
                paid=true;beforeCoins=store.Balance("coin");beforeXp=progression.TotalExperience;return true;
            } catch(Exception error) { if(statusLabel)statusLabel.text="Could not save. Please retry.";Debug.LogException(error,this);return false; }
        }
        private void Refresh()
        {
            if(!ready)return;
            if(scoreLabel)scoreLabel.text="Targets: "+score+" / "+Math.Max(1,goal);
            if(refillButton) {
                refillButton.interactable=flow.Phase==GamePhase.Home&&energy.Current<energy.Capacity&&store.Balance("coin")>=refillPrice;
                var label=refillButton.GetComponentInChildren<Text>();if(label)label.text="Refill "+refillPrice+" coin";
            }
            if(flow.Phase==GamePhase.Won||flow.Phase==GamePhase.Lost) {
                string text="Coins gained: "+Math.Max(0,store.Balance("coin")-beforeCoins)+"  EXP: "+Math.Max(0,progression.TotalExperience-beforeXp);
                if(winSummary)winSummary.text=text;if(loseSummary)loseSummary.text=text;
            }
        }
        private void MoveTarget()
        {
            if(!board||!target)return;
            float x=Math.Max(0,(board.rect.width-target.rect.width)*.5f-12),y=Math.Max(0,(board.rect.height-target.rect.height)*.5f-12);
            target.anchoredPosition=new Vector2(UnityEngine.Random.Range(-x,x),UnityEngine.Random.Range(-y,y));
        }
        public void HitTarget()
        {
            if(!ready||!paid||!flow.CanPlay)return;score++;Refresh();if(score>=goal)flow.Win();else MoveTarget();
        }
        private async void StartRun(Func<Task<bool>> command)
        {
            if(!ready||busy)return;
            if(!flow.CanLeaveResult) { if(statusLabel)statusLabel.text="Saving rewards... retrying";return; }
            if(energy.Current<cost) { if(statusLabel)statusLabel.text="Not enough energy";return; }
            busy=true;try { await command(); }catch(Exception error) { if(statusLabel)statusLabel.text=error.Message;Debug.LogException(error); }finally { busy=false; }
        }
        public void Play() { if(ready)StartRun(flow.PlayAsync); }
        public void Retry() { if(ready)StartRun(flow.RetryAsync); }
        public void Next() { if(ready)StartRun(flow.NextAsync); }
        public void Resume() { if(ready&&flow.Phase==GamePhase.Paused&&(paid||PayEntry()))flow.Resume(); }
        public async void Home() { if(ready&&!busy)try { await flow.HomeAsync(); }catch(Exception error) { Debug.LogException(error); } }
        public void BuyEnergy()
        {
            if(!ready||flow.Phase!=GamePhase.Home)return;
            try {
                var result=store.Transact("demo.refill:"+Guid.NewGuid().ToString("N"),tx=> {
                    if(!energy.GrantInto(tx,refillAmount))return false;return tx.Spend("coin",refillPrice);
                });
                if(statusLabel)statusLabel.text=result==TransactionResult.Success?"Energy received":"Not available";
            } catch(Exception error) { if(statusLabel)statusLabel.text="Could not save. Please retry.";Debug.LogException(error,this); }
        }
    }
}
