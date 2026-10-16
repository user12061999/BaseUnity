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
    public enum FlowPhase { Boot, Loading, Home, Playing, Paused, Result, Error }
    public readonly struct SessionStarted { }
    public readonly struct SessionEnded
    {
        public readonly int Score;
        public SessionEnded(int score) { Score = score; }
    }

    public sealed class GameFlowBootstrap : GameBootstrap
    {
        [Serializable] public sealed class ProgressData { public int bestScore; public int sessions; }
        [SerializeField] private GameFlowConfig config;
        public GameFlowConfig Config { get => config; set => config = value; }
        public FlowPhase Phase { get; private set; } = FlowPhase.Boot;
        public int Score { get; private set; }
        public ProgressData Progress { get; private set; } = new ProgressData();
        public float LoadingProgress { get; private set; }
        public string ErrorMessage { get; private set; }
        public string SaveWarning { get; private set; }
        public event Action Changed;
        private float resumeScale = 1;
        private float originalScale;
        private bool loading;

        protected override void OnReady()
        {
            originalScale = UnityEngine.Time.timeScale;
            if (config && config.uiPrefab)
                Instantiate(config.uiPrefab, transform).Initialize(this);
            else gameObject.AddComponent<GameFlowUI>().Initialize(this);
            StartCoroutine(BootAfterFirstFrame());
        }

        private IEnumerator BootAfterFirstFrame()
        {
            yield return null;
            if (!config) { Fail("Assign a GameFlowConfig to the Bootstrap scene."); yield break; }
            try
            {
                if (Services.Get<ISaveStore>().TryLoad<ProgressData>(config.saveSlot, out var saved)) Progress = saved;
            }
            catch (Exception error) { Fail(error.Message); yield break; }
            GoHome();
        }

        public void GoHome() { if (!loading && config) _ = Transition(config.homeScene, FlowPhase.Home); }
        public void Play()
        {
            if (loading || (Phase != FlowPhase.Home && Phase != FlowPhase.Result)) return;
            _ = Transition(config.inGameScene, FlowPhase.Playing);
        }

        private async Task Transition(string scene, FlowPhase destination)
        {
            loading = true;
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
                if (destination == FlowPhase.Playing) Score = 0;
                SetPhase(destination);
                if (destination == FlowPhase.Playing) Services.Get<EventBus>().Publish(new SessionStarted());
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
            Changed?.Invoke();
        }
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
        public void CompleteSession()
        {
            if (Phase != FlowPhase.Playing) return;
            UnityEngine.Time.timeScale = 0;
            Progress.bestScore = Math.Max(Progress.bestScore, Score);
            Progress.sessions = (int)Math.Min(int.MaxValue, (long)Progress.sessions + 1);
            SaveWarning = null;
            try { Services.Get<ISaveStore>().Save(config.saveSlot, Progress); }
            catch (Exception error) { SaveWarning = "Progress could not be saved: " + error.Message; }
            SetPhase(FlowPhase.Result);
            Services.Get<EventBus>().Publish(new SessionEnded(Score));
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
