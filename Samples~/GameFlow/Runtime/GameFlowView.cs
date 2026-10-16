using System;
using BaseUnity.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Prefab bindings for the optional art-based UI. All sprites/fonts remain project-owned.</summary>
    public sealed class GameFlowView : MonoBehaviour
    {
        [Header("Screens")]
        public GameObject home, gameplay, loading, settings, pause, result, error;
        public GameObject gameplayDemo;
        public GameObject fullScreenBackground;
        [Header("Content")]
        public Text title, homeBest, sessions, score, resultScore, resultBest, loadingText, errorText, saveWarning;
        public Image loadingFill;
        [Header("Actions")]
        public Button playButton, settingsButton, collectButton, finishButton, pauseButton, resumeButton;
        public Button pauseHomeButton, retryButton, resultHomeButton, errorHomeButton;
        [Header("Settings")]
        public Slider masterSlider, musicSlider, effectsSlider;
        public Text masterValue, musicValue, effectsValue, soundLabel, settingsError;
        public Button volumeDownButton, volumeUpButton, soundButton, saveButton, cancelButton, closeSettingsButton;
        private GameFlowBootstrap flow;
        private bool settingsOpen;
        private bool muted;
        private GameObject selectedScreen;

        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner;
            var input = new GameObject("Game Flow EventSystem", typeof(EventSystem));
            input.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            var type = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (type != null) input.AddComponent(type);
            else input.AddComponent<StandaloneInputModule>();
#else
            input.AddComponent<StandaloneInputModule>();
#endif
            playButton.onClick.AddListener(flow.Play);
            settingsButton.onClick.AddListener(OpenSettings);
            collectButton.onClick.AddListener(() => flow.AddScore(10));
            finishButton.onClick.AddListener(flow.CompleteSession);
            pauseButton.onClick.AddListener(flow.Pause);
            resumeButton.onClick.AddListener(flow.Resume);
            pauseHomeButton.onClick.AddListener(flow.GoHome);
            retryButton.onClick.AddListener(flow.Play);
            resultHomeButton.onClick.AddListener(flow.GoHome);
            errorHomeButton.onClick.AddListener(flow.GoHome);
            volumeDownButton.onClick.AddListener(() => masterSlider.value -= 0.1f);
            volumeUpButton.onClick.AddListener(() => masterSlider.value += 0.1f);
            soundButton.onClick.AddListener(() => { muted = !muted; UpdateSettingsLabels(); });
            saveButton.onClick.AddListener(SaveSettings);
            cancelButton.onClick.AddListener(CloseSettings);
            closeSettingsButton.onClick.AddListener(CloseSettings);
            masterSlider.onValueChanged.AddListener(_ => UpdateSettingsLabels());
            musicSlider.onValueChanged.AddListener(_ => UpdateSettingsLabels());
            effectsSlider.onValueChanged.AddListener(_ => UpdateSettingsLabels());
            if (flow.Config) title.text = flow.Config.gameTitle;
            flow.Changed += Refresh;
            Refresh();
        }

        private void OpenSettings()
        {
            var audio = flow.Services.Get<AudioService>();
            masterSlider.SetValueWithoutNotify(audio.MasterVolume);
            musicSlider.SetValueWithoutNotify(audio.MusicVolume);
            effectsSlider.SetValueWithoutNotify(audio.EffectsVolume);
            muted = audio.Muted;
            settingsError.text = "";
            settingsOpen = true;
            UpdateSettingsLabels();
            Refresh();
        }
        private void CloseSettings() { settingsOpen = false; Refresh(); }
        private void SaveSettings()
        {
            var audio = flow.Services.Get<AudioService>();
            float oldMaster = audio.MasterVolume, oldMusic = audio.MusicVolume, oldEffects = audio.EffectsVolume;
            bool oldMuted = audio.Muted;
            try
            {
                audio.SetVolume(masterSlider.value, musicSlider.value, effectsSlider.value, muted);
                audio.SaveSettings();
                CloseSettings();
            }
            catch (Exception exception)
            {
                audio.SetVolume(oldMaster, oldMusic, oldEffects, oldMuted);
                settingsError.text = "Could not save. Please try again.\n" + exception.Message;
            }
        }
        private void UpdateSettingsLabels()
        {
            masterValue.text = Mathf.RoundToInt(masterSlider.value * 100) + "%";
            musicValue.text = Mathf.RoundToInt(musicSlider.value * 100) + "%";
            effectsValue.text = Mathf.RoundToInt(effectsSlider.value * 100) + "%";
            soundLabel.text = muted ? "SOUND OFF" : "SOUND ON";
        }
        private void Refresh()
        {
            var phase = flow.Phase;
            if (fullScreenBackground) fullScreenBackground.SetActive(
                phase == FlowPhase.Home || phase == FlowPhase.Boot || phase == FlowPhase.Loading ||
                phase == FlowPhase.Error || (gameplayDemo && gameplayDemo.activeSelf));
            if (phase != FlowPhase.Home) settingsOpen = false;
            home.SetActive(phase == FlowPhase.Home);
            gameplay.SetActive(phase == FlowPhase.Playing || phase == FlowPhase.Paused || phase == FlowPhase.Result);
            loading.SetActive(phase == FlowPhase.Boot || phase == FlowPhase.Loading);
            settings.SetActive(settingsOpen);
            pause.SetActive(phase == FlowPhase.Paused);
            result.SetActive(phase == FlowPhase.Result);
            error.SetActive(phase == FlowPhase.Error);
            homeBest.text = flow.Progress.bestScore.ToString("N0");
            sessions.text = flow.Progress.sessions.ToString("N0");
            score.text = flow.Score.ToString("N0");
            resultScore.text = flow.Score.ToString("N0");
            resultBest.text = flow.Progress.bestScore.ToString("N0");
            saveWarning.text = flow.SaveWarning ?? "";
            errorText.text = flow.ErrorMessage ?? "";

            // Pointer raycasts are blocked by the modal. Also remove underlying keyboard/gamepad targets.
            SetInteraction(home, phase == FlowPhase.Home && !settingsOpen);
            SetInteraction(gameplay, phase == FlowPhase.Playing);
            var current = settingsOpen ? settings : phase == FlowPhase.Home ? home :
                phase == FlowPhase.Playing ? gameplay : phase == FlowPhase.Paused ? pause :
                phase == FlowPhase.Result ? result : phase == FlowPhase.Error ? error : loading;
            if (current != selectedScreen)
            {
                selectedScreen = current;
                var first = current.GetComponentInChildren<Selectable>();
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(first ? first.gameObject : null);
            }
        }
        private static void SetInteraction(GameObject root, bool enabled)
        {
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }
        private void Update()
        {
            if (!flow || !loading.activeSelf) return;
            loadingFill.fillAmount = flow.LoadingProgress;
            loadingText.text = "LOADING  " + Mathf.RoundToInt(flow.LoadingProgress * 100) + "%";
        }
        private void OnDestroy() { if (flow) flow.Changed -= Refresh; }
    }
}
