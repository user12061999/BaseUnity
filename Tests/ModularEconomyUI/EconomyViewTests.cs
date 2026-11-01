using System.Collections;
using BaseUnity.Core;
using BaseUnity.EconomyModules;
using BaseUnity.EconomyModules.UI;
using BaseUnity.Save;
using BaseUnity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace BaseUnity.Tests
{
    public sealed class EconomyTestSaveInstaller : ModuleInstaller
    {
        private sealed class Store : ISaveStore {
            private string data;
            public void Save<T>(string key,T value,int version=1) where T:class { data=JsonUtility.ToJson(value); }
            public bool TryLoad<T>(string key,out T value,int version=1) where T:class { value=data==null?null:JsonUtility.FromJson<T>(data);return value!=null; }
            public void Delete(string key) { data=null; }
        }
        public override int Priority => -100;
        public override void Install(ModuleBootstrap bootstrap) { bootstrap.Services.Register<ISaveStore>(new Store()); }
    }
    public sealed class EconomyViewTests
    {
        [UnityTest] public IEnumerator ViewsBindAndReflectPurchasesDailyClaimsAndProgress()
        {
            var root=new GameObject("Economy UI test");root.SetActive(false);
            var catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.offers=new[]{new ShopOffer()};
            var daily=ScriptableObject.CreateInstance<DailyRewardSchedule>();
            var quest=ScriptableObject.CreateInstance<QuestCatalog>();quest.objectives=new[]{new ObjectiveDefinition()};
            var achievement=ScriptableObject.CreateInstance<AchievementCatalog>();achievement.objectives=new[]{new ObjectiveDefinition()};
            try {
                root.AddComponent<EconomyTestSaveInstaller>();root.AddComponent<WalletModule>();root.AddComponent<UIModule>();
                root.AddComponent<ShopModule>().catalog=catalog;root.AddComponent<DailyRewardModule>().schedule=daily;
                root.AddComponent<QuestModule>().catalog=quest;root.AddComponent<AchievementModule>().catalog=achievement;
                var bootstrap=root.AddComponent<ModuleBootstrap>();
                var wallet=Child<WalletBarView>(root.transform);wallet.coinLabel=Label(wallet.transform);
                var shop=Child<ShopItemView>(root.transform);shop.buyButton=Child<Button>(shop.transform);shop.ownedLabel=Label(shop.transform);
                var reward=Child<DailyRewardView>(root.transform);reward.claimButton=Child<Button>(reward.transform);reward.dayLabel=Label(reward.transform);
                var quests=Child<QuestItemView>(root.transform);quests.claimButton=Child<Button>(quests.transform);quests.progressLabel=Label(quests.transform);
                var achievements=Child<AchievementItemView>(root.transform);achievements.claimButton=Child<Button>(achievements.transform);
                root.SetActive(true);yield return null;yield return null;
                Assert.IsTrue(bootstrap.IsReady);Assert.IsFalse(shop.buyButton.interactable);wallet.GrantDemoStarter();Assert.IsTrue(shop.buyButton.interactable);
                shop.Buy();Assert.AreEqual(1,bootstrap.Services.Get<EconomyStore>().Owned("red_skin"));Assert.IsFalse(shop.buyButton.interactable);Assert.That(wallet.coinLabel.text,Does.Contain("900"));
                reward.Claim();Assert.IsFalse(reward.claimButton.interactable);Assert.That(reward.dayLabel.text,Does.Contain("2"));
                var bus=bootstrap.Services.Get<EventBus>();bus.Publish(new GameplayProgressEvent("win",3,"match1"));Assert.IsTrue(quests.claimButton.interactable);Assert.IsTrue(achievements.claimButton.interactable);
                quests.Claim();Assert.IsFalse(quests.claimButton.interactable);Assert.AreEqual("Claimed",quests.progressLabel.text);achievements.Claim();Assert.IsFalse(achievements.claimButton.interactable);
                wallet.gameObject.SetActive(false);wallet.gameObject.SetActive(true);Assert.That(wallet.coinLabel.text,Does.Contain("1150"));
            } finally { Object.Destroy(root);Object.Destroy(catalog);Object.Destroy(daily);Object.Destroy(quest);Object.Destroy(achievement); }
            yield return null;
        }
        [UnityTest] public IEnumerator SelfClosedPopupsDoNotLeaveGhostBackEntries()
        {
            var parent=new GameObject("Popup test");var first=Child<UIPanel>(parent.transform);var second=Child<UIPanel>(parent.transform);var ui=new UIService();
            ui.PushPopup(first);ui.PushPopup(second);second.Hide();Assert.IsTrue(ui.Back());Assert.IsFalse(first.IsVisible);Assert.IsFalse(ui.Back());
            ui.Dispose();Object.Destroy(parent);yield return null;
        }
        private static T Child<T>(Transform parent) where T:Component { var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(parent,false);return go.AddComponent<T>(); }
        private static Text Label(Transform parent) => Child<Text>(parent);
    }
}
