using System.Collections;
using BaseUnity.Audio;
using BaseUnity.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BaseUnity.Tests
{
    public sealed class UIFeedbackTests
    {
        [UnityTest] public IEnumerator CueRateLimitUsesInjectedUnscaledTimeAndDisposeStopsEvents()
        {
            float now = 10; int events = 0;
            var service = new UIFeedbackService(new FeedbackSettings(), null, now: () => now);
            service.Played += (cue, position) => events++;
            Assert.IsTrue(service.Play(UICue.Click)); Assert.IsFalse(service.Play(UICue.Click));
            now += .1f; Assert.IsTrue(service.Play(UICue.Click)); Assert.AreEqual(2, events);
            service.Dispose(); Assert.IsFalse(service.Play(UICue.Reward)); Assert.AreEqual(2, events);
            yield return null;
        }
        [UnityTest] public IEnumerator DisabledAndReducedMotionFlagsAreIndependent()
        {
            var disabled = new UIFeedbackService(new FeedbackSettings { enabled = false }, null);
            Assert.IsFalse(disabled.Play(UICue.Click)); Assert.IsFalse(disabled.MotionEnabled); disabled.Dispose();
            var reduced = new UIFeedbackService(new FeedbackSettings { reducedMotion = true, uiSounds = false }, null);
            Assert.IsTrue(reduced.Play(UICue.Reward)); Assert.IsFalse(reduced.MotionEnabled); Assert.IsFalse(reduced.SoundsEnabled);
            Assert.IsFalse(reduced.Play(UICue.Hover)); reduced.Dispose(); yield return null;
        }
        [UnityTest] public IEnumerator CustomClipsRemainOwnedByProjectAndAudioRespectsEffectsMute()
        {
            var owner = new GameObject("Feedback audio test"); var audio = new AudioService(owner.transform);
            var clip = AudioClip.Create("Project clip", 2205, 1, 22050, false);
            var profile = ScriptableObject.CreateInstance<UIFeedbackProfile>(); profile.fallbackTones = false;
            profile.sounds = new[] { new UIFeedbackProfile.Sound { cue = UICue.Click, clip = clip } };
            var service = new UIFeedbackService(new FeedbackSettings(), audio, profile);
            Assert.IsTrue(service.Play(UICue.Click));
            audio.SetVolume(.5f, 1, .25f, true);
            foreach (var source in owner.GetComponentsInChildren<AudioSource>()) Assert.IsTrue(source.mute);
            // The bounded voice stores the cue gain on its source, preserving the audible mix.
            Assert.AreEqual(.125f * profile.sounds[0].volume, owner.GetComponentsInChildren<AudioSource>()[1].volume, .001f);
            service.Dispose(); yield return null;
            Assert.IsTrue(clip); Assert.IsTrue(profile); // Only runtime clones/generated tones are released.
            audio.Dispose(); Object.Destroy(owner); Object.Destroy(profile); Object.Destroy(clip); yield return null;
        }
    }
}
