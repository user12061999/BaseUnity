using System.Collections;
using BaseUnity.Core;
using BaseUnity.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BaseUnity.Tests
{
    public sealed class BootstrapTests
    {
        private GameObject owner;
        private GameObject duplicate;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (duplicate) Object.Destroy(duplicate);
            if (owner) Object.Destroy(owner);
            yield return null;
        }

        [UnityTest] public IEnumerator BootstrapTicksTimersAndDisposesServices()
        {
            owner = new GameObject("Bootstrap test");
            var bootstrap = owner.AddComponent<GameBootstrap>();
            Assert.IsTrue(bootstrap.IsReady);
            var registry = bootstrap.Services;
            bool fired = false;
            registry.Get<TimerService>().Schedule(0, () => fired = true);
            yield return null;
            yield return null;
            Assert.IsTrue(fired);
            Object.Destroy(owner);
            yield return null;
            Assert.IsFalse(GameBootstrap.Instance);
            Assert.IsFalse(registry.TryGet<EventBus>(out _));
        }

        [UnityTest] public IEnumerator DuplicateDoesNotReplaceOriginal()
        {
            owner = new GameObject("Original");
            var original = owner.AddComponent<GameBootstrap>();
            duplicate = new GameObject("Duplicate");
            duplicate.AddComponent<GameBootstrap>();
            yield return null;
            Assert.IsFalse(duplicate);
            Assert.AreSame(original, GameBootstrap.Instance);
            Assert.IsTrue(original.IsReady);
        }
    }
}
