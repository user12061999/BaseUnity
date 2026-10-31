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
        private FoundationDetailsView details;
        private bool adBusy;
        private string pendingEnergyReceipt, energyMessage;
        private int pendingLevel;
        private long pendingLevelReward;
        private bool opened, focused = true;
        public bool IsOpen => opened || (details && details.levelPopup.activeSelf);
        private string T(string text) => services.Localization.Text(text ?? "");
        private string F(string text, params object[] values) => string.Format(T(text),values);
        private static string Duration(double seconds) { var t=TimeSpan.FromSeconds(Math.Min(315360000,Math.Max(0,seconds))); return ((int)t.TotalHours).ToString("00")+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00"); }
        public void OpenEnergy() { opened=true; tab=3; page=0; Refresh(); }
        private int page, tab, previousLevel;
        private float refreshAt, flushAt;
        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner; services = owner.Services.Get<FeatureServices>(); previousLevel = services.Gameplay.PlayerLevel;
            if (owner.Config.foundationPrefab)
            {
                view = Instantiate(owner.Config.foundationPrefab, transform);
                view.open.onClick.AddListener(() => { opened = true; page = tab = 0; Refresh(); });
                view.close.onClick.AddListener(() => { if(adBusy) return; opened = false; Refresh(); });
                view.gifts.onClick.AddListener(() => Select(0)); view.achievements.onClick.AddListener(() => Select(1)); view.settings.onClick.AddListener(() => Select(2));
                details=view.GetComponent<FoundationDetailsView>();
                if(details) {
                    var popupArea=details.levelPopup.GetComponent<GameFlowSafeArea>(); if(popupArea) popupArea.canvas=view.GetComponent<Canvas>();
                    details.energy.onClick.AddListener(OpenEnergy); details.profile.onClick.AddListener(()=>Select(4)); details.controls.onClick.AddListener(()=>Select(5));
                    details.closeLevel.onClick.AddListener(()=> { details.levelPopup.SetActive(false); Refresh(); });
                    details.saveName.onClick.AddListener(()=> { var next=services.PlayerPreferences.Current; next.playerName=details.playerName.text; if(services.PlayerPreferences.Save(next)) services.Feedback.Toast(T("Name saved")); Refresh(); });
                    details.avatar.onClick.AddListener(()=> { var next=services.PlayerPreferences.Current; next.avatar=(next.avatar+1)%4; services.PlayerPreferences.Save(next); Refresh(); });
                }
                view.previous.onClick.AddListener(() => { page = Math.Max(0, page-1); Refresh(); }); view.next.onClick.AddListener(() => { page++; Refresh(); });
            }
            flow.Changed += Refresh; services.Offline.Changed += Refresh; services.Preferences.Changed += Refresh; services.Localization.Changed += Refresh; services.PlayerPreferences.Changed += Refresh;
            services.Gameplay.Visit(); Refresh();
        }
        private void Select(int value) { if(adBusy) return; if(value==4 && details) details.playerName.text=services.PlayerPreferences.Current.playerName; tab = value; page = 0; Refresh(); }
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
            view.rows[index].gameObject.SetActive(true); view.rowLabels[index].text = T(label); view.rows[index].interactable = interactable;
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
            if (level > previousLevel && services.Gameplay.ProgressionEnabled) { pendingLevel=level; pendingLevelReward+=(long)(level-previousLevel)*services.Gameplay.Settings.levelUpCoins; services.Feedback.Toast(F("Player level {0} unlocked!",level)); }
            previousLevel = level;
            if (!view) return;
            bool available = services.Gameplay.Enabled || services.Preferences.Enabled;
            var art = flow.GetComponentInChildren<GameFlowView>(); var fallback = flow.GetComponent<GameFlowUI>();
            bool otherModal = flow.GetComponent<GameFlowOfflineUI>()?.IsOpen == true || flow.GetComponent<GameFlowMobileModules>()?.IsMenuOpen == true || flow.GetComponent<GameFlowMobileModules>()?.IsPopupVisible == true;
            bool home = !otherModal && available && flow.Phase == FlowPhase.Home && !(art && art.SettingsOpen) && !(fallback && fallback.SettingsOpen);
            bool energyContext = tab==3 && services.Gameplay.EnergyEnabled && (flow.Phase==FlowPhase.Result || flow.Phase==FlowPhase.LevelSelect);
            if (!home && !energyContext && !adBusy) opened = false;
            if(details && pendingLevel>0 && !opened && !otherModal && (flow.Phase==FlowPhase.Home || flow.Phase==FlowPhase.Result)) { details.levelMessage.text=F("LEVEL {0}\nLevel reward: +{1} Coin",pendingLevel,pendingLevelReward); details.levelPopup.SetActive(true); pendingLevel=0; pendingLevelReward=0; }
            view.home.SetActive(home && !IsOpen); view.modal.SetActive((home || energyContext || adBusy) && opened);
            view.close.interactable=!adBusy;
            foreach(var button in new[]{view.gifts,view.achievements,view.settings}) button.interactable=!adBusy;
            view.open.GetComponentInChildren<UnityEngine.UI.Text>().text=T("GIFTS / PROFILE"); view.close.GetComponentInChildren<UnityEngine.UI.Text>().text=T("CLOSE");
            view.gifts.GetComponentInChildren<UnityEngine.UI.Text>().text=T("GIFTS"); view.achievements.GetComponentInChildren<UnityEngine.UI.Text>().text=T("ACHIEVEMENTS"); view.settings.GetComponentInChildren<UnityEngine.UI.Text>().text=T("SETTINGS");
            if(details) {
                details.energy.gameObject.SetActive(services.Gameplay.EnergyEnabled); details.profile.gameObject.SetActive(services.Gameplay.ProgressionEnabled); details.controls.gameObject.SetActive(services.Gameplay.ControlsEnabled);
                details.energy.interactable=details.profile.interactable=details.controls.interactable=!adBusy;
                details.closeLevel.GetComponentInChildren<UnityEngine.UI.Text>().text=T("OK");
                details.energy.GetComponentInChildren<UnityEngine.UI.Text>().text=T("ENERGY"); details.profile.GetComponentInChildren<UnityEngine.UI.Text>().text=T("PROFILE"); details.controls.GetComponentInChildren<UnityEngine.UI.Text>().text=T("CONTROLS");
                details.profilePanel.SetActive(view.modal.activeSelf && tab==4);
            }
            view.status.text = (services.Gameplay.EnergyEnabled ? F("ENERGY {0}/{1}",services.Gameplay.Energy,services.Gameplay.Settings.maximumEnergy) + "  " + Duration(services.Gameplay.RefillRemaining) + "\n" : "") + (services.Gameplay.ProgressionEnabled ? F("PLAYER LV {0}  XP {1}",level,services.Gameplay.Experience) : "");
            int notice = services.Notices.Count("gifts") + services.Notices.Count("achievements"); view.badge.gameObject.SetActive(notice > 0); view.badge.text = notice.ToString();
            view.gifts.gameObject.SetActive(services.Gameplay.GiftsEnabled); view.achievements.gameObject.SetActive(services.Offline.AchievementsEnabled); view.settings.gameObject.SetActive(services.Preferences.Enabled);
            if (!opened) return;
            if (tab == 0 && !services.Gameplay.GiftsEnabled) tab = services.Offline.AchievementsEnabled ? 1 : 2;
            if (tab == 1 && !services.Offline.AchievementsEnabled) tab = services.Preferences.Enabled ? 2 : 0;
            if (tab == 2 && !services.Preferences.Enabled) tab = 0;
            foreach (var row in view.rows) row.gameObject.SetActive(false);
            int count = tab == 0 ? services.Gameplay.Gifts.Length : tab == 1 ? goals.Length : tab==4 ? 0 : 6;
            int pages = Math.Max(1, (count + view.rows.Length - 1) / view.rows.Length); page = Math.Min(page, pages-1);
            view.previous.interactable = page > 0; view.next.interactable = page < pages-1; view.pageNumber.text = (page+1) + " / " + pages;
            view.title.text = T(tab == 0 ? "GIFTS" : tab == 1 ? "ACHIEVEMENTS" : tab==2 ? "ADVANCED SETTINGS" : tab==3 ? (services.Gameplay.CanStartRun ? "ENERGY" : "Not enough energy") : tab==4 ? "PROFILE" : "CONTROLS");
            view.message.text = T(energyMessage ?? services.PlayerPreferences.LastError ?? services.Gameplay.LastError ?? services.Preferences.LastError ?? services.Offline.LastError ?? "");
            if(tab==4 && details) { var player=services.PlayerPreferences.Current; details.profileTitle.text=F("PLAYER LV {0}  XP {1}",level,services.Gameplay.Experience); details.nameCaption.text=T("PLAYER NAME"); details.avatarLabel.text=(player.avatar+1).ToString(); details.avatarBackground.color=FoundationDetailsView.AvatarTint(player.avatar); details.experience.text=level>=services.Gameplay.Settings.maximumPlayerLevel?T("MAX LEVEL"):F("EXP {0}/{1}",services.Gameplay.Experience%Math.Max(1,services.Gameplay.Settings.experiencePerLevel),services.Gameplay.Settings.experiencePerLevel); details.experienceFill.fillAmount=services.Gameplay.ExperienceFraction; details.saveName.GetComponentInChildren<UnityEngine.UI.Text>().text=T("SAVE NAME"); }
            for (int i = 0; i < view.rows.Length && page * view.rows.Length + i < count; i++)
            {
                int index = page * view.rows.Length + i;
                if (tab == 0) { var gift = services.Gameplay.Gifts[index]; Row(i, T(gift.title) + "  +" + gift.coins + " " + T("Coin") + (services.Gameplay.EnergyEnabled && gift.energy>0 ? F(" +{0} energy",gift.energy) : "") + "\n" + GiftStatus(gift), services.Gameplay.CanClaim(gift.id), () => { if(services.Gameplay.Claim(gift.id)) RewardFeedback(gift.coins,services.Gameplay.EnergyEnabled?gift.energy:0); }); }
                else if (tab == 1) { var goal = goals[index]; if (goal == null) continue; bool claimed = services.Offline.AchievementClaimed(goal.id); Row(i, T(goal.title) + "  " + services.Offline.AchievementProgress(goal) + "/" + goal.target + (claimed ? "  " + T("CLAIMED") : "  +" + goal.coins + "\n" + T(services.Offline.AchievementProgress(goal)>=goal.target ? "CLAIM" : "IN PROGRESS")), !claimed && services.Offline.AchievementProgress(goal) >= goal.target, () => { if(services.Offline.ClaimAchievement(goal.id)) RewardFeedback(goal.coins); }); }
                else if(tab==2) SettingsRow(i, index);
                else if(tab==3) EnergyRow(i,index);
                else if(tab==5) ControlRow(i,index);
            }
        }
        private string GiftStatus(GiftDefinition gift)
        {
            if(services.Gameplay.CanClaim(gift.id)) return T("CLAIM");
            if(services.Gameplay.GiftClaimed(gift.id)) return T("CLAIMED");
            return gift.kind==GiftKind.Return ? F("Return after {0} away",Duration(gift.seconds)) : F("Available in {0}",Duration(services.Gameplay.GiftRemaining(gift)));
        }
        private void RewardFeedback(long coins, int energy=0)
        {
            services.Feedback.Toast(T("Reward received"));
            if(view.effects) view.effects.Show((coins>0?"+"+coins+" "+T("Coin"):"")+(energy>0?F(" +{0} energy",energy):""),new Vector2(Screen.width*.5f,Screen.height*.5f),Color.yellow,new Vector2(Screen.width*.5f,Screen.height*.9f));
        }
        private void EnergyRow(int row,int index)
        {
            if(index==0) Row(row,F("ENERGY {0}/{1}",services.Gameplay.Energy,services.Gameplay.Settings.maximumEnergy),false,null);
            else if(index==1) Row(row,services.Gameplay.RefillRemaining<=0 ? T("ENERGY FULL") : F("Next energy in {0}",Duration(services.Gameplay.RefillRemaining)),false,null);
            else if(index==2) Row(row,F("Run cost: {0} energy",services.Gameplay.Settings.energyPerRun),false,null);
            else if(index==3) { bool available=services.Gameplay.RewardedEnergyEnabled && services.Ads.Enabled && services.Consent.CanRequestAds; Row(row,T(adBusy ? "Waiting for ad..." : pendingEnergyReceipt!=null ? "Retry saving energy" : "WATCH AD FOR ENERGY"),!adBusy && (pendingEnergyReceipt!=null || (available && services.Gameplay.Energy<services.Gameplay.Settings.maximumEnergy)),()=>{ _=RewardEnergy(); }); }
        }
        private async System.Threading.Tasks.Task RewardEnergy()
        {
            if(adBusy) return; adBusy=true; energyMessage=null; Refresh();
            try {
                if(pendingEnergyReceipt==null) {
                    bool ready=services.Ads.IsReady(AdFormat.Rewarded) || await services.Ads.LoadAsync(AdFormat.Rewarded);
                    var result=ready ? await services.Ads.ShowAsync(AdFormat.Rewarded) : AdResult.Unavailable;
                    if(!this) return;
                    if(result!=AdResult.Rewarded) { energyMessage=result==AdResult.Closed ? "Ad closed without reward" : "Ad unavailable. Try again."; return; }
                    pendingEnergyReceipt=Guid.NewGuid().ToString("N");
                }
                if(services.Gameplay.GrantVerifiedEnergy(pendingEnergyReceipt)) { pendingEnergyReceipt=null; energyMessage="Energy received"; services.Feedback.Toast(T(energyMessage)); }
                else energyMessage="Could not save. Try again.";
            } catch(Exception) { if(this) energyMessage="Ad unavailable. Try again."; }
            finally { adBusy=false; if(this) Refresh(); }
        }
        private void ControlRow(int row,int index)
        {
            var p=services.PlayerPreferences.Current;
            string[] labels={F("Control size: {0}%",Mathf.RoundToInt(p.controlScale*100)),F("Sensitivity: {0}",p.sensitivity),F("Dead zone: {0}%",Mathf.RoundToInt(p.deadZone*100)),T(p.leftHanded ? "LEFT HANDED" : "RIGHT HANDED"),F("Joystick offset: {0}, {1}",p.joystickOffset.x,p.joystickOffset.y),F("Action offset: {0}, {1}",p.actionOffset.x,p.actionOffset.y)};
            Row(row,labels[index],true,()=> { var n=services.PlayerPreferences.Current; if(index==0) n.controlScale=n.controlScale>=1.19f?.8f:n.controlScale+.2f; else if(index==1) n.sensitivity=n.sensitivity>=1.99f?.5f:n.sensitivity+.5f; else if(index==2) n.deadZone=n.deadZone>=.29f?.05f:n.deadZone+.05f; else if(index==3) n.leftHanded=!n.leftHanded; else { var offset=index==4?n.joystickOffset:n.actionOffset; offset=offset==Vector2.zero?new Vector2(-60,0):offset.x<0?new Vector2(0,80):offset.y>0?new Vector2(60,0):Vector2.zero; if(index==4)n.joystickOffset=offset; else n.actionOffset=offset; } services.PlayerPreferences.Save(n); });
        }
        private void SettingsRow(int row, int index)
        {
            var p = services.Preferences.Current;
            string[] labels = { "FPS: " + p.frameRate, T("QUALITY") + ": " + (p.quality < 0 ? T("PROJECT DEFAULT") : QualitySettings.names[Mathf.Clamp(p.quality,0,QualitySettings.names.Length-1)]), T("BATTERY SAVER") + ": " + T(p.batterySaver?"ON":"OFF"), T("REDUCED MOTION") + ": " + T(p.reducedMotion?"ON":"OFF"), T("GAMEPLAY EFFECTS") + ": " + T(p.gameplayEffects?"ON":"OFF"), T(services.Haptics.Enabled?"HAPTICS ON":"HAPTICS OFF") };
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
            if (services != null) { services.Offline.Changed -= Refresh; services.Preferences.Changed -= Refresh; services.Localization.Changed -= Refresh; services.PlayerPreferences.Changed -= Refresh; }
        }
    }
}
