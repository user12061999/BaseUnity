using System;
using System.IO;
using System.Linq;
using BaseUnity.Core;
using BaseUnity.EconomyModules;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;
namespace BaseUnity.Tests
{
    public sealed class ModularEconomyTests
    {
        private sealed class MemorySave : ISaveStore
        {
            public string data;public bool fail;
            public void Save<T>(string slot,T value,int version=1) where T:class { if(fail)throw new IOException("disk full");data=JsonUtility.ToJson(value); }
            public bool TryLoad<T>(string slot,out T value,int version=1) where T:class { value=data==null?null:JsonUtility.FromJson<T>(data);return value!=null; }
            public void Delete(string slot) { data=null; }
        }
        private static EconomyReward Coin(long amount) => new EconomyReward { id="coin",amount=amount };
        private static ShopOffer Offer(int limit=1) => new ShopOffer { id="skin",price=100,purchaseLimit=limit,rewards=new[]{new EconomyReward { kind=RewardKind.Item,id="skin",amount=1 }} };
        private static ObjectiveDefinition Objective(bool daily=false) => new ObjectiveDefinition { id="win3",eventId="win",target=3,dailyReset=daily,rewards=new[]{Coin(50)} };
        [Test] public void WalletReceiptsAndSpendSurviveReload()
        {
            var save=new MemorySave();var store=new EconomyStore(save);
            Assert.AreEqual(TransactionResult.Success,store.Grant("starter",Coin(100)));
            Assert.AreEqual(TransactionResult.Duplicate,store.Grant("starter",Coin(100)));
            Assert.AreEqual(TransactionResult.Rejected,store.Spend("coin",101,"too-much"));
            Assert.AreEqual(TransactionResult.Success,store.Spend("coin",40,"spend"));
            var loaded=new EconomyStore(save);Assert.AreEqual(60,loaded.Balance("coin"));Assert.AreEqual(TransactionResult.Duplicate,loaded.Spend("coin",40,"spend"));
        }
        [Test] public void SaveFailureDoesNotSpendOrGrantOrConsumeReceipt()
        {
            var save=new MemorySave();var store=new EconomyStore(save);store.Grant("starter",Coin(200));var shop=new ShopService(store,new[]{Offer()});save.fail=true;
            Assert.Throws<IOException>(()=>shop.Buy("skin","request"));Assert.AreEqual(200,store.Balance("coin"));Assert.AreEqual(0,store.Owned("skin"));Assert.AreEqual(0,shop.PurchaseCount("skin"));
            save.fail=false;Assert.AreEqual(TransactionResult.Success,shop.Buy("skin","request"));Assert.AreEqual(100,new EconomyStore(save).Balance("coin"));
        }
        [Test] public void FailedTransactionAndOverflowRollbackAllRewards()
        {
            var store=new EconomyStore(new MemorySave());store.Grant("max",Coin(long.MaxValue));
            Assert.AreEqual(TransactionResult.Rejected,store.Grant("overflow",new EconomyReward { kind=RewardKind.Item,id="skin" },Coin(1)));
            Assert.AreEqual(0,store.Owned("skin"));Assert.IsFalse(store.HasReceipt("overflow"));
            Assert.AreEqual(TransactionResult.Rejected,store.Transact("cancel",tx=> { tx.Spend("coin",10);return false; }));Assert.AreEqual(long.MaxValue,store.Balance("coin"));
        }
        [Test] public void RetainedTransactionAndReentrantCallbackCannotEditCommittedData()
        {
            var store=new EconomyStore(new MemorySave());EconomyTransaction retained=null;
            store.Transact("one",tx=> { retained=tx;tx.Grant(new[]{Coin(10)});Assert.AreEqual(TransactionResult.Rejected,store.Grant("nested",Coin(1)));return true; });
            retained.Write("money:coin",999);Assert.AreEqual(10,store.Balance("coin"));Assert.IsFalse(store.HasReceipt("nested"));
        }
        [Test] public void ShopOwnsItemOnceAndRepeatableOffersHonorReceipt()
        {
            var save=new MemorySave();var store=new EconomyStore(save);store.Grant("starter",Coin(500));var shop=new ShopService(store,new[]{Offer()});
            Assert.AreEqual(TransactionResult.Success,shop.Buy("skin","one"));Assert.IsFalse(shop.CanBuy("skin"));Assert.AreEqual(TransactionResult.Duplicate,shop.Buy("skin","one"));Assert.AreEqual(TransactionResult.Rejected,shop.Buy("skin","two"));
            var loaded=new EconomyStore(save);Assert.AreEqual(1,loaded.Owned("skin"));Assert.AreEqual(400,loaded.Balance("coin"));
            var repeat=new ShopService(loaded,new[]{Offer(0)});Assert.AreEqual(TransactionResult.Success,repeat.Buy("skin","two"));Assert.AreEqual(2,loaded.Owned("skin"));
        }
        [Test] public void ShopRejectsInsufficientFundsAndUsesCatalogSnapshot()
        {
            var store=new EconomyStore(new MemorySave());var offer=Offer();var shop=new ShopService(store,new[]{offer});offer.price=0;
            Assert.IsFalse(shop.CanBuy("skin"));Assert.AreEqual(TransactionResult.Rejected,shop.Buy("skin","request"));
            shop.Offers[0].price=0;Assert.IsFalse(shop.CanBuy("skin"));Assert.AreEqual(0,store.Owned("skin"));
        }
        [Test] public void DailyCyclesAfterCooldownPersistsAndRejectsBackwardClock()
        {
            long now=100000;var save=new MemorySave();var store=new EconomyStore(save);var config=ScriptableObject.CreateInstance<DailyRewardSchedule>();
            config.cooldownSeconds=100;config.days=new[]{new DailyRewardDay { rewards=new[]{Coin(10)} },new DailyRewardDay { rewards=new[]{Coin(20)} }};
            try {
                var daily=new DailyRewardService(store,config,()=>now);Assert.IsTrue(daily.CanClaim);Assert.AreEqual(TransactionResult.Success,daily.Claim());Assert.AreEqual(1,daily.NextDay);
                Assert.AreEqual(TransactionResult.Rejected,daily.Claim());now-=500;Assert.IsFalse(daily.CanClaim);Assert.AreEqual(TransactionResult.Rejected,daily.Claim());
                now=100099;Assert.AreEqual(1,daily.RemainingSeconds);now++;Assert.AreEqual(TransactionResult.Success,daily.Claim());Assert.AreEqual(0,daily.NextDay);
                var loaded=new DailyRewardService(new EconomyStore(save),config,()=>now);Assert.AreEqual(2,loaded.ClaimCount);Assert.IsFalse(loaded.CanClaim);Assert.AreEqual(30,store.Balance("coin"));
            } finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void DailySaveFailureLeavesDayAndWalletUnchanged()
        {
            var save=new MemorySave();var store=new EconomyStore(save);var config=ScriptableObject.CreateInstance<DailyRewardSchedule>();
            try { var daily=new DailyRewardService(store,config,()=>100L);save.fail=true;Assert.Throws<IOException>(()=>daily.Claim());Assert.AreEqual(0,daily.ClaimCount);Assert.AreEqual(0,store.Balance("coin"));save.fail=false;Assert.AreEqual(TransactionResult.Success,daily.Claim()); }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void QuestProgressClaimsAndEventDedupSurviveReload()
        {
            var save=new MemorySave();var store=new EconomyStore(save);var quest=new QuestService(store,new[]{Objective()});
            Assert.AreEqual(TransactionResult.Rejected,quest.Claim("win3"));quest.Record("win",2,"match1");Assert.AreEqual(TransactionResult.Duplicate,quest.Record("win",2,"match1"));
            quest.Record("win",long.MaxValue,"match2");Assert.AreEqual(3,quest.Statuses[0].progress);Assert.IsTrue(quest.Statuses[0].CanClaim);
            Assert.AreEqual(TransactionResult.Success,quest.Claim("win3"));Assert.AreEqual(TransactionResult.Duplicate,quest.Claim("win3"));Assert.AreEqual(50,store.Balance("coin"));
            var loaded=new QuestService(new EconomyStore(save),new[]{Objective()});Assert.IsTrue(loaded.Statuses[0].claimed);Assert.AreEqual(TransactionResult.Duplicate,loaded.Record("win",2,"match1"));
        }
        [Test] public void DailyQuestsResetAtUtcDayAndDoNotReopenOldClaimOnClockRollback()
        {
            long now=86400;var store=new EconomyStore(new MemorySave());var quest=new QuestService(store,new[]{Objective(true)},utcSeconds:()=>now);
            quest.Record("win",3,"first");quest.Claim("win3");now=172800;Assert.AreEqual(0,quest.Statuses[0].progress);quest.Record("win",3,"second");quest.Claim("win3");
            now=86400;Assert.IsTrue(quest.Statuses[0].claimed);Assert.AreEqual(TransactionResult.Duplicate,quest.Claim("win3"));Assert.AreEqual(100,store.Balance("coin"));
        }
        [Test] public void EventBusUpdatesIndependentQuestsAndAchievementsAndDisposesSubscriptions()
        {
            var bus=new EventBus();var store=new EconomyStore(new MemorySave());var quest=new QuestService(store,new[]{Objective()},bus);var achievement=new AchievementService(store,new[]{Objective()},bus);
            bus.Publish(new GameplayProgressEvent("win",2,"match1"));bus.Publish(new GameplayProgressEvent("win",2,"match1"));Assert.AreEqual(2,quest.Statuses[0].progress);Assert.AreEqual(2,achievement.Statuses[0].progress);
            quest.Dispose();bus.Publish(new GameplayProgressEvent("win",1,"match2"));Assert.AreEqual(2,quest.Statuses[0].progress);Assert.AreEqual(3,achievement.Statuses[0].progress);achievement.Claim("win3");Assert.AreEqual(50,store.Balance("coin"));achievement.Dispose();
        }
        [Test] public void ClaimSaveFailureCanRetryWithoutLosingProgressOrDoubleReward()
        {
            var save=new MemorySave();var store=new EconomyStore(save);var quest=new QuestService(store,new[]{Objective()});quest.Record("win",3,"event");save.fail=true;
            Assert.Throws<IOException>(()=>quest.Claim("win3"));Assert.IsTrue(quest.Statuses[0].CanClaim);Assert.AreEqual(0,store.Balance("coin"));save.fail=false;
            Assert.AreEqual(TransactionResult.Success,quest.Claim("win3"));Assert.AreEqual(50,store.Balance("coin"));
        }
        [Test] public void InvalidDefinitionsAndIdsFailBeforeMutation()
        {
            var store=new EconomyStore(new MemorySave());Assert.Throws<ArgumentException>(()=>new ShopService(store,new[]{Offer(),Offer()}));Assert.Throws<ArgumentException>(()=>new AchievementService(store,new[]{Objective(true)}));
            Assert.Throws<ArgumentException>(()=>store.Grant("bad",Coin(-1)));Assert.Throws<ArgumentException>(()=>store.Balance("bad:id"));Assert.IsFalse(store.HasReceipt("bad"));
        }
        [Test] public void CorruptStateIsNotSilentlyReset()
        {
            var save=new MemorySave { data="{\"values\":[{\"key\":\"money:coin\",\"value\":-1}],\"receipts\":[]}" };string before=save.data;
            Assert.Throws<InvalidOperationException>(()=>new EconomyStore(save));Assert.AreEqual(before,save.data);
        }
        [Test] public void DeleteMissingSaveIsIdempotent()
        {
            string missing=Path.Combine(Path.GetTempPath(),"BaseUnityEconomy-"+Guid.NewGuid().ToString("N"));
            var save=new JsonSaveStore(missing);Assert.DoesNotThrow(()=>save.Delete("never_saved"));Assert.IsFalse(Directory.Exists(missing));
        }
        [Test] public void FutureVersionSaveIsPreserved()
        {
            string dir=Path.Combine(Path.GetTempPath(),"BaseUnityEconomy-"+Guid.NewGuid().ToString("N"));
            try { var save=new JsonSaveStore(dir);save.Save("baseunity_modular_economy",new EconomyState(),2);string original=File.ReadAllText(Path.Combine(dir,"baseunity_modular_economy.json"));Assert.Throws<NotSupportedException>(()=>new EconomyStore(save));Assert.AreEqual(original,File.ReadAllText(Path.Combine(dir,"baseunity_modular_economy.json"))); }
            finally { if(Path.GetFullPath(dir).StartsWith(Path.Combine(Path.GetTempPath(),"BaseUnityEconomy-"))&&Directory.Exists(dir))Directory.Delete(dir,true); }
        }
    }
}
