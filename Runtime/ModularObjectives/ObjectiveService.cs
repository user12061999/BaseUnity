using System;
using System.Linq;
using BaseUnity.Core;
using UnityEngine;
namespace BaseUnity.EconomyModules
{
    public readonly struct GameplayProgressEvent
    {
        public readonly string eventId,receipt;
        public readonly long amount;
        public GameplayProgressEvent(string eventId,long amount,string receipt) { this.eventId=eventId;this.amount=amount;this.receipt=receipt; }
    }
    [Serializable] public sealed class ObjectiveDefinition
    {
        public string id="win_3",title="Win 3 games",eventId="win";
        public long target=3;
        public bool dailyReset;
        public EconomyReward[] rewards={new EconomyReward { amount=100 }};
    }
    public abstract class ObjectiveCatalog : ScriptableObject { public ObjectiveDefinition[] objectives=new ObjectiveDefinition[0]; }
    public sealed class ObjectiveStatus
    {
        public string id,title;
        public long progress,target;
        public bool claimed;
        public EconomyReward[] rewards;
        public bool CanClaim => progress>=target&&!claimed;
    }
    public abstract class ObjectiveService : IDisposable
    {
        private readonly EconomyStore store;
        private readonly string group;
        private readonly ObjectiveDefinition[] definitions;
        private readonly Func<long> clock;
        private readonly IDisposable subscription;
        protected ObjectiveService(EconomyStore store,string group,ObjectiveDefinition[] definitions,bool allowDaily,EventBus events=null,Func<long> utcSeconds=null)
        {
            this.store=store??throw new ArgumentNullException(nameof(store));this.group=group;clock=utcSeconds??(()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            this.definitions=(definitions??throw new ArgumentNullException(nameof(definitions))).Select(x=> {
                if(x==null)throw new ArgumentException("Null objective");EconomyStore.ValidateId(x.id);EconomyStore.ValidateId(x.eventId);
                if(x.target<=0||(!allowDaily&&x.dailyReset)||x.rewards==null||x.rewards.Length==0)throw new ArgumentException("Invalid objective");foreach(var r in x.rewards)r.Validate();
                return new ObjectiveDefinition { id=x.id,title=x.title,eventId=x.eventId,target=x.target,dailyReset=x.dailyReset,rewards=x.rewards.Select(r=>r.Copy()).ToArray() };
            }).ToArray();
            if(this.definitions.Select(x=>x.id).Distinct().Count()!=this.definitions.Length)throw new ArgumentException("Duplicate objective ID");
            if(events!=null)subscription=events.Subscribe<GameplayProgressEvent>(e=>Record(e.eventId,e.amount,e.receipt));
        }
        private long Day => Math.Max(store.Read(group+":day"),Math.Max(0,clock())/86400);
        private string Key(ObjectiveDefinition d,long day) => group+":"+d.id+(d.dailyReset?":"+day:"");
        public ObjectiveStatus[] Statuses => definitions.Select(d=> { string key=Key(d,Day);return new ObjectiveStatus { id=d.id,title=d.title,target=d.target,progress=store.Read(key+":progress"),claimed=store.Read(key+":claimed")!=0,rewards=d.rewards.Select(r=>r.Copy()).ToArray() }; }).ToArray();
        public TransactionResult Record(string eventId,long amount,string receipt)
        {
            EconomyStore.ValidateId(eventId);EconomyStore.ValidateKey(receipt);if(amount<=0)throw new ArgumentOutOfRangeException(nameof(amount));
            var matches=definitions.Where(d=>d.eventId==eventId).ToArray();if(matches.Length==0)return TransactionResult.Rejected;
            long day=Day;
            return store.Transact(group+":event:"+receipt,tx=> {
                tx.Write(group+":day",day);
                foreach(var d in matches) { string key=Key(d,day)+":progress";long previous=tx.Read(key);tx.Write(key,previous>=d.target?previous:previous+Math.Min(amount,d.target-previous)); }
                return true;
            });
        }
        public TransactionResult Claim(string id)
        {
            var d=definitions.FirstOrDefault(x=>x.id==id)??throw new ArgumentException("Unknown objective");long day=Day;string key=Key(d,day);
            return store.Transact(key+":reward",tx=> {
                if(tx.Read(key+":progress")<d.target||tx.Read(key+":claimed")!=0)return false;
                tx.Grant(d.rewards);tx.Write(key+":claimed",1);tx.Write(group+":day",day);return true;
            });
        }
        public void Dispose() { subscription?.Dispose(); }
    }
}
