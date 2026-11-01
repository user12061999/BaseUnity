using System;
using System.Collections.Generic;
using System.Linq;
using BaseUnity.Save;
using UnityEngine;
namespace BaseUnity.EconomyModules
{
    [Serializable] public sealed class EconomyEntry { public string key; public long value; }
    [Serializable] public sealed class EconomyState
    {
        public List<EconomyEntry> values=new List<EconomyEntry>();
        public List<string> receipts=new List<string>();
    }
    public enum TransactionResult { Success, Duplicate, Rejected }
    public enum RewardKind { Currency, Item }
    [Serializable] public sealed class EconomyReward
    {
        public RewardKind kind;
        public string id="coin";
        public long amount=1;
        public void Validate() { EconomyStore.ValidateId(id); if(amount<=0||!Enum.IsDefined(typeof(RewardKind),kind))throw new ArgumentException("Invalid reward"); }
        public EconomyReward Copy() => new EconomyReward { kind=kind,id=id,amount=amount };
    }
    /// <summary>A transaction is a private working copy; retaining it cannot mutate committed state.</summary>
    public sealed class EconomyTransaction
    {
        private readonly EconomyState state;
        internal EconomyTransaction(EconomyState state) { this.state=state; }
        public long Read(string key) { EconomyStore.ValidateKey(key);return state.values.FirstOrDefault(x=>x.key==key)?.value??0; }
        public void Write(string key,long value)
        {
            EconomyStore.ValidateKey(key);if(value<0)throw new ArgumentOutOfRangeException(nameof(value));
            var entry=state.values.FirstOrDefault(x=>x.key==key);if(entry==null)state.values.Add(new EconomyEntry { key=key,value=value });else entry.value=value;
        }
        public long Balance(string currency) { EconomyStore.ValidateId(currency);return Read("money:"+currency); }
        public long Owned(string item) { EconomyStore.ValidateId(item);return Read("item:"+item); }
        public bool Spend(string currency,long amount)
        { if(amount<0)throw new ArgumentOutOfRangeException(nameof(amount));long current=Balance(currency);if(current<amount)return false;Write("money:"+currency,current-amount);return true; }
        public void Grant(IEnumerable<EconomyReward> rewards)
        {
            if(rewards==null)throw new ArgumentNullException(nameof(rewards));
            foreach(var reward in rewards) { reward.Validate();string key=(reward.kind==RewardKind.Currency?"money:":"item:")+reward.id;Write(key,checked(Read(key)+reward.amount)); }
        }
    }
    /// <summary>Main thread only. Currency, items, progress and receipts are committed in one save.</summary>
    public sealed class EconomyStore : IDisposable
    {
        private readonly ISaveStore saves;
        private readonly string slot;
        private EconomyState state;
        private bool busy,disposed;
        public event Action Changed;
        public event Action<Exception> ObserverError;
        public EconomyStore(ISaveStore saves,string slot="baseunity_modular_economy")
        {
            this.saves=saves??throw new ArgumentNullException(nameof(saves));this.slot=slot;
            var json=saves as JsonSaveStore;
            if(json!=null) { if(json.LoadVersioned(slot,out state,1,Valid)==SaveLoadState.Missing)state=new EconomyState(); }
            else if(!saves.TryLoad(slot,out state))state=new EconomyState();
            if(!Valid(state))throw new InvalidOperationException("Invalid economy save; refusing to overwrite");
        }
        private static EconomyState Copy(EconomyState source) => new EconomyState {
            values=source.values.Select(x=>new EconomyEntry { key=x.key,value=x.value }).ToList(),receipts=new List<string>(source.receipts) };
        private static bool Valid(EconomyState value) => value!=null&&value.values!=null&&value.receipts!=null&&
            value.values.All(x=>x!=null&&Safe(x.key,512)&&x.value>=0)&&value.values.Select(x=>x.key).Distinct().Count()==value.values.Count&&
            value.receipts.All(x=>Safe(x,512))&&value.receipts.Distinct().Count()==value.receipts.Count;
        private static bool Safe(string id,int length) => !string.IsNullOrWhiteSpace(id)&&id.Length<=length&&!id.Any(char.IsControl);
        public static void ValidateId(string id) { if(!Safe(id,64)||id.Any(c=>!(char.IsLetterOrDigit(c)||c=='_'||c=='-')))throw new ArgumentException("ID: 1-64 letters, digits, underscore or hyphen"); }
        public static void ValidateKey(string key) { if(!Safe(key,512))throw new ArgumentException("Invalid transaction key/receipt"); }
        public long Read(string key) { ValidateKey(key);return state.values.FirstOrDefault(x=>x.key==key)?.value??0; }
        public long Balance(string currency) { ValidateId(currency);return Read("money:"+currency); }
        public long Owned(string item) { ValidateId(item);return Read("item:"+item); }
        public bool HasReceipt(string receipt) { ValidateKey(receipt);return state.receipts.Contains(receipt); }
        public TransactionResult Spend(string currency,long amount,string receipt)
        { if(amount<=0)throw new ArgumentOutOfRangeException(nameof(amount));return Transact(receipt,tx=>tx.Spend(currency,amount)); }
        public TransactionResult Grant(string receipt,params EconomyReward[] rewards) => Transact(receipt,tx=> { tx.Grant(rewards);return true; });
        public TransactionResult Transact(string receipt,Func<EconomyTransaction,bool> change)
        {
            if(disposed)throw new ObjectDisposedException(nameof(EconomyStore));ValidateKey(receipt);
            if(change==null)throw new ArgumentNullException(nameof(change));if(busy)return TransactionResult.Rejected;
            if(HasReceipt(receipt))return TransactionResult.Duplicate;
            busy=true;
            try {
                var next=Copy(state);
                try { if(!change(new EconomyTransaction(next)))return TransactionResult.Rejected; }
                catch(OverflowException) { return TransactionResult.Rejected; }
                next.receipts.Add(receipt);
                // Snapshot again so a callback retaining its transaction cannot edit committed state.
                var committed=Copy(next);saves.Save(slot,committed,1);state=Copy(committed);
            } finally { busy=false; }
            if(Changed!=null)foreach(Action observer in Changed.GetInvocationList())try { observer(); }catch(Exception error) { try { ObserverError?.Invoke(error); }catch { } }
            return TransactionResult.Success;
        }
        public void Dispose() { disposed=true;Changed=null;ObserverError=null; }
    }
}
