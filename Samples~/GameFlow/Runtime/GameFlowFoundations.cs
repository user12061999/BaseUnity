using System;
using System.Linq;
using BaseUnity.Features;
using UnityEngine;
namespace BaseUnity.GameFlow
{
    public sealed class GameFlowFoundations : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private FeatureServices services;
        private GameplayFoundationView view;
        private bool opened, focused = true;
        public bool IsOpen => opened;
        private int page, tab, previousLevel;
        private float refreshAt, flushAt;
        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner; services = owner.Services.Get<FeatureServices>(); previousLevel = services.Gameplay.PlayerLevel;
            if (owner.Config.foundationPrefab)
            {
                view = Instantiate(owner.Config.foundationPrefab, transform);
                view.open.onClick.AddListener(() => { opened = true; page = tab = 0; Refresh(); });
                view.close.onClick.AddListener(() => { opened = false; Refresh(); });
                view.gifts.onClick.AddListener(() => Select(0)); view.achievements.onClick.AddListener(() => Select(1)); view.settings.onClick.AddListener(() => Select(2));
                view.previous.onClick.AddListener(() => { page = Math.Max(0, page-1); Refresh(); }); view.next.onClick.AddListener(() => { page++; Refresh(); });
            }
            flow.Changed += Refresh; services.Offline.Changed += Refresh; services.Preferences.Changed += Refresh;
            services.Gameplay.Visit(); Refresh();
        }
        private void Select(int value) { tab = value; page = 0; Refresh(); }
        private void Update()
        {
            if (flow && flow.Phase == FlowPhase.Playing && focused) services.Gameplay.TickPlaytime(UnityEngine.Time.unscaledDeltaTime);
            if (focused && UnityEngine.Time.unscaledTime >= flushAt) { flushAt = UnityEngine.Time.unscaledTime + 30; services.Gameplay.Heartbeat(); }
            if (UnityEngine.Time.unscaledTime >= refreshAt) { refreshAt = UnityEngine.Time.unscaledTime + .5f; Refresh(); }
        }
        private void OnApplicationFocus(bool focus) { focused = focus; if (!focus) services?.Gameplay.Heartbeat(); else services?.Gameplay.Visit(); }
        private void OnApplicationPause(bool paused) { focused = !paused; if (paused) services?.Gameplay.Heartbeat(); else services?.Gameplay.Visit(); }
        private void Row(int index, string label, bool interactable, Action action)
        {
            view.rows[index].gameObject.SetActive(true); view.rowLabels[index].text = label; view.rows[index].interactable = interactable;
            view.rows[index].onClick.RemoveAllListeners(); if (action != null) view.rows[index].onClick.AddListener(() => { action(); Refresh(); });
        }
        private void Refresh()
        {
            if (!flow || services == null) return;
            int giftCount = services.Gameplay.Gifts.Count(x => services.Gameplay.CanClaim(x.id));
            var goals = services.Offline.Settings.achievementGoals ?? Array.Empty<DailyMission>();
            int achievementCount = services.Offline.AchievementsEnabled ? goals.Count(x => x != null && !services.Offline.AchievementClaimed(x.id) && services.Offline.AchievementProgress(x) >= x.target) : 0;
            services.Notices.Set("gifts", services.Gameplay.BadgesEnabled ? giftCount : 0); services.Notices.Set("achievements", services.Gameplay.BadgesEnabled ? achievementCount : 0);
            int level = services.Gameplay.PlayerLevel;
            if (level > previousLevel && services.Gameplay.ProgressionEnabled) services.Feedback.Toast("Player level " + level + " unlocked!");
            previousLevel = level;
            if (!view) return;
            bool available = services.Gameplay.Enabled || services.Preferences.Enabled;
            var art = flow.GetComponentInChildren<GameFlowView>(); var fallback = flow.GetComponent<GameFlowUI>();
            bool otherModal = flow.GetComponent<GameFlowOfflineUI>()?.IsOpen == true || flow.GetComponent<GameFlowMobileModules>()?.IsMenuOpen == true || flow.GetComponent<GameFlowMobileModules>()?.IsPopupVisible == true;
            bool home = !otherModal && available && flow.Phase == FlowPhase.Home && !(art && art.SettingsOpen) && !(fallback && fallback.SettingsOpen);
            if (!home) opened = false;
            view.home.SetActive(home && !opened); view.modal.SetActive(home && opened);
            view.status.text = (services.Gameplay.EnergyEnabled ? "ENERGY " + services.Gameplay.Energy + "/" + services.Gameplay.Settings.maximumEnergy + "  " + Mathf.CeilToInt((float)services.Gameplay.RefillRemaining) + "s  " : "") + (services.Gameplay.ProgressionEnabled ? "PLAYER LV " + level + "  XP " + services.Gameplay.Experience : "");
            int notice = services.Notices.Count("gifts") + services.Notices.Count("achievements"); view.badge.gameObject.SetActive(notice > 0); view.badge.text = notice.ToString();
            view.gifts.gameObject.SetActive(services.Gameplay.GiftsEnabled); view.achievements.gameObject.SetActive(services.Offline.AchievementsEnabled); view.settings.gameObject.SetActive(services.Preferences.Enabled);
            if (!opened) return;
            if (tab == 0 && !services.Gameplay.GiftsEnabled) tab = services.Offline.AchievementsEnabled ? 1 : 2;
            if (tab == 1 && !services.Offline.AchievementsEnabled) tab = services.Preferences.Enabled ? 2 : 0;
            if (tab == 2 && !services.Preferences.Enabled) tab = 0;
            foreach (var row in view.rows) row.gameObject.SetActive(false);
            int count = tab == 0 ? services.Gameplay.Gifts.Length : tab == 1 ? goals.Length : 6;
            int pages = Math.Max(1, (count + view.rows.Length - 1) / view.rows.Length); page = Math.Min(page, pages-1);
            view.previous.interactable = page > 0; view.next.interactable = page < pages-1; view.pageNumber.text = (page+1) + " / " + pages;
            view.title.text = tab == 0 ? "GIFTS" : tab == 1 ? "ACHIEVEMENTS" : "ADVANCED SETTINGS";
            view.message.text = services.Gameplay.LastError ?? services.Preferences.LastError ?? services.Offline.LastError ?? "";
            for (int i = 0; i < view.rows.Length && page * view.rows.Length + i < count; i++)
            {
                int index = page * view.rows.Length + i;
                if (tab == 0) { var gift = services.Gameplay.Gifts[index]; Row(i, gift.title + "  +" + gift.coins + " Coin", services.Gameplay.CanClaim(gift.id), () => services.Gameplay.Claim(gift.id)); }
                else if (tab == 1) { var goal = goals[index]; if (goal == null) continue; bool claimed = services.Offline.AchievementClaimed(goal.id); Row(i, goal.title + "  " + services.Offline.AchievementProgress(goal) + "/" + goal.target + (claimed ? "  CLAIMED" : "  +" + goal.coins), !claimed && services.Offline.AchievementProgress(goal) >= goal.target, () => services.Offline.ClaimAchievement(goal.id)); }
                else SettingsRow(i, index);
            }
        }
        private void SettingsRow(int row, int index)
        {
            var p = services.Preferences.Current;
            string[] labels = { "FPS: " + p.frameRate, "QUALITY: " + (p.quality < 0 ? "PROJECT DEFAULT" : QualitySettings.names[Mathf.Clamp(p.quality,0,QualitySettings.names.Length-1)]), "BATTERY SAVER: " + p.batterySaver, "REDUCED MOTION: " + p.reducedMotion, "GAMEPLAY EFFECTS: " + p.gameplayEffects, "VIBRATION: toggle" };
            Row(row, labels[index], index != 5 || services.Haptics.ModuleEnabled, () => {
                if (index == 5) { services.Haptics.SetUserEnabled(!services.Haptics.Enabled); return; }
                var next = services.Preferences.Current;
                if (index == 0) next.frameRate = next.frameRate == 30 ? 60 : next.frameRate == 60 ? 120 : 30;
                else if (index == 1) next.quality = next.quality + 1 >= QualitySettings.names.Length ? -1 : next.quality + 1;
                else if (index == 2) next.batterySaver = !next.batterySaver;
                else if (index == 3) next.reducedMotion = !next.reducedMotion;
                else next.gameplayEffects = !next.gameplayEffects;
                services.Preferences.Save(next);
            });
        }
        private void OnApplicationQuit() { services?.Gameplay.Heartbeat(); }
        private void OnDestroy()
        {
            if (flow) flow.Changed -= Refresh;
            if (services != null) { services.Offline.Changed -= Refresh; services.Preferences.Changed -= Refresh; }
        }
    }
}
