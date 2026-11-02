using System;
using System.Linq;
using BaseUnity.EconomyModules;
namespace BaseUnity.Progression
{
    /// <summary>XP and level rewards share one atomic EconomyStore transaction.</summary>
    public sealed class PlayerProgressionService
    {
        private readonly EconomyStore store;
        private readonly string prefix;
        private readonly long perLevel,cap;
        private readonly EconomyReward[] rewards;
        public int MaximumLevel { get; }
        public PlayerProgressionService(EconomyStore store,PlayerProgressionConfig config)
        {
            this.store=store??throw new ArgumentNullException(nameof(store));if(!config)throw new ArgumentNullException(nameof(config));config.Validate();
            prefix="progression:"+config.id+":";perLevel=config.experiencePerLevel;MaximumLevel=config.maximumLevel;cap=checked((MaximumLevel-1)*perLevel);rewards=config.levelUpRewards.Select(r=>r.Copy()).ToArray();
        }
        public long TotalExperience => Math.Min(cap,store.Read(prefix+"xp"));
        public int Level => (int)(TotalExperience/perLevel)+1;
        public bool IsMaximumLevel => Level>=MaximumLevel;
        public long ExperienceIntoLevel => IsMaximumLevel?0:TotalExperience%perLevel;
        public long ExperiencePerLevel => perLevel;
        public long ExperienceToNextLevel => IsMaximumLevel?0:perLevel-ExperienceIntoLevel;
        public TransactionResult GrantExperience(long amount,string receipt)
        {
            EconomyStore.ValidateKey(receipt);if(amount<1)throw new ArgumentOutOfRangeException(nameof(amount));
            return store.Transact(prefix+"xp:"+receipt,tx=> {
                long old=Math.Min(cap,tx.Read(prefix+"xp"));if(old>=cap)return false;
                long next=old+Math.Min(amount,cap-old);int before=(int)(old/perLevel)+1,after=(int)(next/perLevel)+1;
                long highest=Math.Max(1,tx.Read(prefix+"rewardedLevel"));long gained=Math.Max(0,after-Math.Max(highest,before));
                if(gained>0)tx.Grant(rewards.Select(r=>new EconomyReward { kind=r.kind,id=r.id,amount=checked(r.amount*gained) }));
                tx.Write(prefix+"xp",next);tx.Write(prefix+"rewardedLevel",Math.Max(highest,after));return true;
            });
        }
    }
}
