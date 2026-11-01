using System;
using System.Linq;
using UnityEngine;
namespace BaseUnity.EconomyModules
{
    [Serializable] public sealed class DailyRewardDay { public EconomyReward[] rewards={new EconomyReward { amount=50 }}; }
    public sealed class DailyRewardService
    {
        private readonly EconomyStore store;
        private readonly Func<long> clock;
        private readonly string prefix;
        private readonly int cooldown;
        private readonly EconomyReward[][] rewards;
        public DailyRewardService(EconomyStore store,DailyRewardSchedule schedule,Func<long> utcSeconds=null)
        {
            this.store=store??throw new ArgumentNullException(nameof(store));if(!schedule)throw new ArgumentNullException(nameof(schedule));
            EconomyStore.ValidateId(schedule.id);if(schedule.cooldownSeconds<1||schedule.days==null||schedule.days.Length==0)throw new ArgumentException("Empty/invalid daily schedule");
            prefix="daily:"+schedule.id+":";cooldown=schedule.cooldownSeconds;clock=utcSeconds??(()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            rewards=schedule.days.Select(d=> { if(d==null||d.rewards==null||d.rewards.Length==0)throw new ArgumentException("Empty day reward");foreach(var r in d.rewards)r.Validate();return d.rewards.Select(r=>r.Copy()).ToArray(); }).ToArray();
        }
        public long ClaimCount => store.Read(prefix+"count");
        public int DayCount => rewards.Length;
        public int NextDay => (int)(ClaimCount%rewards.Length);
        public EconomyReward[] RewardsAt(int day) => rewards[day].Select(x=>x.Copy()).ToArray();
        public long RemainingSeconds {
            get { if(ClaimCount==0)return 0;long now=Math.Max(0,clock());long last=store.Read(prefix+"last");return now<last?cooldown:Math.Max(0,cooldown-Math.Min(cooldown,now-last)); }
        }
        public bool CanClaim => RemainingSeconds==0;
        public TransactionResult Claim()
        {
            long count=ClaimCount;long now=Math.Max(0,clock());
            return store.Transact(prefix+"claim:"+count,tx=> {
                if(tx.Read(prefix+"count")!=count)return false;
                long last=tx.Read(prefix+"last");if(count>0&&(now<last||now-last<cooldown))return false;
                tx.Grant(rewards[(int)(count%rewards.Length)]);tx.Write(prefix+"count",checked(count+1));tx.Write(prefix+"last",now);return true;
            });
        }
    }
}
