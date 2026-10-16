using System;
using System.Collections;
using System.IO;
using System.Linq;
using BaseUnity.Core;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BaseUnity.GameFlow.Tests
{
    public sealed class LevelFlowTests
    {
        private GameFlowBootstrap flow;
        private GameFlowConfig config;
        private string slot;
        private GameObject gameplayTemplate;
        private LevelData levelData;
        private GameObject installedDemo;

        private IEnumerator WaitFor(FlowPhase phase)
        {
            float until = UnityEngine.Time.realtimeSinceStartup + 15;
            while (UnityEngine.Time.realtimeSinceStartup < until)
            {
                if (flow && flow.Phase == FlowPhase.Error) Assert.Fail(flow.ErrorMessage);
                if (flow && flow.Phase == phase) yield break;
                yield return null;
            }
            Assert.Fail("Timed out waiting for " + phase);
        }
        private static void Click(string name)
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(item => item.name == name);
            Assert.IsTrue(button.IsInteractable()); button.onClick.Invoke();
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            string bootstrap = Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
                .Select(SceneUtility.GetScenePathByBuildIndex).FirstOrDefault(path => path.EndsWith("/Scenes/Bootstrap.unity", StringComparison.Ordinal));
            Assert.IsNotNull(bootstrap);
            yield return SceneManager.LoadSceneAsync(bootstrap);
            flow = GameBootstrap.Instance as GameFlowBootstrap;
            yield return WaitFor(FlowPhase.Home);
            // Never mutate the project's config asset or persistent progress in these tests.
            config = UnityEngine.Object.Instantiate(flow.Config);
            installedDemo = flow.Config.GetLevel(1)?.gameplayPrefab;
            slot = "level_test_" + Guid.NewGuid().ToString("N");
            config.saveSlot = slot;
            config.autoWinAtTarget = true;
            config.levels = new[]
            {
                new GameLevel { targetScore = 30, timeLimitSeconds = 0 },
                new GameLevel { targetScore = 50, timeLimitSeconds = 0 },
                new GameLevel { targetScore = 80, timeLimitSeconds = 0 }
            };
            flow.Config = config;
            flow.Progress.highestUnlockedLevel = 1; flow.Progress.highestCompletedLevel = 0;
            flow.Progress.sessions = 0; flow.Progress.wins = 0; flow.Progress.losses = 0;
            flow.Progress.bestScore = 0;
        }

        [UnityTest] public IEnumerator WinUnlocksNextAndDuplicateResultsAreIgnored()
        {
            flow.StartLevel(2); Assert.AreEqual(FlowPhase.Home, flow.Phase);
            flow.Play(); yield return WaitFor(FlowPhase.Playing);
            int ended = 0;
            using (flow.Services.Get<EventBus>().Subscribe<SessionEnded>(message =>
            { ended++; Assert.AreEqual(1, message.Level); Assert.AreEqual(LevelOutcome.Win, message.Outcome); }))
            {
                flow.AddScore(30);
                Assert.AreEqual(LevelOutcome.Win, flow.Outcome); Assert.AreEqual(FlowPhase.Result, flow.Phase);
                flow.WinLevel(); flow.LoseLevel(); flow.AddScore(10);
                Assert.AreEqual(1, ended); Assert.AreEqual(1, flow.Progress.sessions); Assert.AreEqual(30, flow.Score);
            }
            Assert.AreEqual(2, flow.Progress.highestUnlockedLevel); Assert.AreEqual(1, flow.Progress.highestCompletedLevel);
            Assert.IsTrue(flow.CanNextLevel); Assert.AreEqual(0, UnityEngine.Time.timeScale);
            Click("Next Level"); flow.NextLevel();
            yield return WaitFor(FlowPhase.Playing);
            Assert.AreEqual(2, flow.CurrentLevel); Assert.AreEqual(0, flow.Score);
            Assert.AreEqual(LevelOutcome.None, flow.Outcome); Assert.AreEqual(1, UnityEngine.Time.timeScale);
        }

        [UnityTest] public IEnumerator LoseDoesNotUnlockAndRetryKeepsSameLevel()
        {
            Click("Play"); yield return WaitFor(FlowPhase.Playing);
            flow.AddScore(10); Click("Lose level");
            Assert.AreEqual(LevelOutcome.Lose, flow.Outcome);
            Assert.AreEqual(1, flow.Progress.highestUnlockedLevel); Assert.AreEqual(0, flow.Progress.highestCompletedLevel);
            Assert.AreEqual(1, flow.Progress.losses); Assert.IsFalse(flow.CanNextLevel);
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(button => button.name == "Next Level"));
            flow.NextLevel(); Assert.AreEqual(FlowPhase.Result, flow.Phase);
            Click("Retry"); yield return WaitFor(FlowPhase.Playing);
            Assert.AreEqual(1, flow.CurrentLevel); Assert.AreEqual(0, flow.Score);
            flow.LoseLevel(); Click("Home"); yield return WaitFor(FlowPhase.Home);
            Assert.AreEqual(1, UnityEngine.Time.timeScale);
        }

        [UnityTest] public IEnumerator TimerPausesAndExpiresOnce()
        {
            config.levels[0].timeLimitSeconds = 0.5f;
            flow.Play(); yield return WaitFor(FlowPhase.Playing);
            flow.Pause(); float remaining = flow.RemainingTime;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(remaining, flow.RemainingTime); Assert.AreEqual(FlowPhase.Paused, flow.Phase);
            flow.Resume(); yield return WaitFor(FlowPhase.Result);
            Assert.AreEqual(LevelOutcome.Lose, flow.Outcome); Assert.AreEqual(0, flow.RemainingTime);
            Assert.AreEqual(1, flow.Progress.losses);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(1, flow.Progress.sessions);
        }

        [UnityTest] public IEnumerator FinalLevelStopsNextAndProgressSurvivesRestart()
        {
            flow.Play(); yield return WaitFor(FlowPhase.Playing);
            for (int level = 1; level <= 3; level++)
            {
                flow.AddScore(config.GetLevel(level).targetScore);
                Assert.AreEqual(LevelOutcome.Win, flow.Outcome);
                if (level < 3) { flow.NextLevel(); yield return WaitFor(FlowPhase.Playing); }
            }
            Assert.IsFalse(flow.CanNextLevel);
            flow.NextLevel(); Assert.AreEqual(FlowPhase.Result, flow.Phase); Assert.AreEqual(3, flow.CurrentLevel);
            Assert.AreEqual(3, flow.Progress.highestUnlockedLevel); Assert.AreEqual(3, flow.Progress.wins);
            var view = UnityEngine.Object.FindFirstObjectByType<GameFlowView>();
            if (view) { Assert.IsFalse(view.nextLevelButton.gameObject.activeSelf); StringAssert.Contains("ALL LEVELS", view.resultTitle.text); }
            yield return Restart();
            Assert.AreEqual(3, flow.HomeLevel); Assert.AreEqual(3, flow.Progress.highestCompletedLevel);
            Click("Play"); yield return WaitFor(FlowPhase.Playing); Assert.AreEqual(3, flow.CurrentLevel);
        }

        [UnityTest] public IEnumerator LegacySaveStartsAtLevelOneAndKeepsScore()
        {
            flow.Services.Get<ISaveStore>().Save(slot, new LegacyProgress { bestScore = 99, sessions = 7 });
            yield return Restart();
            Assert.AreEqual(1, flow.HomeLevel); Assert.AreEqual(0, flow.Progress.highestCompletedLevel);
            Assert.AreEqual(99, flow.Progress.bestScore); Assert.AreEqual(7, flow.Progress.sessions);
        }
        [Serializable] public sealed class LegacyProgress { public int bestScore; public int sessions; }

        [UnityTest] public IEnumerator DemoPrefabConnectsTargetTrapAndRetry() { yield return PlayDemo(); }

        [UnityTest] public IEnumerator PlainUIAlsoSupportsSelectionAndPrefabGameplay()
        {
            config.uiPrefab = null;
            yield return Restart();
            yield return PlayDemo();
        }

        private IEnumerator PlayDemo()
        {
            if (!installedDemo || !installedDemo.GetComponent<TapTargetGameplay>())
                Assert.Ignore("Generate the sample Level Assets to test the demo prefab.");
            config.levels[0].gameplayPrefab = installedDemo;
            Click("Choose Level"); Click("Level 1"); yield return WaitFor(FlowPhase.Playing);
            var demo = flow.Levels.Instance.GetComponent<TapTargetGameplay>();
            Assert.IsTrue(demo.gameObject.activeInHierarchy);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TapTargetGameplay>(FindObjectsSortMode.None).Length);
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.name == "Collect +10"));
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            Canvas.ForceUpdateCanvases();
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, demo.target.transform.position);
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Count > 0); Assert.AreEqual(demo.target.gameObject, hits[0].gameObject);
            demo.target.onClick.Invoke(); demo.target.onClick.Invoke(); demo.target.onClick.Invoke();
            Assert.AreEqual(LevelOutcome.Win, flow.Outcome); Assert.AreEqual(30, flow.Score);
            Click("Retry"); yield return WaitFor(FlowPhase.Playing);
            Assert.IsTrue(demo == null);
            demo = flow.Levels.Instance.GetComponent<TapTargetGameplay>();
            demo.trap.onClick.Invoke(); Assert.AreEqual(LevelOutcome.Lose, flow.Outcome);
            Assert.AreEqual(1, flow.Progress.wins); Assert.AreEqual(1, flow.Progress.losses);
        }

        [UnityTest] public IEnumerator PrefabLifecycleRejectsOldCallbacksAndCleansOnHome()
        {
            gameplayTemplate = new GameObject("Test gameplay template");
            gameplayTemplate.AddComponent<LevelProbe>();
            gameplayTemplate.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(gameplayTemplate);
            levelData = ScriptableObject.CreateInstance<LevelData>();
            levelData.title = "Asset definition"; levelData.targetScore = 40; levelData.timeLimitSeconds = 0;
            levelData.gameplayPrefab = gameplayTemplate; config.levels[0].data = levelData;
            flow.ShowLevels(); Assert.AreEqual(FlowPhase.LevelSelect, flow.Phase);
            Click("Level 1"); yield return WaitFor(FlowPhase.Playing);
            Assert.AreEqual(40, flow.CurrentDefinition.targetScore);
            var first = flow.Levels.Instance;
            Assert.IsNotNull(first); Assert.AreNotSame(gameplayTemplate, first);
            var context = first.GetComponent<LevelProbe>().Context;
            Assert.IsNotNull(context); Assert.IsTrue(context.IsPlaying);
            context.Lose("Prefab lost"); Assert.AreEqual(LevelOutcome.Lose, flow.Outcome);
            Assert.IsFalse(context.IsPlaying);
            flow.RetryLevel(); yield return WaitFor(FlowPhase.Playing);
            Assert.IsTrue(first == null); Assert.IsNotNull(flow.Levels.Instance);
            context.Win(); context.AddScore(100);
            Assert.AreEqual(FlowPhase.Playing, flow.Phase); Assert.AreEqual(0, flow.Score);
            var current = flow.Levels.Context;
            flow.Pause(); current.Win(); Assert.AreEqual(FlowPhase.Paused, flow.Phase);
            flow.Resume(); current.Win(); Assert.AreEqual(LevelOutcome.Win, flow.Outcome);
            flow.NextLevel(); yield return WaitFor(FlowPhase.Playing);
            Assert.AreEqual(2, flow.CurrentLevel); Assert.IsNull(flow.Levels.Instance);
            current.Lose(); Assert.AreEqual(FlowPhase.Playing, flow.Phase);
            flow.GoHome(); yield return WaitFor(FlowPhase.Home);
            Assert.IsNull(flow.Levels.GameplayRoot); Assert.IsNull(flow.Levels.Context);
        }

        [UnityTest] public IEnumerator SelectionLocksPagesAndUpdatesAfterWinning()
        {
            config.levels = Enumerable.Range(1, 9).Select(i => new GameLevel { title = "Stage " + i, timeLimitSeconds = 0 }).ToArray();
            Click("Choose Level"); Assert.AreEqual(FlowPhase.LevelSelect, flow.Phase);
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            Assert.IsFalse(buttons.Single(b => b.name == "Level 2").IsInteractable());
            flow.StartLevel(2); Assert.AreEqual(FlowPhase.LevelSelect, flow.Phase);
            Click("Next page");
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Level 7").IsInteractable());
            Click("Previous page"); Click("Level 1"); yield return WaitFor(FlowPhase.Playing);
            flow.WinLevel(); flow.GoHome(); yield return WaitFor(FlowPhase.Home);
            Click("Choose Level");
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Level 2").IsInteractable());
            Click("Level 2"); yield return WaitFor(FlowPhase.Playing); Assert.AreEqual(2, flow.CurrentLevel);
        }

        private IEnumerator Restart()
        {
            UnityEngine.Object.Destroy(flow.gameObject); yield return null;
            var root = new GameObject("Level test bootstrap"); root.SetActive(false);
            flow = root.AddComponent<GameFlowBootstrap>(); flow.Config = config;
            root.SetActive(true);
            yield return WaitFor(FlowPhase.Home);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (flow) UnityEngine.Object.Destroy(flow.gameObject);
            yield return null;
            if (config) UnityEngine.Object.Destroy(config);
            if (gameplayTemplate) UnityEngine.Object.Destroy(gameplayTemplate);
            if (levelData) UnityEngine.Object.Destroy(levelData);
            if (slot != null) new JsonSaveStore(Path.Combine(Application.persistentDataPath, "BaseUnity")).Delete(slot);
            UnityEngine.Time.timeScale = 1;
        }
    }
    public sealed class LevelProbe : MonoBehaviour, ILevelGameplay
    {
        public LevelContext Context { get; private set; }
        public void Initialize(LevelContext context) { Context = context; }
    }
}
