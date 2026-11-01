using System;
using System.Collections.Generic;
using System.Linq;
using BaseUnity.Core;
using BaseUnity.Flow;
using BaseUnity.EconomyModules;
namespace BaseUnity.FlowEconomy
{
    public sealed class FlowEconomyBridge : IDisposable
    {
        private readonly GameFlowService flow;
        private readonly EconomyStore store;
        private readonly EventBus events;
        private readonly EconomyReward[] win,lose;
        private readonly bool progress;
        private readonly Dictionary<string,GameOutcome> memory=new Dictionary<string,GameOutcome>();
        private readonly IDisposable guard;
        private bool busy,disposed;
        public string LastError { get; private set; }
        public bool HasPending => memory.Count>0||store.Keys("flow:").Any(k=>k.EndsWith(":pending",StringComparison.Ordinal)&&store.Read(k)>0);
        public FlowEconomyBridge(GameFlowService flow,EconomyStore store,EventBus events,FlowRewardConfig config)
        {
            this.flow=flow??throw new ArgumentNullException(nameof(flow));this.store=store??throw new ArgumentNullException(nameof(store));this.events=events??throw new ArgumentNullException(nameof(events));
            if(!config)throw new ArgumentNullException(nameof(config));
            win=Clone(config.winRewards);lose=Clone(config.loseRewards);progress=config.publishProgress;
            guard=flow.GuardResults(()=>!HasPending);flow.Completed+=Complete;
        }
        private static EconomyReward[] Clone(EconomyReward[] rewards)
        { if(rewards==null)throw new ArgumentException("Reward array is required");foreach(var r in rewards) { if(r==null)throw new ArgumentException("Null reward");r.Validate(); }return rewards.Select(r=>r.Copy()).ToArray(); }
        private void Complete(GameOutcome outcome) { memory[outcome.runId]=outcome;RetryPending(); }
        public bool RetryPending()
        {
            if(disposed||busy)return false;busy=true;bool success=false;
            try {
                foreach(var outcome in memory.Values.ToArray()) {
                    string key="flow:"+outcome.runId;
                    var result=store.Transact(key+":reward",tx=> {
                        tx.Grant(outcome.won?win:lose);tx.Write(key+":pending",1);tx.Write(key+":won",outcome.won?1:0);tx.Write(key+":level",outcome.level);tx.Write(key+":progress",progress?1:0);return true;
                    });
                    if(result==TransactionResult.Rejected)throw new InvalidOperationException("Outcome transaction rejected");memory.Remove(outcome.runId);
                }
                foreach(string pending in store.Keys("flow:").Where(k=>k.EndsWith(":pending",StringComparison.Ordinal)&&store.Read(k)>0)) {
                    string key=pending.Substring(0,pending.Length-8);
                    if(store.Read(key+":progress")!=0) {
                        events.Publish(new GameplayProgressEvent("play",1,key+":play"));
                        events.Publish(new GameplayProgressEvent(store.Read(key+":won")==1?"win":"lose",1,key+":result"));
                    }
                    var result=store.Transact(key+":delivered",tx=> { tx.Write(pending,0);return true; });
                    if(result==TransactionResult.Rejected)throw new InvalidOperationException("Outcome acknowledgement rejected");
                }
                LastError=null;success=!HasPending;return success;
            } catch(Exception error) { LastError=error.Message;return false; }
            finally { busy=false;if(success)flow.RefreshNavigation(); }
        }
        public void Dispose() { if(disposed)return;disposed=true;flow.Completed-=Complete;guard.Dispose(); }
    }
}
