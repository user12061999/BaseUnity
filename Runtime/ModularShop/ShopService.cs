using System;
using System.Linq;
using UnityEngine;
namespace BaseUnity.EconomyModules
{
    [Serializable] public sealed class ShopOffer
    {
        public string id="red_skin",title="Red skin",currency="coin";
        public long price=100;
        [Min(0)] public int purchaseLimit=1;
        public EconomyReward[] rewards={new EconomyReward { kind=RewardKind.Item,id="red_skin",amount=1 }};
    }
    public sealed class ShopService
    {
        private readonly EconomyStore store;
        private readonly ShopOffer[] offers;
        public ShopService(EconomyStore store,ShopOffer[] offers)
        {
            this.store=store??throw new ArgumentNullException(nameof(store));
            this.offers=(offers??throw new ArgumentNullException(nameof(offers))).Select(x=> {
                if(x==null)throw new ArgumentException("Null shop offer");EconomyStore.ValidateId(x.id);EconomyStore.ValidateId(x.currency);
                if(x.price<0||x.purchaseLimit<0||x.rewards==null||x.rewards.Length==0)throw new ArgumentException("Invalid offer");foreach(var r in x.rewards)r.Validate();
                return new ShopOffer { id=x.id,title=x.title,currency=x.currency,price=x.price,purchaseLimit=x.purchaseLimit,rewards=x.rewards.Select(r=>r.Copy()).ToArray() };
            }).ToArray();
            if(this.offers.Select(x=>x.id).Distinct().Count()!=this.offers.Length)throw new ArgumentException("Duplicate offer ID");
        }
        // Return copies: catalog edits at runtime must not change transaction rules silently.
        public ShopOffer[] Offers => offers.Select(x=>new ShopOffer { id=x.id,title=x.title,currency=x.currency,price=x.price,purchaseLimit=x.purchaseLimit,rewards=x.rewards.Select(r=>r.Copy()).ToArray() }).ToArray();
        private ShopOffer Find(string id) => offers.FirstOrDefault(x=>x.id==id)??throw new ArgumentException("Unknown offer: "+id);
        public long PurchaseCount(string id) { Find(id);return store.Read("shop:"+id); }
        public bool CanBuy(string id) { var x=Find(id);return (x.purchaseLimit==0||PurchaseCount(id)<x.purchaseLimit)&&store.Balance(x.currency)>=x.price; }
        public TransactionResult Buy(string id,string transactionId)
        {
            var x=Find(id);EconomyStore.ValidateKey(transactionId);
            return store.Transact("shop.buy:"+id+":"+transactionId,tx=> {
                long count=tx.Read("shop:"+id);if(x.purchaseLimit>0&&count>=x.purchaseLimit)return false;
                if(!tx.Spend(x.currency,x.price))return false;tx.Grant(x.rewards);tx.Write("shop:"+id,checked(count+1));return true;
            });
        }
    }
}
