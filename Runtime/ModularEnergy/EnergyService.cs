using System;
using BaseUnity.EconomyModules;
namespace BaseUnity.Energy
{
    /// <summary>Offline UTC regeneration. Reads are projected; only initialization and mutations write a save.</summary>
    public sealed class EnergyService
    {
        private readonly EconomyStore store;
        private readonly Func<long> clock;
        private readonly string prefix;
        private readonly int initial,interval;
        public int Capacity { get; }
        private struct Snapshot { public int value;public long anchor; }
        public EnergyService(EconomyStore store,EnergyConfig config,Func<long> utcSeconds=null)
        {
            this.store=store??throw new ArgumentNullException(nameof(store));if(!config)throw new ArgumentNullException(nameof(config));config.Validate();
            prefix="energy:"+config.id+":";Capacity=config.capacity;initial=config.initialEnergy;interval=config.refillSeconds;clock=utcSeconds??(()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if(store.Read(prefix+"initialized")==0) {
                long now=Now;
                store.Transact(prefix+"init",tx=> { tx.Write(prefix+"initialized",1);tx.Write(prefix+"value",initial);tx.Write(prefix+"anchor",now);return true; });
                if(store.Read(prefix+"initialized")==0)throw new InvalidOperationException("Energy initialization marker is inconsistent");
            }
        }
        private long Now => Math.Max(0,clock());
        private Snapshot Project(Func<string,long> read,long now)
        {
            int value=(int)Math.Min(Capacity,read(prefix+"value"));long anchor=read(prefix+"anchor");long effective=Math.Max(now,anchor);
            if(value>=Capacity)return new Snapshot { value=Capacity,anchor=effective };
            long ticks=(effective-anchor)/interval;
            if(ticks>=Capacity-value)return new Snapshot { value=Capacity,anchor=effective };
            return new Snapshot { value=value+(int)ticks,anchor=anchor+ticks*interval };
        }
        public int Current => Project(store.Read,Now).value;
        public long RemainingSeconds {
            get { long now=Now;var value=Project(store.Read,now);if(value.value>=Capacity)return 0;long wait=value.anchor>now?value.anchor-now:0;return wait>long.MaxValue-interval?long.MaxValue:wait+interval-(now>=value.anchor?(now-value.anchor)%interval:0); }
        }
        private void Write(EconomyTransaction tx,Snapshot value) { tx.Write(prefix+"initialized",1);tx.Write(prefix+"value",value.value);tx.Write(prefix+"anchor",value.anchor); }
        public bool ConsumeInto(EconomyTransaction tx,int amount)
        {
            if(tx==null)throw new ArgumentNullException(nameof(tx));if(amount<1)throw new ArgumentOutOfRangeException(nameof(amount));
            var value=Project(tx.Read,Now);if(value.value<amount)return false;value.value-=amount;Write(tx,value);return true;
        }
        public bool GrantInto(EconomyTransaction tx,int amount)
        {
            if(tx==null)throw new ArgumentNullException(nameof(tx));if(amount<1)throw new ArgumentOutOfRangeException(nameof(amount));
            var value=Project(tx.Read,Now);if(value.value>=Capacity)return false;value.value=(int)Math.Min(Capacity,(long)value.value+amount);if(value.value==Capacity)value.anchor=Math.Max(Now,value.anchor);Write(tx,value);return true;
        }
        public TransactionResult Consume(int amount,string receipt) { EconomyStore.ValidateKey(receipt);if(amount<1)throw new ArgumentOutOfRangeException(nameof(amount));return store.Transact(prefix+"consume:"+receipt,tx=>ConsumeInto(tx,amount)); }
        public TransactionResult Grant(int amount,string receipt) { EconomyStore.ValidateKey(receipt);if(amount<1)throw new ArgumentOutOfRangeException(nameof(amount));return store.Transact(prefix+"grant:"+receipt,tx=>GrantInto(tx,amount)); }
    }
}
