using System;
using System.Linq;
using BaseUnity.Features;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Portable portrait overlay. Reuses the installed view's button art when present.</summary>
    public sealed class GameFlowOfflineUI : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private GameFlowFeatures features;
        private RectTransform content;
        private GameObject page;
        private OfflineMenuView menu;
        private bool transactionBusy, messageIsError;
        private Font font;
        private Sprite buttonSprite;
        private bool dirty = true, open;
        private string tab = "SHOP", message;
        private int pageIndex;
        private float refreshAt;
        public bool IsOpen => open || GetComponent<GameFlowHub>()?.IsModalOpen == true;
        public bool LegacyIsOpen => open;
        public void OpenTab(string key)
        {
            if (flow.Phase != FlowPhase.Home || features.Busy || !TabEnabled(key)) return;
            tab = key; pageIndex = 0; message = null; open = true; dirty = true;
        }
        private OfflineService Economy => features.Offline;
        private string T(string value) => features.Language.Text(value);
        public void Initialize(GameFlowBootstrap owner, GameFlowFeatures integration)
        {
            flow = owner; features = integration;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var view = GetComponentInChildren<GameFlowView>();
            if (view && view.playButton) buttonSprite = view.playButton.image.sprite;
            var root = new GameObject("Offline Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = .5f;
            content = Rect(root.transform, "Safe Area", 900, 1600, 0, 0);
            var safe = root.AddComponent<GameFlowSafeArea>(); safe.canvas = canvas; safe.content = content;
            flow.Changed += MarkDirty; Economy.Changed += MarkDirty; features.Changed += MarkDirty; features.Language.Changed += MarkDirty;
        }
        private void MarkDirty() { dirty = true; }
        private void LateUpdate()
        {
            if (features == null) return;
            if (open && UnityEngine.Time.unscaledTime >= refreshAt) { refreshAt = UnityEngine.Time.unscaledTime + 10; dirty = true; }
            if (dirty) { dirty = false; Draw(); }
        }
        private RectTransform Rect(Transform parent, string name, float w, float h, float x, float y)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); return rect;
        }
        private void Label(Transform parent, string text, float y, float x = 0, float width = 780, int size = 30)
        {
            var label = Rect(parent, "Text", width, 80, x, y).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.text = T(text); label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
        }
        private void Button(Transform parent, string text, float y, Action action, float x = 0, float width = 320, bool enabled = true)
        {
            var rect = Rect(parent, text, width, 90, x, y);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = buttonSprite; image.type = Image.Type.Sliced;
            image.color = buttonSprite ? Color.white : new Color(.16f, .4f, .6f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.interactable = enabled;
            button.onClick.AddListener(() => { action(); dirty = true; });
            Label(rect, text, 0, 0, width - 20, 25);
        }
        private void Draw()
        {
            if (menu && open && flow.Phase == FlowPhase.Home && !features.Busy) { RefreshPrefab(); return; }
            menu = null;
            if (page) { page.SetActive(false); Destroy(page); }
            page = Rect(content, "Offline features", 900, 1600, 0, 0).gameObject;
            var parent = page.transform;
            if (flow.Phase != FlowPhase.Home) open = false;
            if (features.Busy)
            {
                var cover = page.AddComponent<Image>(); cover.color = new Color(.03f, .05f, .12f, .97f);
                Label(parent, "Waiting for ad...", 0); return;
            }
            if (flow.Phase == FlowPhase.Home)
            {
                if (flow.Config.homeHubPrefab && !open) return;
                if (Economy.Enabled) Label(parent, Economy.Coins + " " + T("Coin") + "    " + Economy.Gems + " " + T("Gem"), 665, 0, 800, 24);
                if (Economy.Enabled || features.Language.Enabled) Button(parent, "MENU", 730, () => open = !open, 0, 220);
                if (open) DrawMenu(parent);
            }
            else if (flow.Phase == FlowPhase.Result && (Economy.Enabled || features.PendingSave))
            {
                if (Economy.Enabled) Label(parent, "REWARD +" + Economy.LastReward + " Coin", -570);
                if (features.PendingSave) Button(parent, "RETRY REWARD SAVE", -660, () => features.SaveResult(), 0, 680);
                else if (Economy.CanDouble && features.RewardAdsEnabled) Button(parent, "X2 REWARD", -660, () => { _ = features.RewardDouble(); }, 0, 580);
                if (!string.IsNullOrEmpty(features.Message)) Label(parent, features.Message, -750, 0, 850, 22);
            }
        }
        private void DrawMenu(Transform parent)
        {
            if (flow.Config.offlineMenuPrefab) { OpenPrefab(parent); return; }
            var panel = Rect(parent, "Menu popup", 900, 1600, 0, 0); var bg = panel.gameObject.AddComponent<Image>(); bg.color = new Color(.035f, .07f, .15f, 1f);
            Label(panel, Economy.Enabled ? Economy.Coins + " " + T("Coin") + "    " + Economy.Gems + " " + T("Gem") : "MENU", 700);
            var tabs = new System.Collections.Generic.List<string>();
            if (Economy.ShopEnabled) tabs.Add("SHOP"); if (Economy.DailyEnabled) tabs.Add("DAILY REWARD");
            if (Economy.MissionsEnabled) tabs.Add("MISSIONS"); if (Economy.Enabled) tabs.Add("INVENTORY");
            if (!tabs.Contains(tab)) tab = tabs.FirstOrDefault();
            for (int i = 0; i < tabs.Count; i++)
            {
                string choice = tabs[i]; Button(panel, choice, 570 - (i / 2) * 110, () => { tab = choice; pageIndex = 0; message = null; }, i % 2 == 0 ? -205 : 205, 390);
            }
            Label(panel, tab ?? "MENU", 330);
            var config = Economy.Settings;
            if (tab == "DAILY REWARD")
            {
                Label(panel, "DAY " + (Economy.DailyIndex + 1), 160);
                Label(panel, "+" + Economy.DailyCoins + " " + T("Coin"), 50);
                Button(panel, Economy.CanClaimDaily ? "CLAIM" : "CLAIMED", -100, () => Act(Economy.ClaimDaily(), "daily_reward", "day", (Economy.DailyIndex + 1).ToString()), enabled: Economy.CanClaimDaily);
            }
            else if (tab == "MISSIONS")
            {
                var entries = (config.dailyMissions ?? Array.Empty<DailyMission>()).Where(x => x != null).ToArray();
                Pager(panel, entries.Length);
                for (int i = pageIndex * 3; i < Math.Min(entries.Length, pageIndex * 3 + 3); i++)
                {
                    var m = entries[i]; float y = 180 - (i % 3) * 200;
                    Label(panel, T(m.title) + "  " + Math.Min(Economy.MissionProgress(m), m.target) + "/" + m.target + "  +" + m.coins + " " + T("Coin"), y, 0, 820, 25);
                    Button(panel, Economy.MissionClaimed(m.id) ? "CLAIMED" : "CLAIM", y - 85, () => Act(Economy.ClaimMission(m.id), "mission_claim", "mission_id", m.id), enabled: !Economy.MissionClaimed(m.id) && Economy.MissionProgress(m) >= m.target);
                }
            }
            else if (tab == "SHOP" || tab == "INVENTORY")
            {
                var entries = (config.items ?? Array.Empty<ShopItem>()).Where(x => x != null && (tab == "SHOP" || Economy.ItemCount(x.id) > 0)).ToArray();
                Pager(panel, entries.Length);
                if (entries.Length == 0) Label(panel, "No items", 100);
                for (int i = pageIndex * 3; i < Math.Min(entries.Length, pageIndex * 3 + 3); i++)
                {
                    var item = entries[i]; float y = 180 - (i % 3) * 200;
                    Label(panel, T(item.title) + "  x" + Economy.ItemCount(item.id), y, 0, 800);
                    if (tab == "SHOP") Button(panel, T("BUY") + " x" + item.quantity + " • " + item.price + " " + T(item.currency.ToString()), y - 85, () => Act(Economy.Buy(item.id), "shop_purchase", "item_id", item.id), width: 600);
                }
            }
            if (!string.IsNullOrEmpty(message)) Label(panel, message, -430, 0, 820, 24);
            if (features.Language.Enabled) Button(panel, features.Language.Language == "en" ? "Tiếng Việt" : "English", -570, () => { if (!features.Language.SetLanguage(features.Language.Language == "en" ? "vi" : "en")) message = features.Language.LastError; });
            Button(panel, "CLOSE", -700, () => { open = false; message = null; });
        }
        private bool TabEnabled(string key) => key == "SHOP" ? Economy.ShopEnabled : key == "DAILY REWARD" ? Economy.DailyEnabled : key == "MISSIONS" ? Economy.MissionsEnabled : key == "INVENTORY" && Economy.Enabled;
        private void OpenPrefab(Transform parent)
        {
            menu = Instantiate(flow.Config.offlineMenuPrefab, parent);
            menu.close.onClick.AddListener(() => menu.Close(() => { open = false; message = null; dirty = true; }));
            menu.language.onClick.AddListener(() => {
                bool ok = features.Language.SetLanguage(features.Language.Language == "en" ? "vi" : "en");
                message = ok ? null : features.Language.LastError; messageIsError = !ok; dirty = true;
            });
            foreach (var entry in menu.tabs)
            {
                var selected = entry;
                entry.button.onClick.AddListener(() => { tab = selected.key; pageIndex = 0; message = null; dirty = true; });
                entry.panel.previous.onClick.AddListener(() => { pageIndex = Math.Max(0, pageIndex - 1); dirty = true; });
                entry.panel.next.onClick.AddListener(() => { pageIndex++; dirty = true; });
            }
            RefreshPrefab();
        }
        private void RefreshPrefab()
        {
            var available = menu.tabs.Where(x => TabEnabled(x.key)).ToArray();
            if (!available.Any(x => x.key == tab)) { tab = available.FirstOrDefault()?.key; pageIndex = 0; }
            menu.balance.text = Economy.Enabled ? Economy.Coins + " " + T("Coin") + "    " + Economy.Gems + " " + T("Gem") : "MENU";
            menu.message.text = T(message ?? ""); menu.message.color = messageIsError ? menu.errorColor : menu.successColor;
            menu.language.gameObject.SetActive(features.Language.Enabled);
            menu.languageLabel.text = features.Language.Language == "en" ? "Tiếng Việt" : "English";
            menu.loading.SetActive(transactionBusy);
            foreach (var entry in menu.tabs)
            {
                bool enabled = TabEnabled(entry.key), active = enabled && entry.key == tab;
                entry.button.gameObject.SetActive(enabled); entry.selected.SetActive(active); entry.panel.gameObject.SetActive(active);
                if (active) FillPanel(entry.panel);
            }
        }
        private void FillPanel(OfflinePanelView panel)
        {
            foreach (Transform child in panel.rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            panel.title.text = T(panel.key); panel.empty.gameObject.SetActive(false);
            var rows = new System.Collections.Generic.List<Tuple<string, string, bool, Action>>();
            var settings = Economy.Settings;
            if (panel.key == "DAILY REWARD")
            {
                int day = Economy.DailyIndex + 1;
                rows.Add(Tuple.Create(T("DAY " + day) + "  +" + Economy.DailyCoins + " " + T("Coin"), T(Economy.CanClaimDaily ? "CLAIM" : "CLAIMED"), Economy.CanClaimDaily,
                    (Action)(() => Transact(() => Economy.ClaimDaily(), "daily_reward", "day", day.ToString()))));
            }
            else if (panel.key == "MISSIONS")
                foreach (var mission in settings.dailyMissions ?? Array.Empty<DailyMission>())
                {
                    if (mission == null) continue;
                    var m = mission; bool claimed = Economy.MissionClaimed(m.id);
                    rows.Add(Tuple.Create(T(m.title) + "  " + Math.Min(Economy.MissionProgress(m), m.target) + "/" + m.target + "  +" + m.coins + " " + T("Coin"), T(claimed ? "CLAIMED" : "CLAIM"), !claimed && Economy.MissionProgress(m) >= m.target,
                        (Action)(() => Transact(() => Economy.ClaimMission(m.id), "mission_claim", "mission_id", m.id))));
                }
            else foreach (var item in settings.items ?? Array.Empty<ShopItem>())
                {
                    if (item == null || (panel.key == "INVENTORY" && Economy.ItemCount(item.id) == 0)) continue;
                    var captured = item;
                    rows.Add(Tuple.Create(T(item.title) + "  x" + Economy.ItemCount(item.id), panel.key == "SHOP" ? T("BUY") + " x" + item.quantity + " • " + item.price + " " + T(item.currency.ToString()) : "", true,
                        (Action)(() => Transact(() => Economy.Buy(captured.id), "shop_purchase", "item_id", captured.id))));
                }
            int size = Math.Max(1, panel.pageSize), pages = Math.Max(1, (rows.Count + size - 1) / size);
            pageIndex = Mathf.Clamp(pageIndex, 0, pages - 1);
            panel.pageLabel.text = (pageIndex + 1) + " / " + pages;
            panel.previous.gameObject.SetActive(pages > 1); panel.next.gameObject.SetActive(pages > 1); panel.pageLabel.gameObject.SetActive(pages > 1);
            panel.previous.interactable = pageIndex > 0; panel.next.interactable = pageIndex + 1 < pages;
            panel.empty.gameObject.SetActive(rows.Count == 0); panel.empty.text = T("No items");
            foreach (var entry in rows.Skip(pageIndex * size).Take(size))
            {
                var row = Instantiate(panel.rowPrefab, panel.rows); row.title.text = entry.Item1;
                row.actionLabel.text = entry.Item2; row.action.name = entry.Item2;
                row.action.gameObject.SetActive(!string.IsNullOrEmpty(entry.Item2)); row.action.interactable = entry.Item3 && !transactionBusy;
                row.action.onClick.AddListener(() => entry.Item4());
            }
        }
        private void Transact(Func<bool> transaction, string name, string key, string value)
        { if (!transactionBusy) StartCoroutine(RunTransaction(transaction, name, key, value)); }
        private System.Collections.IEnumerator RunTransaction(Func<bool> transaction, string name, string key, string value)
        {
            transactionBusy = true; dirty = true;
            yield return null; // Display a blocker before writing, and suppress repeated clicks.
            try { Act(transaction(), name, key, value); }
            catch (Exception e) { message = e.Message; messageIsError = true; }
            finally { transactionBusy = false; dirty = true; }
        }
        private void Pager(Transform panel, int count)
        {
            int pages = Math.Max(1, (count + 2) / 3); pageIndex = Math.Min(pageIndex, pages - 1);
            if (pages <= 1) return;
            Button(panel, "<", -350, () => pageIndex--, -280, 110, pageIndex > 0);
            Label(panel, (pageIndex + 1) + " / " + pages, -350, 0, 250);
            Button(panel, ">", -350, () => pageIndex++, 280, 110, pageIndex + 1 < pages);
        }
        private void Act(bool success, string name, string key, string value)
        { messageIsError = !success; message = success ? "Saved" : Economy.LastError; if (success) features.Track(name, key, value); }
        private void OnDestroy()
        {
            if (flow) flow.Changed -= MarkDirty;
            if (features) { Economy.Changed -= MarkDirty; features.Changed -= MarkDirty; features.Language.Changed -= MarkDirty; }
        }
    }
}
