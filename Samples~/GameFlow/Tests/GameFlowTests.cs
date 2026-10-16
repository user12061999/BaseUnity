using System;
using System.Collections;
using System.Linq;
using BaseUnity.Audio;
using BaseUnity.Core;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BaseUnity.GameFlow.Tests
{
    public sealed class GameFlowTests
    {
        private GameFlowBootstrap flow;
        private string originalSlot;
        private string testSlot;

        private IEnumerator WaitFor(FlowPhase phase)
        {
            float deadline = UnityEngine.Time.realtimeSinceStartup + 15;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                flow = GameBootstrap.Instance as GameFlowBootstrap;
                if (flow && flow.Phase == phase) yield break;
                if (flow && flow.Phase == FlowPhase.Error) Assert.Fail(flow.ErrorMessage);
                yield return null;
            }
            Assert.Fail("Timed out waiting for " + phase);
        }

        private static void Click(string name)
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(item => item.name == name);
            Assert.IsTrue(button.IsInteractable());
            button.onClick.Invoke();
        }

        [UnityTest] public IEnumerator CompleteFlowThroughRealScenesAndButtons()
        {
            string bootstrap = null;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.EndsWith("/Scenes/Bootstrap.unity", StringComparison.Ordinal)) { bootstrap = path; break; }
            }
            Assert.IsNotNull(bootstrap, "Generate the Game Flow sample scenes before running its tests.");
            yield return SceneManager.LoadSceneAsync(bootstrap);
            yield return WaitFor(FlowPhase.Home);
            Assert.AreEqual(flow.Config.homeScene, SceneManager.GetActiveScene().path);
            originalSlot = flow.Config.saveSlot;
            testSlot = "flow_test_" + Guid.NewGuid().ToString("N");
            flow.Config.saveSlot = testSlot;
            var audio = flow.Services.Get<AudioService>();
            float originalVolume = audio.MasterVolume;
            float originalMusic = audio.MusicVolume, originalEffects = audio.EffectsVolume;
            Click("Settings"); Click("Volume -");
            var view = UnityEngine.Object.FindFirstObjectByType<GameFlowView>();
            if (view)
            {
                Assert.IsFalse(view.home.GetComponent<CanvasGroup>().interactable);
                Assert.IsTrue(view.settings.activeSelf);
                view.musicSlider.value = 0.25f; view.effectsSlider.value = 0.5f;
                foreach (var slider in new[] { view.masterSlider, view.musicSlider, view.effectsSlider })
                    Assert.LessOrEqual(slider.fillRect.rect.width, ((RectTransform)slider.fillRect.parent).rect.width + 0.1f);
            }
            Click("Cancel");
            Assert.AreEqual(originalVolume, audio.MasterVolume);
            Assert.AreEqual(originalMusic, audio.MusicVolume);
            Assert.AreEqual(originalEffects, audio.EffectsVolume);
            if (view) Assert.IsTrue(view.home.GetComponent<CanvasGroup>().interactable);
            Click("Play");
            // Repeated input must not start a second load.
            flow.Play(); flow.GoHome();
            yield return WaitFor(FlowPhase.Playing);
            Assert.AreEqual(flow.Config.inGameScene, SceneManager.GetActiveScene().path);
            Click("Collect +10"); Click("Collect +10");
            Assert.AreEqual(20, flow.Score);
            Click("Pause");
            Assert.AreEqual(FlowPhase.Paused, flow.Phase); Assert.AreEqual(0, UnityEngine.Time.timeScale);
            if (view) Assert.IsFalse(view.gameplay.GetComponent<CanvasGroup>().interactable);
            flow.AddScore(10); Assert.AreEqual(20, flow.Score);
            Click("Resume"); Assert.AreEqual(1, UnityEngine.Time.timeScale);
            Click("Finish session");
            Assert.AreEqual(FlowPhase.Result, flow.Phase);
            Assert.IsTrue(flow.Services.Get<ISaveStore>().TryLoad<GameFlowBootstrap.ProgressData>(testSlot, out var saved));
            Assert.GreaterOrEqual(saved.bestScore, 20);
            Click("Retry"); yield return WaitFor(FlowPhase.Playing);
            Assert.AreEqual(0, flow.Score); Assert.AreEqual(1, UnityEngine.Time.timeScale);
            Click("Pause"); Click("Home"); yield return WaitFor(FlowPhase.Home);
            Assert.AreEqual(1, UnityEngine.Time.timeScale);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (flow)
            {
                if (testSlot != null) flow.Services.Get<ISaveStore>().Delete(testSlot);
                if (originalSlot != null) flow.Config.saveSlot = originalSlot;
                UnityEngine.Object.Destroy(flow.gameObject);
            }
            UnityEngine.Time.timeScale = 1;
            yield return null;
        }
    }
}
