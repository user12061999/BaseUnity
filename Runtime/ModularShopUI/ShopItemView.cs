using System;
using System.Linq;
using BaseUnity.Core;
using UnityEngine.UI;
namespace BaseUnity.EconomyModules.UI
{
    public sealed class ShopItemView : EconomyViewBase
    {
        public string offerId="red_skin";
        public Text titleLabel,priceLabel,ownedLabel;
        public Button buyButton;
        private ShopService shop;
        private string pendingReceipt;
        protected override bool Bind(ServiceRegistry services) => services.TryGet(out shop);
        protected override void RefreshView()
        {
            var offer=shop.Offers.FirstOrDefault(x=>x.id==offerId);
            if(buyButton)buyButton.interactable=offer!=null&&shop.CanBuy(offerId);
            if(offer==null) { if(titleLabel)titleLabel.text="Unknown offer: "+offerId;return; }
            if(titleLabel)titleLabel.text=offer.title;if(priceLabel)priceLabel.text=offer.price+" "+offer.currency;
            if(ownedLabel)ownedLabel.text="Purchased: "+shop.PurchaseCount(offerId)+(offer.purchaseLimit>0?" / "+offer.purchaseLimit:"");
        }
        public void Buy()
        {
            if(pendingReceipt==null)pendingReceipt=Guid.NewGuid().ToString("N");
            Execute(()=> { var result=shop.Buy(offerId,pendingReceipt);if(result!=TransactionResult.Rejected)pendingReceipt=null;return result; });
        }
    }
}
