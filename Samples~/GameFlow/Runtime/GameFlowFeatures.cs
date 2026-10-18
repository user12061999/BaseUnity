using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseUnity.Core;
using BaseUnity.Features;
using UnityEngine;

namespace BaseUnity.GameFlow
{
    /// <summary>Optional sample policy. SDK services remain usable independently of this flow.</summary>
    public sealed class GameFlowFeatures : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private FeatureServices services;
        private GameIntegrationSettings settings;
        private string session;
        private bool pendingResult, pendingDouble, interstitialDue;
        private int wins;
        private FlowPhase previous;
        private IDisposable started, ended;
        public bool Busy { get; private set; }
        public string Message { get; private set; }
        public bool PendingSave => pendingResult || pendingDouble || Offline.PendingBonus || flow.ProgressSavePending;
        public OfflineService Offline => services.Offline;
        public LocalizationService Language => services.Localization;
        public bool RewardAdsEnabled => flow.Config && flow.Config.features && flow.Config.features.ads.enabled && flow.Config.features.ads.rewarded;
        public event Action Changed;
        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner; services = flow.Services.Get<FeatureServices>(); settings = services.GameIntegration;
            flow.Changed += FlowChanged;
            started = flow.Services.Get<EventBus>().Subscribe<SessionStarted>(e => Track("level_start", "level", e.Level.ToString()));
            ended = flow.Services.Get<EventBus>().Subscribe<SessionEnded>(e => Track("level_end", "result", e.Outcome.ToString()));
            gameObject.AddComponent<GameFlowOfflineUI>().Initialize(flow, this);
            if (Language.Enabled) gameObject.AddComponent<GameFlowLocalization>().Initialize(Language);
        }
        public void RestoreSession()
        { session = Offline.RestoredSession; SaveResult(); }
        public bool LeaveResult()
        {
            if (!SaveResult()) return false;
            if (flow.Phase != FlowPhase.Result || Offline.AcknowledgeResult()) return true;
            Message = Offline.LastError; Changed?.Invoke(); return false;
        }
        public void BeginSession() { session = Offline.BeginSession(); Message = null; }
        public void CompleteSession()
        {
            pendingResult = Offline.UsesUnifiedProgress;
            if (flow.Outcome == LevelOutcome.Win && settings.interstitialBetweenLevels)
                interstitialDue = ++wins % Math.Max(1, settings.interstitialEveryWins) == 0;
            SaveResult();
        }
        // A failed save keeps the result screen open and can be retried without granting twice.
        public bool SaveResult()
        {
            if (!Offline.UsesUnifiedProgress) flow.SaveProgress();
            if (pendingResult && Offline.FinishSession(session, flow.CurrentLevel, flow.Outcome == LevelOutcome.Win, flow.Score, levelCount: flow.Config.LevelCount))
            { pendingResult = false; flow.ApplyOfflineProgress(); }
            if (pendingDouble && Offline.RecordRewardedCompletion(session) && Offline.RecoverPendingBonus()) pendingDouble = false;
            if (Offline.PendingBonus && Offline.RecoverPendingBonus()) pendingDouble = false;
            Message = PendingSave ? "Reward save failed. Try again." : null;
            Changed?.Invoke();
            return !PendingSave;
        }
        public async Task RewardDouble()
        {
            if (Busy || flow.Phase != FlowPhase.Result || !RewardAdsEnabled || !Offline.CanDouble) return;
            if (PendingSave) { SaveResult(); return; }
            Busy = true; Message = "Waiting for ad..."; Changed?.Invoke();
            string captured = session;
            try
            {
                bool ready = services.Ads.IsReady(AdFormat.Rewarded) || await services.Ads.LoadAsync(AdFormat.Rewarded);
                if (!this || captured != session || flow.Phase != FlowPhase.Result) return;
                var result = ready ? await services.Ads.ShowAsync(AdFormat.Rewarded) : AdResult.Unavailable;
                if (!this || captured != session || flow.Phase != FlowPhase.Result) return;
                if (result == AdResult.Rewarded) { pendingDouble = true; SaveResult(); Track("rewarded_bonus", "level", flow.CurrentLevel.ToString()); }
                else Message = "Ad unavailable";
            }
            catch (Exception e) { if (this) Message = e.Message; }
            finally { if (this) { Busy = false; Changed?.Invoke(); } }
        }
        public async Task BeforeNextLevel()
        {
            if (!interstitialDue) return;
            interstitialDue = false;
            // Only show an already loaded ad at this natural break; never delay an offline start to load one.
            if (!services.Ads.IsReady(AdFormat.Interstitial)) return;
            Busy = true; Changed?.Invoke();
            try { await services.Ads.ShowAsync(AdFormat.Interstitial); }
            finally { if (this) { Busy = false; Changed?.Invoke(); } }
        }
        private void FlowChanged()
        {
            if (previous == flow.Phase) return;
            previous = flow.Phase;
            if (flow.Phase == FlowPhase.Home)
            {
                if (settings.fetchRemoteConfigAtHome) _ = services.Firebase.FetchRemoteConfigAsync();
                if (settings.bannerAtHome) _ = ShowHomeBanner();
            }
            else services.Ads.HideBanner();
            if (flow.Phase == FlowPhase.Playing && settings.interstitialBetweenLevels) _ = services.Ads.LoadAsync(AdFormat.Interstitial);
        }
        private async Task ShowHomeBanner()
        {
            if (await services.Ads.LoadAsync(AdFormat.Banner) && this && flow.Phase == FlowPhase.Home)
            {
                await services.Ads.ShowAsync(AdFormat.Banner);
                if (this && flow.Phase != FlowPhase.Home) services.Ads.HideBanner();
            }
        }
        public async void Track(string name, string key, string value)
        {
            if (!settings.analyticsEvents) return;
            if (await services.Firebase.InitializeAsync() && this)
                services.Firebase.LogEvent(name, new Dictionary<string, string> { { key, value } });
        }
        private void OnDestroy()
        {
            if (flow) flow.Changed -= FlowChanged;
            started?.Dispose(); ended?.Dispose();
        }
    }
}
