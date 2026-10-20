using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;

namespace BaseUnity.GameFlow
{
    /// <summary>Editable sample presentation; wallet, quest and receipt transactions stay in services.</summary>
    public sealed class GameFlowHub : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private FeatureServices services;
        private HomeHubView view;
        private HomeHubCatalog catalog;
        private string screen, section = "COINS", message, utcDay;
        private int page;
        private bool dirty = true, purchaseStarting;
        private float tickAt;
        private Action confirmed;
        public bool IsModalOpen => !string.IsNullOrEmpty(screen);
        public HomeHubView View => view;
        private OfflineService Economy => services.Offline;
        private bool Busy => purchaseStarting || services.Purchases.IsBusy || flow.Features.Busy || services.Consent.IsBusy;
        private string T(string source) => services.Localization.Text(source ?? "");
        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner; services = flow.Services.Get<FeatureServices>(); catalog = flow.Config.homeHubCatalog;
            view = Instantiate(flow.Config.homeHubPrefab, transform);
            view.play.name = "Play"; view.levels.name = "Choose Level"; view.settings.name = "Settings";
            view.play.onClick.AddListener(flow.Play); view.levels.onClick.AddListener(flow.ShowLevels);
            view.settings.onClick.AddListener(() => { view.gameObject.SetActive(false); var art = flow.GetComponentInChildren<GameFlowView>(); if (art) art.OpenSettings(); else flow.GetComponent<GameFlowUI>()?.OpenSettings(); });
            view.store.onClick.AddListener(() => Open("STORE")); view.quests.onClick.AddListener(() => Open("QUESTS"));
            view.daily.onClick.AddListener(() => flow.GetComponent<GameFlowOfflineUI>().OpenTab("DAILY REWARD"));
            view.inventory.onClick.AddListener(() => flow.GetComponent<GameFlowOfflineUI>().OpenTab("INVENTORY"));
            view.more.onClick.AddListener(() => flow.GetComponent<GameFlowMobileModules>().OpenMenu());
            view.SelectPage(true);
            view.questPage.restore.gameObject.SetActive(false); view.questPage.retryPending.gameObject.SetActive(false);
            view.storePage.close.onClick.AddListener(Close); view.questPage.close.onClick.AddListener(Close);
            BindTab(view.coinsTab, "COINS"); BindTab(view.iapTab, "IAP"); BindTab(view.dailyTab, "DAILY QUESTS"); BindTab(view.achievementTab, "ACHIEVEMENTS");
            foreach (var pageView in new[] { view.storePage, view.questPage })
            {
                pageView.previous.onClick.AddListener(() => { page = Math.Max(0, page - 1); Dirty(); });
                pageView.next.onClick.AddListener(() => { page++; Dirty(); });
            }
            view.confirmCancel.onClick.AddListener(() => { confirmed = null; view.confirmation.SetActive(false); });
            view.confirmAccept.onClick.AddListener(() => { if (Busy || confirmed == null) return; var action = confirmed; confirmed = null; view.confirmation.SetActive(false); action(); });
            view.restore.onClick.AddListener(() => { _ = StoreOperation(true); });
            view.retryPending.onClick.AddListener(() => { _ = StoreOperation(false); });
            services.Offline.Changed += Dirty; services.Purchases.Changed += Dirty; services.Localization.Changed += Dirty; flow.Changed += FlowChanged;
            (view.play.GetComponent<TutorialTarget>() ?? view.play.gameObject.AddComponent<TutorialTarget>()).Initialize("home.play", services.Tutorial);
            Refresh();
        }
        private void BindTab(UnityEngine.UI.Button button, string key) => button.onClick.AddListener(() => { if (Busy) return; section = key; page = 0; message = null; confirmed = null; Dirty(); });
        private void Dirty() { dirty = true; }
        private void FlowChanged() { if (flow.Phase != FlowPhase.Home) Close(); Dirty(); }
        public void Open(string destination)
        {
            if (Busy || flow.Phase != FlowPhase.Home || services.Popups.Active != null) return;
            if (destination == "STORE" && !(Economy.ShopEnabled || services.Purchases.Enabled)) return;
            if (destination == "QUESTS" && !(Economy.MissionsEnabled || Economy.AchievementsEnabled)) return;
            if (destination != "STORE" && destination != "QUESTS") return;
            screen = destination; section = screen == "STORE" ? (Economy.ShopEnabled ? "COINS" : "IAP") : (Economy.MissionsEnabled ? "DAILY QUESTS" : "ACHIEVEMENTS");
            page = 0; message = null; confirmed = null; Dirty();
        }
        public void Close() { screen = null; confirmed = null; if (view) view.confirmation.SetActive(false); Dirty(); }
        private void LateUpdate()
        {
            if (!flow || !flow.IsReady) return;
            var art = flow.GetComponentInChildren<GameFlowView>();
            bool settings = art ? art.SettingsOpen : flow.GetComponent<GameFlowUI>()?.SettingsOpen == true;
            bool visible = flow.Phase == FlowPhase.Home && !settings && !flow.GetComponent<GameFlowOfflineUI>().LegacyIsOpen && !flow.GetComponent<GameFlowMobileModules>().IsMenuOpen;
            view.gameObject.SetActive(visible);
            if (art && art.home)
            {
                var group = art.home.GetComponent<CanvasGroup>(); if (!group) group = art.home.AddComponent<CanvasGroup>();
                group.alpha = visible ? 0 : 1; group.blocksRaycasts = group.interactable = !visible;
            }
            else flow.GetComponent<GameFlowUI>()?.CoverHome(visible);
            if (UnityEngine.Time.unscaledTime >= tickAt)
            {
                tickAt = UnityEngine.Time.unscaledTime + 1;
                string day = DateTime.UtcNow.ToString("yyyyMMdd"); if (day != utcDay) { utcDay = day; dirty = true; }
                UpdateHeader();
            }
            if (dirty) { dirty = false; Refresh(); }
            view.storePage.close.interactable = view.questPage.close.interactable = !Busy;
            view.confirmAccept.interactable = !Busy;
        }
        private void UpdateHeader()
        {
            view.wallet.gameObject.SetActive(Economy.Enabled);
            view.wallet.text = Economy.Coins.ToString("N0") + " " + T("Coin") + "    " + Economy.Gems.ToString("N0") + " " + T("Gem");
            view.playerName.text = catalog ? catalog.playerName : "PLAYER";
            if (catalog && catalog.avatar) { view.avatar.sprite = catalog.avatar; view.avatar.enabled = true; }
            view.level.text = T("LEVEL") + " " + flow.HomeLevel;
            view.gameTitle.text = flow.Config.gameTitle;
            view.store.gameObject.SetActive(Economy.ShopEnabled || services.Purchases.Enabled);
            view.quests.gameObject.SetActive(Economy.MissionsEnabled || Economy.AchievementsEnabled);
            view.daily.gameObject.SetActive(Economy.DailyEnabled); view.inventory.gameObject.SetActive(Economy.Enabled);
            view.more.gameObject.SetActive(services.Haptics.ModuleEnabled || services.Notifications.Enabled || services.Tutorial.Enabled || services.Purchases.Enabled);
            view.dailyDot.SetActive(HubSignals.DailyReady(Economy));
            int count = HubSignals.ClaimableQuests(Economy); view.questDot.SetActive(count > 0); view.questCount.text = count.ToString();
            var remaining = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            view.countdown.text = T("DAILY RESET") + "  " + remaining.ToString(@"hh\:mm\:ss") + " UTC";
        }
        private void Refresh()
        {
            UpdateHeader();
            view.home.SetActive(!IsModalOpen); view.modal.SetActive(IsModalOpen);
            view.confirmation.SetActive(confirmed != null);
            if (!IsModalOpen) return;
            bool store = screen == "STORE";
            view.SelectPage(store);
            view.storePage.countdown.gameObject.SetActive(false);
            view.questPage.countdown.gameObject.SetActive(section == "DAILY QUESTS");
            view.heading.text = T(screen); view.message.text = T(message ?? (store && section == "IAP" ? services.Purchases.LastError : null) ?? Economy.LoadError ?? "");
            view.coinsTab.gameObject.SetActive(store && Economy.ShopEnabled); view.iapTab.gameObject.SetActive(store && services.Purchases.Enabled);
            view.dailyTab.gameObject.SetActive(!store && Economy.MissionsEnabled); view.achievementTab.gameObject.SetActive(!store && Economy.AchievementsEnabled);
            view.restore.gameObject.SetActive(store && section == "IAP"); view.restore.interactable = !Busy;
            view.retryPending.gameObject.SetActive(store && section == "IAP" && services.Purchases.PendingCount > 0); view.retryPending.interactable = !Busy;
            foreach (var tab in new[] { view.coinsTab, view.iapTab, view.dailyTab, view.achievementTab }) tab.interactable = !Busy && tab.name != section;
            foreach (Transform row in view.rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            var entries = new List<Action>(); var settings = Economy.Settings;
            if (section == "COINS") foreach (var item in settings.items ?? Array.Empty<ShopItem>()) { if (item != null) { var captured = item; entries.Add(() => CoinCard(captured)); } }
            else if (section == "IAP")
            {
                // The FeatureConfig is only used for catalog metadata. Receipt policy lives in PurchaseService.
                foreach (var product in flow.Config.features?.purchasing.products ?? Array.Empty<StoreProduct>())
                { if (product != null) { var captured = product; entries.Add(() => IapCard(captured)); } }
            }
            else
            {
                bool lifetime = section == "ACHIEVEMENTS";
                foreach (var goal in (lifetime ? settings.achievementGoals : settings.dailyMissions) ?? Array.Empty<DailyMission>())
                { if (goal != null) { var captured = goal; entries.Add(() => QuestCard(captured, lifetime)); } }
            }
            int pages = Math.Max(1, (entries.Count + 2) / 3); page = Mathf.Clamp(page, 0, pages - 1);
            foreach (var create in entries.Skip(page * 3).Take(3)) create();
            view.empty.SetActive(entries.Count == 0); view.pageNumber.text = (page + 1) + " / " + pages;
            view.previous.interactable = page > 0 && !Busy; view.next.interactable = page + 1 < pages && !Busy;
        }
        private HubCardView Card(string id, string title, string fallbackDescription)
        {
            var card = Instantiate(screen == "QUESTS" && view.questCardPrefab ? view.questCardPrefab : view.cardPrefab, view.rows); var entry = catalog ? catalog.Find(id) : null;
            bool vi = services.Localization.Enabled && services.Localization.Language == "vi";
            string customTitle = entry == null ? null : vi ? entry.titleVietnamese : entry.titleEnglish;
            card.title.text = string.IsNullOrEmpty(customTitle) ? T(title) : customTitle;
            string customDescription = entry == null ? null : vi ? entry.descriptionVietnamese : entry.descriptionEnglish;
            card.description.text = string.IsNullOrEmpty(customDescription) ? T(fallbackDescription) : customDescription;
            if (entry != null && entry.icon) card.icon.sprite = entry.icon;
            card.progressRoot.SetActive(false); card.progress.text = "";
            return card;
        }
        private void CoinCard(ShopItem item)
        {
            var card = Card(item.id, item.title, "Add items to your inventory");
            bool owned = item.purchaseLimit > 0 && Economy.PurchaseCount(item.id) >= item.purchaseLimit;
            card.reward.text = "x" + item.quantity + " • " + item.price + " " + T(item.currency.ToString());
            card.progress.text = T("OWNED") + " x" + Economy.ItemCount(item.id);
            card.actionLabel.text = T(owned ? "SOLD OUT" : Economy.Balance(item.currency) < item.price ? "NOT ENOUGH" : "BUY");
            card.action.name = "Buy_" + item.id; card.action.interactable = !owned && !Busy && Economy.LoadError == null;
            card.action.onClick.AddListener(() => {
                if (Busy) return;
                if (Economy.Balance(item.currency) < item.price) { message = "Not enough currency"; services.UI.Play(UICue.Error); Dirty(); return; }
                Confirm(T("BUY") + " " + T(item.title) + " x" + item.quantity + " • " + item.price + " " + T(item.currency.ToString()), () => {
                    bool ok = Economy.Buy(item.id); Result(ok, "Purchase completed"); if (ok) flow.Features.Track("shop_purchase", "item_id", item.id);
                });
            });
        }
        private void QuestCard(DailyMission goal, bool lifetime)
        {
            long progress = lifetime ? Economy.AchievementProgress(goal) : Economy.MissionProgress(goal);
            bool claimed = lifetime ? Economy.AchievementClaimed(goal.id) : Economy.MissionClaimed(goal.id);
            var card = Card(goal.id, goal.title, lifetime ? "Permanent milestone" : "Complete before the daily reset");
            card.reward.text = "+" + goal.coins + " " + T("Coin"); card.progressRoot.SetActive(true);
            card.progressFill.fillAmount = goal.target <= 0 ? 0 : Mathf.Clamp01((float)progress / goal.target);
            card.progress.text = Math.Min(progress, goal.target) + " / " + goal.target;
            card.actionLabel.text = T(claimed ? "CLAIMED" : progress >= goal.target ? "CLAIM" : "IN PROGRESS");
            card.action.name = "Claim_" + goal.id; card.action.interactable = !Busy && !claimed && progress >= goal.target && Economy.LoadError == null;
            card.action.onClick.AddListener(() => { if (Busy) return; bool ok = lifetime ? Economy.ClaimAchievement(goal.id) : Economy.ClaimMission(goal.id); Result(ok, "Reward received"); if (ok) flow.Features.Track(lifetime ? "achievement_claim" : "mission_claim", "goal_id", goal.id); });
        }
        private void IapCard(StoreProduct product)
        {
            var info = services.Purchases.Products.FirstOrDefault(value => value.id == product.id);
            bool owned = product.kind == StoreProductKind.NonConsumable && Economy.HasStoreProduct(product.id);
            var card = Card(product.id, info?.title ?? product.id, "Store purchase requires verification");
            card.reward.text = info?.price ?? T("PRICE UNAVAILABLE"); card.progress.text = "+" + product.coins + " " + T("Coin") + " / +" + product.gems + " " + T("Gem");
            card.actionLabel.text = T(owned ? "OWNED" : "BUY"); card.action.name = "IAP_" + product.id;
            card.action.interactable = !Busy && !owned && info != null && info.available;
            card.action.onClick.AddListener(() => Confirm(card.title.text + " • " + card.reward.text, () => { _ = Purchase(product.id); }));
        }
        private void Confirm(string text, Action action) { if (Busy) return; confirmed = action; view.confirmMessage.text = text; view.confirmation.SetActive(true); }
        private void Result(bool success, string successMessage)
        { message = success ? successMessage : Economy.LastError; if (!success) services.UI.Play(UICue.Error); else if (successMessage == "Purchase completed") services.UI.Play(UICue.Purchase); Dirty(); }
        private async Task Purchase(string id)
        {
            if (Busy) return; purchaseStarting = true; Dirty();
            try { bool accepted = await services.Purchases.BuyAsync(id); if (this) message = accepted ? "Purchase pending verification" : services.Purchases.LastError ?? "Purchase unavailable"; }
            finally { if (this) { purchaseStarting = false; Dirty(); } }
        }
        private async Task StoreOperation(bool restore)
        {
            if (Busy) return; purchaseStarting = true; Dirty();
            try
            {
                if (restore) { bool ok = await services.Purchases.RestoreAsync(); if (this) message = ok ? "Restore completed" : services.Purchases.LastError; }
                else { await services.Purchases.RetryPendingAsync(); if (this) message = services.Purchases.LastError ?? "Delivery retried"; }
            }
            finally { if (this) { purchaseStarting = false; Dirty(); } }
        }
        private void OnDestroy()
        {
            if (services != null) { services.Offline.Changed -= Dirty; services.Purchases.Changed -= Dirty; services.Localization.Changed -= Dirty; }
            if (flow) flow.Changed -= FlowChanged;
        }
    }
}
