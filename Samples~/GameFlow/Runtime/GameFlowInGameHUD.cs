using System;
using System.Collections.Generic;
using System.Linq;
using BaseUnity.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class GameFlowInGameHUD : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private FeatureServices services;
        private InGameHUDView view;
        private BoosterController controller;
        private LevelContext session, dialogSession;
        private BoosterDefinition selected;
        private bool ownsPause, buying;
        private readonly List<Tuple<BoosterDefinition, BoosterSlotView>> slots = new List<Tuple<BoosterDefinition, BoosterSlotView>>();
        public InGameHUDView View => view;
        public bool IsModalOpen => selected != null;
        private OfflineService Economy => services.Offline;
        private string T(string value) => services.Localization.Text(value ?? "");
        private bool Busy => flow.Features.Busy || services.Consent.IsBusy || services.Purchases.IsBusy || services.Popups.Active != null;
        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner; services = owner.Services.Get<FeatureServices>();
            view = Instantiate(flow.Config.inGameHUDPrefab, transform); view.modal.SetActive(false);
            var definitions = flow.Config.boosters ? flow.Config.boosters.boosters : null;
            foreach (var definition in (definitions ?? Array.Empty<BoosterDefinition>()).Where(item => item != null).Take(4))
            {
                var slot = Instantiate(view.slotPrefab, view.slots); slots.Add(Tuple.Create(definition, slot));
                slot.icon.enabled = definition.icon; if (definition.icon) slot.icon.sprite = definition.icon;
                slot.button.name = "Booster_" + definition.itemId;
                slot.button.onClick.AddListener(() => Open(definition));
            }
            view.cancel.onClick.AddListener(CloseDialog); view.accept.onClick.AddListener(Confirm);
            view.accept.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = view.cancel, selectOnDown = view.cancel };
            view.cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = view.accept, selectOnUp = view.accept };
            flow.Changed += PhaseChanged;
        }
        private void PhaseChanged()
        { if (flow.Phase != FlowPhase.Playing && flow.Phase != FlowPhase.Paused) CloseDialog(); }
        private void RefreshController()
        {
            if (controller != null && session == flow.Levels.Context) return;
            session = flow.Levels.Context; controller = new BoosterController(flow, new OfflineBoosterInventory(Economy));
            if (flow.Levels.Instance) foreach (var behaviour in flow.Levels.Instance.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour is IBoosterEffect effect) controller.Register(effect);
        }
        private void LateUpdate()
        {
            if (!flow || !flow.IsReady) return; RefreshController();
            bool enabled = Economy.Enabled && flow.Config.boosters && flow.Config.boosters.enabled;
            if (IsModalOpen && (!enabled || dialogSession != flow.Levels.Context)) CloseDialog();
            bool visible = flow.Phase == FlowPhase.Playing || (IsModalOpen && flow.Phase == FlowPhase.Paused);
            view.gameplay.SetActive(visible);
            view.boosterBar.SetActive(enabled && slots.Count > 0);
            view.level.text = T("LEVEL") + " " + flow.CurrentLevel;
            int target = Math.Max(1, flow.CurrentDefinition?.targetScore ?? 1);
            view.target.text = T("TARGET") + " " + flow.Score + " / " + target;
            view.progress.fillAmount = Mathf.Clamp01((float)flow.Score / target);
            view.timer.text = flow.HasTimeLimit ? T("TIME") + " " + Mathf.CeilToInt(flow.RemainingTime) + "s" : T("NO TIME LIMIT");
            foreach (var entry in slots)
            {
                var definition = entry.Item1; var slot = entry.Item2;
                var state = enabled && !IsModalOpen ? controller.Check(definition) : BoosterAvailability.Unavailable;
                slot.title.text = T(definition.title); slot.quantity.text = "x" + Economy.ItemCount(definition.itemId);
                slot.status.text = T(state == BoosterAvailability.Ready ? "USE" : state == BoosterAvailability.Empty ? (Economy.ShopEnabled ? "BUY" : "EMPTY") : state == BoosterAvailability.LimitReached ? "LIMIT REACHED" : state == BoosterAvailability.Cooldown ? "COOLDOWN" : "NOT AVAILABLE");
                if (state == BoosterAvailability.Cooldown) slot.status.text += " " + Mathf.CeilToInt(controller.Cooldown(definition)) + "s";
                slot.button.interactable = !Busy && (state == BoosterAvailability.Ready || (state == BoosterAvailability.Empty && Economy.ShopEnabled));
            }
            if (IsModalOpen && buying)
            {
                var item = ShopItem(selected.itemId);
                view.accept.interactable = !Busy && item != null && Economy.ShopEnabled && Economy.Balance(item.currency) >= item.price && (item.purchaseLimit == 0 || Economy.PurchaseCount(item.id) < item.purchaseLimit);
                if (!view.accept.interactable) view.modalMessage.text = T(item == null ? "Booster is not in Store" : item.purchaseLimit > 0 && Economy.PurchaseCount(item.id) >= item.purchaseLimit ? "Purchase limit reached" : "Not enough currency");
            }
            else if (IsModalOpen) view.accept.interactable = !Busy;
        }
        private ShopItem ShopItem(string id) => (Economy.Settings.items ?? Array.Empty<ShopItem>()).FirstOrDefault(item => item != null && item.id == id);
        public void Open(BoosterDefinition definition)
        {
            if (IsModalOpen || Busy || flow.Phase != FlowPhase.Playing || !Economy.Enabled || !flow.Config.boosters || !flow.Config.boosters.enabled) return;
            RefreshController(); var state = controller.Check(definition);
            if (state != BoosterAvailability.Ready && state != BoosterAvailability.Empty) return;
            buying = state == BoosterAvailability.Empty;
            if (buying && !Economy.ShopEnabled) return;
            selected = definition; dialogSession = flow.Levels.Context; ownsPause = true;
            flow.Pause(); view.modal.SetActive(true);
            view.modalTitle.text = T(buying ? "BOOSTER STORE" : "USE BOOSTER");
            var item = ShopItem(definition.itemId);
            string details = T(definition.title) + ((definition.effectId == "extra_time" || definition.effectId == "hint") ? " • " + definition.amount + "s" : "");
            view.modalMessage.text = buying ? (item == null ? T("Booster is not in Store") : details + "\n" + T(item.title) + " x" + item.quantity + " • " + item.price + " " + T(item.currency.ToString())) : details + " • x1";
            view.acceptLabel.text = T(buying ? "BUY" : "USE"); view.message.text = "";
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(view.cancel.gameObject);
        }
        private void Confirm()
        {
            if (!Economy.Enabled || !flow.Config.boosters || !flow.Config.boosters.enabled) { CloseDialog(); return; }
            if (!IsModalOpen || Busy || flow.Phase != FlowPhase.Paused || dialogSession != flow.Levels.Context) return;
            var definition = selected;
            if (buying)
            {
                if (!Economy.Buy(definition.itemId)) { view.modalMessage.text = T(Economy.LastError); services.UI.Play(UICue.Error); return; }
                services.UI.Play(UICue.Purchase); CloseDialog(); view.message.text = T("Purchased. Tap booster to use"); return;
            }
            CloseDialog();
            bool used = controller.TryUse(definition);
            view.message.text = T(used ? "Booster used" : controller.LastError);
            services.UI.Play(used ? UICue.Success : UICue.Error);
            if (used) flow.Features.Track("booster_use", "item_id", definition.itemId);
        }
        public void CloseDialog()
        {
            bool resume = ownsPause && dialogSession == flow.Levels.Context && flow.Phase == FlowPhase.Paused;
            ownsPause = false; dialogSession = null; selected = null;
            if (view) view.modal.SetActive(false);
            if (resume) flow.Resume();
        }
        private void OnApplicationFocus(bool focus) { if (!focus && IsModalOpen) { ownsPause = false; CloseDialog(); } }
        private void OnDestroy() { if (flow) flow.Changed -= PhaseChanged; }
    }
}
