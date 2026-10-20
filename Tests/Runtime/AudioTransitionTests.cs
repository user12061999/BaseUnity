using System.Collections;
using BaseUnity.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BaseUnity.Tests
{
    public sealed class AudioTransitionTests
    {
        private GameObject owner;
        private AudioService audio;
        private AudioClip first, second;
        [SetUp] public void Setup() { owner = new GameObject("Audio transition test"); audio = new AudioService(owner.transform); first = AudioClip.Create("First", 44100, 1, 22050, false); second = AudioClip.Create("Second", 44100, 1, 22050, false); }
        [TearDown] public void Cleanup() { audio.Dispose(); Object.Destroy(owner); Object.Destroy(first); Object.Destroy(second); }
        [UnityTest] public IEnumerator CrossfadeCanReverseAndUsesUnscaledTicksWithoutRestartingCurrentClip()
        {
            audio.PlayMusic(first); audio.CrossFadeMusic(second, 1); audio.Tick(.5f);
            var sources = owner.GetComponentsInChildren<AudioSource>();
            Assert.AreEqual(.5f, sources[0].volume, .001f); Assert.AreEqual(.5f, sources[2].volume, .001f);
            audio.CrossFadeMusic(first, 1); audio.Tick(.5f); Assert.AreEqual(.75f, sources[0].volume, .001f);
            audio.CrossFadeMusic(first, 1); audio.Tick(.5f); Assert.IsFalse(audio.IsFading); Assert.AreEqual(first, audio.MusicClip);
            Assert.IsNull(sources[2].clip); audio.FadeOutMusic(.5f); audio.Tick(.5f); Assert.IsNull(audio.MusicClip); Assert.IsNull(sources[0].clip);
            yield return null;
        }
        [UnityTest] public IEnumerator EffectsRespectCooldownVoiceLimitsAndPerVoiceGain()
        {
            audio.ConfigureEffects(2, 1, .5f);
            Assert.IsTrue(audio.TryPlayEffect(first, .4f)); Assert.IsFalse(audio.TryPlayEffect(first)); Assert.IsTrue(audio.TryPlayEffect(second));
            audio.SetVolume(.5f, 1, .25f, false);
            Assert.AreEqual(.05f, owner.GetComponentsInChildren<AudioSource>()[1].volume, .001f);
            Assert.LessOrEqual(owner.GetComponentsInChildren<AudioSource>().Length, 4);
            audio.SetVolume(1, 1, 1, true); Assert.IsFalse(audio.TryPlayEffect(first));
            audio.Dispose(); Assert.IsFalse(audio.TryPlayEffect(first)); Assert.IsTrue(first); Assert.IsTrue(second); yield return null;
        }
    }
}
