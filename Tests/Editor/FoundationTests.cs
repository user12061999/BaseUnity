using System;
using System.Collections.Generic;
using System.IO;
using BaseUnity.Core;
using BaseUnity.Input;
using BaseUnity.Save;
using BaseUnity.Time;
using BaseUnity.UI;
using NUnit.Framework;
using UnityEngine;

namespace BaseUnity.Tests
{
    public sealed class FoundationTests
    {
        private sealed class Owned : IDisposable
        {
            private readonly Action action;
            public Owned(Action action) { this.action = action; }
            public void Dispose() => action();
        }
        private sealed class State : IGameState
        {
            public Action enter, exit;
            public void Enter() => enter?.Invoke();
            public void Tick(float deltaTime) { }
            public void Exit() => exit?.Invoke();
        }

        [Test] public void RegistryRejectsDuplicateAndMissingServices()
        {
            using (var registry = new ServiceRegistry())
            {
                registry.Register(new object());
                Assert.Throws<InvalidOperationException>(() => registry.Register(new object()));
                Assert.Throws<InvalidOperationException>(() => registry.Get<EventBus>());
            }
        }
        [Test] public void RegistryDisposesAliasesOnlyOnceAndContinuesAfterFailure()
        {
            var calls = new List<int>();
            var registry = new ServiceRegistry();
            var first = new Owned(() => calls.Add(1));
            registry.Register(first);
            registry.Register<IDisposable>(first);
            registry.Register<object>(new Owned(() => { calls.Add(2); throw new Exception(); }));
            Assert.Throws<AggregateException>(() => registry.Dispose());
            CollectionAssert.AreEqual(new[] { 2, 1 }, calls);
            registry.Dispose();
            Assert.IsFalse(registry.TryGet<Owned>(out _));
        }
        [Test] public void EventsUseSnapshotAndDisposableSubscriptions()
        {
            using (var bus = new EventBus())
            {
                int calls = 0;
                IDisposable second = null;
                bus.Subscribe<int>(_ => second.Dispose());
                second = bus.Subscribe<int>(_ => calls++);
                bus.Publish(1);
                bus.Publish(2);
                Assert.AreEqual(1, calls);
            }
        }
        [Test] public void EventsContinueAfterListenerFailure()
        {
            using (var bus = new EventBus())
            {
                bool received = false;
                bus.Subscribe<int>(_ => throw new Exception());
                bus.Subscribe<int>(_ => received = true);
                Assert.Throws<AggregateException>(() => bus.Publish(1));
                Assert.IsTrue(received);
            }
        }
        [Test] public void StateTransitionsExitBeforeEnterAndDisposeExits()
        {
            var calls = new List<string>();
            using (var machine = new StateMachine())
            {
                machine.Change(new State { enter = () => calls.Add("A+"), exit = () => calls.Add("A-") });
                machine.Change(new State { enter = () => calls.Add("B+"), exit = () => calls.Add("B-") });
            }
            CollectionAssert.AreEqual(new[] { "A+", "A-", "B+", "B-" }, calls);
        }
        [Test] public void StateRejectsReentrantTransitionAndCleansFailedEntry()
        {
            using (var machine = new StateMachine())
            {
                bool cleaned = false;
                Assert.Throws<InvalidOperationException>(() => machine.Change(new State
                { enter = () => machine.Change(new State()), exit = () => cleaned = true }));
                Assert.IsTrue(cleaned);
                Assert.IsNull(machine.Current);
            }
        }
        [Test] public void TimersRespectPauseAndCancellation()
        {
            using (var timers = new TimerService())
            {
                int scaled = 0, unscaled = 0;
                var cancelled = timers.Schedule(0, () => Assert.Fail("Cancelled timer fired"));
                cancelled.Dispose();
                timers.Schedule(1, () => scaled++);
                timers.Schedule(1, () => unscaled++, true);
                timers.Tick(0, 1);
                Assert.AreEqual(0, scaled); Assert.AreEqual(1, unscaled);
                timers.Tick(1, 1); timers.Tick(1, 1);
                Assert.AreEqual(1, scaled); Assert.AreEqual(1, unscaled);
            }
        }
        [Test] public void TimersDeferNewTimersAndLimitRepeatCatchup()
        {
            using (var timers = new TimerService())
            {
                int calls = 0;
                timers.Schedule(1, () => calls++, repeat: true);
                timers.Schedule(0, () => timers.Schedule(0, () => calls++));
                timers.Tick(10, 10); Assert.AreEqual(1, calls);
                timers.Tick(0, 0); Assert.AreEqual(2, calls);
            }
        }
        [Test] public void TimerExceptionDoesNotStarveOtherTimers()
        {
            using (var timers = new TimerService())
            {
                bool fired = false;
                timers.Schedule(0, () => throw new Exception());
                timers.Schedule(0, () => fired = true);
                Assert.Throws<AggregateException>(() => timers.Tick(0, 0));
                Assert.IsTrue(fired);
                Assert.DoesNotThrow(() => timers.Tick(0, 0));
            }
        }
        [Test] public void TimerRejectsInvalidIntervals()
        {
            using (var timers = new TimerService())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => timers.Schedule(double.NaN, () => { }));
                Assert.Throws<ArgumentOutOfRangeException>(() => timers.Schedule(0, () => { }, repeat: true));
            }
        }
        [Test] public void InputGateSuppressesAndRestoresInput()
        {
            var manual = new ManualInputSource();
            var router = new InputRouter(manual);
            manual.SetAxis("Move", 0.5f); manual.SetButton("Jump", true);
            Assert.IsTrue(router.IsPressed("Jump")); Assert.AreEqual(0.5f, router.ReadAxis("Move"));
            router.Enabled = false;
            Assert.IsFalse(router.IsPressed("Jump")); Assert.AreEqual(0, router.ReadAxis("Move"));
            manual.Reset(); router.Enabled = true;
            Assert.IsFalse(router.IsPressed("Jump"));
        }
        [Test] public void PopupBackClosesInReverseOrder()
        {
            var a = new GameObject("A").AddComponent<UIPanel>();
            var b = new GameObject("B").AddComponent<UIPanel>();
            try
            {
                using (var ui = new UIService())
                {
                    ui.PushPopup(a); ui.PushPopup(b);
                    Assert.IsTrue(ui.Back()); Assert.IsFalse(b.IsVisible); Assert.IsTrue(a.IsVisible);
                    Assert.IsTrue(ui.Back()); Assert.IsFalse(ui.Back());
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(a.gameObject); UnityEngine.Object.DestroyImmediate(b.gameObject); }
        }
    }

    public sealed class SaveTests
    {
        [Serializable] public sealed class Data { public int coins; }
        private string directory;
        private JsonSaveStore saves;
        [SetUp] public void Setup()
        { directory = Path.Combine(Path.GetTempPath(), "BaseUnityTests", Guid.NewGuid().ToString("N")); saves = new JsonSaveStore(directory); }
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test] public void SaveRoundtripAndSlotIsolation()
        {
            saves.Save("one", new Data { coins = 42 }); saves.Save("two", new Data { coins = 7 });
            Assert.IsTrue(saves.TryLoad<Data>("one", out var one)); Assert.AreEqual(42, one.coins);
            Assert.IsTrue(saves.TryLoad<Data>("two", out var two)); Assert.AreEqual(7, two.coins);
        }
        [Test] public void CorruptPrimaryFallsBackToPreviousGeneration()
        {
            saves.Save("slot", new Data { coins = 1 }); saves.Save("slot", new Data { coins = 2 });
            saves.Save("slot", new Data { coins = 3 });
            File.WriteAllText(Path.Combine(directory, "slot.json"), "corrupted");
            Assert.IsTrue(saves.TryLoad<Data>("slot", out var data)); Assert.AreEqual(2, data.coins);
        }
        [Test] public void VersionMismatchIsNotSilentlyLoaded()
        {
            saves.Save("slot", new Data(), 2);
            Assert.IsFalse(saves.TryLoad<Data>("slot", out _));
            Assert.IsTrue(saves.TryLoad<Data>("slot", out _, 2));
        }
        [Test] public void DeleteRemovesBackupToo()
        {
            saves.Save("slot", new Data()); saves.Save("slot", new Data()); saves.Delete("slot");
            Assert.IsFalse(saves.TryLoad<Data>("slot", out _));
        }
        [TestCase("../escape")] [TestCase("a/b")] [TestCase("")] [TestCase("slot\n")]
        public void RejectsUnsafeSlotNames(string slot)
        { Assert.Throws<ArgumentException>(() => saves.Save(slot, new Data())); }
    }
}
