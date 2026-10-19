using System;
using System.Collections;
using System.Threading.Tasks;
using BaseUnity.Core;
using BaseUnity.Input;
using BaseUnity.Save;
using BaseUnity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BaseUnity.GameFlow
{
    public enum FlowPhase { Boot, Loading, Home, Playing, Paused, Result, Error, LevelSelect }
    public enum LevelOutcome { None, Win, Lose }
    public readonly struct SessionStarted
    {
        public readonly int Level;
        public SessionStarted(int level) { Level = level; }
    }
    public readonly struct SessionEnded
    {
        public readonly int Score;
        public readonly int Level;
        public readonly LevelOutcome Outcome;
        public SessionEnded(int score) : this(score, 1, LevelOutcome.Win) { }
        public SessionEnded(int score, int level, LevelOutcome outcome) { Score = score; Level = level; Outcome = outcome; }
    }

    public sealed class GameFlowBootstrap : GameBootstrap
    {
        [Serializable] public sealed class ProgressData : BaseUnity.Features.PlayerProgress { }
        [SerializeField] private GameFlowConfig config;
        public GameFlowConfig Config { get => config; set => config = value; }
        protected override BaseUnity.Features.FeatureConfig GetFeatureConfig() => config && config.features ? config.features : base.GetFeatureConfig();
        public FlowPhase Phase { get; private set; } = FlowPhase.Boot;
        public int Score { get; private set; }
        public int CurrentLevel { get; private set; } = 1;
        public GameLevel CurrentDefinition => config ? config.GetLevel(CurrentLevel) : null;
        public LevelOutcome Outcome { get; private set; }
        public string OutcomeReason { get; private set; }
        public float RemainingTime { get; private set; }
        public bool HasTimeLimit => CurrentDefinition != null && CurrentDefinition.timeLimitSeconds > 0;
        public bool CanNextLevel => Phase == FlowPhase.Result && Outcome == LevelOutcome.Win && config && CurrentLevel < config.LevelCount;
        public int HomeLevel => config ? Mathf.Clamp(Progress.highestUnlockedLevel, 1, Math.Max(1, config.LevelCount)) : 1;
        public ProgressData Progress { get; private set; } = new ProgressData();
        public float LoadingProgress { get; private set; }
        public string ErrorMessage { get; private set; }
        public string SaveWarning { get; private set; }
        public bool ProgressSavePending { get; private set; }
        public event Action Changed;
        private float resumeScale = 1;
        private float originalScale;
        private bool loading, dataReady;
        public LevelManager Levels { get; private set; }
        public GameFlowFeatures Features { get; private set; }
        public bool IsLevelUnlocked(int number) => config && number >= 1 && number <= config.LevelCount && number <= HomeLevel;
        public bool IsLevelCompleted(int number) => IsLevelUnlocked(number) && number <= Progress.highestCompletedLevel;
        public void ShowLevels() { if (!loading && Phase == FlowPhase.Home) SetPhase(FlowPhase.LevelSelect); }
        public void CloseLevels() { if (Phase == FlowPhase.LevelSelect) SetPhase(FlowPhase.Home); }

        protected override void OnReady()
        {
            originalScale = UnityEngine.Time.timeScale;
            Levels = gameObject.AddComponent<LevelManager>();
            Levels.Initialize(this);
            if (config && config.uiPrefab)
                Instantiate(config.uiPrefab, transform).Initialize(this);
            else gameObject.AddComponent<GameFlowUI>().Initialize(this);
            Features = gameObject.AddComponent<GameFlowFeatures>();
            Features.Initialize(this);
            gameObject.AddComponent<GameFlowMobileModules>().Initialize(this);
            if (config && config.homeHubPrefab) gameObject.AddComponent<GameFlowHub>().Initialize(this);
            if (config && config.inGameHUDPrefab) gameObject.AddComponent<GameFlowInGameHUD>().Initialize(this);
            gameObject.AddComponent<GameFlowUIFeedback>().Initialize(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<GameFlowDebugPanel>().Initialize(this);
#endif
            StartCoroutine(BootAfterFirstFrame());
        }

        private IEnumerator BootAfterFirstFrame()
        {
            yield return null;
            if (!config) { Fail("Assign a GameFlowConfig to the Bootstrap scene."); yield break; }
            try
            {
                ValidateLevels();
                if (Features.Offline.LoadError != null) throw new InvalidOperationException(Features.Offline.LoadError);
                if (!Features.Offline.HasProgress)
                {
                    var store = Services.Get<ISaveStore>(); ProgressData saved;
                    if (store is JsonSaveStore json)
                    { if (json.LoadValidated(config.saveSlot, out saved, p => p.sessions >= 0 && p.bestScore >= 0) != SaveLoadState.Missing) Progress = saved; }
                    else if (store.TryLoad(config.saveSlot, out saved)) Progress = saved;
                    NormalizeProgress();
                    if (Features.Offline.UsesUnifiedProgress && !Features.Offline.InitializeProgress(Progress)) throw new InvalidOperationException(Features.Offline.LastError);
                }
                if (Features.Offline.UsesUnifiedProgress) ApplyOfflineProgress();
                NormalizeProgress(); dataReady = true;
            }
            catch (Exception error) { Fail(error.Message); yield break; }
            _ = OpenInitialScene();
        }
        public void ApplyOfflineProgress()
        { Progress = JsonUtility.FromJson<ProgressData>(JsonUtility.ToJson(Features.Offline.Progress)); ProgressSavePending = false; }
        private async Task OpenInitialScene()
        {
            await Transition(config.homeScene, FlowPhase.Home);
            if (!this || Phase != FlowPhase.Home) return;
            var saved = Features.Offline.PendingResult;
            if (saved == null) return;
            if (saved.level > config.LevelCount) { Fail("Saved result references a removed level. Migrate the level catalog before continuing."); return; }
            CurrentLevel = saved.level; Score = saved.score; Outcome = saved.won ? LevelOutcome.Win : LevelOutcome.Lose;
            OutcomeReason = "Restored result"; Features.RestoreSession(); UnityEngine.Time.timeScale = 0;
            SetPhase(FlowPhase.Result);
        }
        public void GoHome() { if (dataReady && !loading && config && !Features.Busy && Features.LeaveResult()) _ = Transition(config.homeScene, FlowPhase.Home); }
        public void Play()
        {
            if (Phase == FlowPhase.Home) StartLevel(HomeLevel);
            else if (Phase == FlowPhase.Result) RetryLevel();
        }

        public void StartLevel(int number)
        {
            if (!dataReady || Features.Busy || loading || (Phase != FlowPhase.Home && Phase != FlowPhase.Result && Phase != FlowPhase.LevelSelect) || !config) return;
            if (number < 1 || number > config.LevelCount || number > HomeLevel) return;
            var definition = config.GetLevel(number);
            if (definition == null) { Fail("Level " + number + " has no configuration."); return; }
            if (!Features.LeaveResult()) return;
            CurrentLevel = number;
            Score = 0;
            Outcome = LevelOutcome.None;
            Features.BeginSession();
            OutcomeReason = null;
            SaveWarning = null;
            RemainingTime = Math.Max(0, definition.timeLimitSeconds);
            _ = StartWithAds(string.IsNullOrWhiteSpace(definition.sceneOverride) ? config.inGameScene : definition.sceneOverride);
        }
        private async Task StartWithAds(string scene)
        {
            loading = true;
            await Features.BeforeNextLevel();
            if (!this || !IsReady) return;
            await Transition(scene, FlowPhase.Playing);
        }
        public void RetryLevel() { if (Phase == FlowPhase.Result) StartLevel(CurrentLevel); }
        public void NextLevel() { if (CanNextLevel) StartLevel(CurrentLevel + 1); }

        private void ValidateLevels()
        {
            if (config.LevelCount == 0) throw new InvalidOperationException("Configure at least one level in GameFlowConfig.");
            for (int number = 1; number <= config.LevelCount; number++)
            {
                var level = config.GetLevel(number);
                if (level == null || level.targetScore < 1 || float.IsNaN(level.timeLimitSeconds) || float.IsInfinity(level.timeLimitSeconds) || level.timeLimitSeconds < 0)
                    throw new InvalidOperationException("Levels need a positive target score and a non-negative finite time limit.");
            }
        }
        private void NormalizeProgress()
        {
            // Additive v1 save migration: older saves contain only score/session fields.
            Progress.highestCompletedLevel = Mathf.Clamp(Progress.highestCompletedLevel, 0, config.LevelCount);
            Progress.highestUnlockedLevel = Mathf.Clamp(Math.Max(Progress.highestUnlockedLevel, Progress.highestCompletedLevel + 1), 1, config.LevelCount);
            Progress.wins = Math.Max(0, Progress.wins); Progress.losses = Math.Max(0, Progress.losses);
        }

        private async Task Transition(string scene, FlowPhase destination)
        {
            loading = true;
            Levels.Clear();
            LoadingProgress = 0;
            ErrorMessage = null;
            UnityEngine.Time.timeScale = 1;
            SetPhase(FlowPhase.Loading);
            try
            {
                if (string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene))
                    throw new InvalidOperationException($"Scene is missing or disabled in the active Build Profile: {scene}");
                await Services.Get<SceneService>().LoadAsync(scene, LoadSceneMode.Single, new LoadProgress(this), destroyCancellationToken);
                if (!this || !IsReady) return;
                if (destination == FlowPhase.Playing) await Levels.LoadGameplayAsync();
                if (!this || !IsReady) return;
                SetPhase(destination);
                if (destination == FlowPhase.Playing) Services.Get<EventBus>().Publish(new SessionStarted(CurrentLevel));
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (this) Fail(error.Message); }
            finally { loading = false; }
        }

        private sealed class LoadProgress : IProgress<float>
        {
            private readonly GameFlowBootstrap flow;
            public LoadProgress(GameFlowBootstrap flow) { this.flow = flow; }
            public void Report(float value) { if (flow) flow.LoadingProgress = Mathf.Clamp01(value / 0.9f); }
        }

        public void AddScore(int amount)
        {
            if (Phase != FlowPhase.Playing || amount <= 0) return;
            Score = (int)Math.Min(int.MaxValue, (long)Score + amount);
            if (config.autoWinAtTarget && CurrentDefinition != null && Score >= CurrentDefinition.targetScore) WinLevel();
            else Changed?.Invoke();
        }
        internal void SetBoosterTime(float seconds) { RemainingTime = seconds; }
        public void Pause()
        {
            if (Phase != FlowPhase.Playing) return;
            resumeScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 0;
            SetPhase(FlowPhase.Paused);
        }
        public void Resume()
        {
            if (Phase != FlowPhase.Paused) return;
            UnityEngine.Time.timeScale = resumeScale;
            SetPhase(FlowPhase.Playing);
        }
        // Compatibility alias for v0.2/v0.3 gameplay integrations.
        public void CompleteSession() => WinLevel();
        public void WinLevel() => EndLevel(LevelOutcome.Win, "Level completed!");
        public void LoseLevel() => LoseLevel("You can do it. Try again!");
        public void LoseLevel(string reason) => EndLevel(LevelOutcome.Lose, reason);

        private void EndLevel(LevelOutcome outcome, string reason)
        {
            if (Phase != FlowPhase.Playing) return;
            Outcome = outcome;
            OutcomeReason = reason;
            UnityEngine.Time.timeScale = 0;
            if (!Features.Offline.UsesUnifiedProgress)
            {
                Progress.bestScore = Math.Max(Progress.bestScore, Score);
                Progress.sessions = (int)Math.Min(int.MaxValue, (long)Progress.sessions + 1);
                if (outcome == LevelOutcome.Win)
                {
                    Progress.wins = (int)Math.Min(int.MaxValue, (long)Progress.wins + 1);
                    Progress.highestCompletedLevel = Math.Max(Progress.highestCompletedLevel, CurrentLevel);
                    Progress.highestUnlockedLevel = Math.Max(Progress.highestUnlockedLevel, Math.Min(CurrentLevel + 1, config.LevelCount));
                }
                else Progress.losses = (int)Math.Min(int.MaxValue, (long)Progress.losses + 1);
                ProgressSavePending = true;
            }
            Features.CompleteSession();
            SetPhase(FlowPhase.Result);
            Services.Get<EventBus>().Publish(new SessionEnded(Score, CurrentLevel, outcome));
        }
        public bool SaveProgress()
        {
            if (!ProgressSavePending) return true;
            try { Services.Get<ISaveStore>().Save(config.saveSlot, Progress); ProgressSavePending = false; SaveWarning = null; }
            catch (Exception error) { SaveWarning = "Progress could not be saved: " + error.Message; }
            return !ProgressSavePending;
        }
        protected override void Update()
        {
            base.Update();
            if (Phase != FlowPhase.Playing || !HasTimeLimit) return;
            RemainingTime = Mathf.Max(0, RemainingTime - UnityEngine.Time.deltaTime);
            if (RemainingTime <= 0) LoseLevel("Time is up. Try again!");
        }
        private void SetPhase(FlowPhase phase)
        {
            Phase = phase;
            Services.Get<InputRouter>().Enabled = phase == FlowPhase.Playing;
            Changed?.Invoke();
        }
        private void Fail(string message) { ErrorMessage = message; SetPhase(FlowPhase.Error); }
        protected override void OnApplicationFocus(bool focus)
        { base.OnApplicationFocus(focus); if (!focus) Pause(); }
        protected override void OnDestroy()
        {
            if (Instance == this) UnityEngine.Time.timeScale = originalScale;
            base.OnDestroy();
        }
    }
}
