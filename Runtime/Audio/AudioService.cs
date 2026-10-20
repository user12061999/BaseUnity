using System;
using System.Collections.Generic;
using UnityEngine;
using BaseUnity.Save;
namespace BaseUnity.Audio
{
    public sealed class AudioService : IDisposable
    {
        [Serializable] public sealed class Settings { public float master = 1, music = 1, effects = 1; public bool muted; }
        private readonly GameObject root;
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly Dictionary<AudioClip, float> lastEffect = new Dictionary<AudioClip, float>();
        private readonly ISaveStore saves;
        private Settings settings;
        private readonly float[] gains = new float[2], starts = new float[2];
        private int target, maxVoices = 16, maxPerClip = 3;
        private float fadeSeconds, fadeElapsed, minInterval = .04f;
        private bool fading, stopping, disposed;
        public float MasterVolume => settings.master;
        public float MusicVolume => settings.music;
        public float EffectsVolume => settings.effects;
        public bool Muted => settings.muted;
        public bool IsFading => fading;
        public AudioClip MusicClip => disposed || stopping ? null : music[target].clip;
        public int ActiveEffectVoices { get { int count = 0; foreach (var voice in voices) if (voice && voice.isPlaying) count++; return count; } }
        public AudioService(Transform parent, ISaveStore saves = null)
        {
            this.saves = saves; settings = new Settings();
            if (saves != null && saves.TryLoad<Settings>("baseunity_audio", out var loaded)) settings = loaded;
            root = new GameObject("Audio"); root.transform.SetParent(parent, false);
            music[0] = Source(true); voices.Add(Source(false)); music[1] = Source(true); Apply();
        }
        private AudioSource Source(bool loop)
        { var source = root.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0; return source; }
        public void SetVolume(float master, float musicVolume, float effectsVolume, bool muted)
        { Alive(); settings.master = Sanitize(master); settings.music = Sanitize(musicVolume); settings.effects = Sanitize(effectsVolume); settings.muted = muted; Apply(); }
        private static float Sanitize(float value) => float.IsNaN(value) ? 1 : Mathf.Clamp01(value);
        private void Alive() { if (disposed || !root) throw new ObjectDisposedException(nameof(AudioService)); }
        private void Apply()
        {
            settings.master = Sanitize(settings.master); settings.music = Sanitize(settings.music); settings.effects = Sanitize(settings.effects);
            for (int i = 0; i < music.Length; i++) { music[i].volume = settings.master * settings.music * gains[i]; music[i].mute = settings.muted; }
            foreach (var voice in voices) { voice.volume = settings.master * settings.effects * (effectGains.TryGetValue(voice, out var gain) ? gain : 1); voice.mute = settings.muted; }
        }
        public void SaveSettings() { Alive(); saves?.Save("baseunity_audio", settings); }
        public void PlayMusic(AudioClip clip) => CrossFadeMusic(clip, 0);
        public void CrossFadeMusic(AudioClip clip, float seconds = .35f)
        {
            Alive(); if (!clip) throw new ArgumentNullException(nameof(clip)); Duration(seconds);
            if (!stopping && music[target].clip == clip && music[target].isPlaying) return;
            int index = -1;
            for (int i = 0; i < 2; i++) if (music[i].clip == clip && music[i].isPlaying) index = i;
            if (index < 0)
            { index = gains[0] <= gains[1] ? 0 : 1; music[index].Stop(); music[index].clip = clip; gains[index] = 0; music[index].Play(); }
            target = index; stopping = false; BeginFade(seconds);
        }
        private static void Duration(float value) { if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value)); }
        private void BeginFade(float seconds)
        { starts[0] = gains[0]; starts[1] = gains[1]; fadeSeconds = seconds; fadeElapsed = 0; fading = true; Tick(0); }
        public void FadeOutMusic(float seconds = .35f)
        { Alive(); Duration(seconds); stopping = true; BeginFade(seconds); }
        public void StopMusic()
        { Alive(); fading = false; stopping = true; for (int i = 0; i < 2; i++) { music[i].Stop(); music[i].clip = null; gains[i] = 0; } Apply(); }
        /// <summary>Call once per frame with unscaled delta time. GameBootstrap does this automatically.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (disposed || !root || !fading) return;
            if (float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime < 0) throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            fadeElapsed += unscaledDeltaTime; float t = fadeSeconds <= 0 ? 1 : Mathf.Clamp01(fadeElapsed / fadeSeconds);
            for (int i = 0; i < 2; i++) gains[i] = Mathf.Lerp(starts[i], !stopping && i == target ? 1 : 0, t);
            if (t >= 1)
            { fading = false; for (int i = 0; i < 2; i++) if (stopping || i != target) { music[i].Stop(); music[i].clip = null; } }
            Apply();
        }
        public void ConfigureEffects(int maximumVoices = 16, int maximumPerClip = 3, float minimumIntervalSeconds = .04f)
        {
            Alive(); Duration(minimumIntervalSeconds);
            if (maximumVoices < 1 || maximumVoices > 64 || maximumPerClip < 1 || maximumPerClip > maximumVoices) throw new ArgumentOutOfRangeException(nameof(maximumVoices));
            maxVoices = maximumVoices; maxPerClip = maximumPerClip; minInterval = minimumIntervalSeconds;
            for (int i = voices.Count - 1; i >= maxVoices; i--) { voices[i].Stop(); effectGains.Remove(voices[i]); UnityEngine.Object.Destroy(voices[i]); voices.RemoveAt(i); }
            lastEffect.Clear();
        }
        public void PlayEffect(AudioClip clip, float volume = 1) { TryPlayEffect(clip, volume); }
        public bool TryPlayEffect(AudioClip clip, float volume = 1)
        {
            if (disposed || !root || !clip || settings.muted) return false;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (lastEffect.TryGetValue(clip, out var last) && now - last < minInterval) return false;
            AudioSource available = null; int same = 0;
            foreach (var voice in voices) { if (!voice.isPlaying && !available) available = voice; if (voice.isPlaying && voice.clip == clip) same++; }
            if (same >= maxPerClip) return false;
            if (!available) { if (voices.Count >= maxVoices) return false; available = Source(false); voices.Add(available); }
            if (lastEffect.Count >= 256)
            { AudioClip oldest = null; float time = float.MaxValue; foreach (var pair in lastEffect) if (pair.Value < time) { time = pair.Value; oldest = pair.Key; } if (!ReferenceEquals(oldest, null)) lastEffect.Remove(oldest); }
            lastEffect[clip] = now; available.clip = clip; available.volume = settings.master * settings.effects; available.mute = settings.muted;
            // Per-voice gain remains independent of later Master/Effects changes.
            var gain = Sanitize(volume); effectGains[available] = gain; available.volume *= gain; available.Play(); return true;
        }
        private readonly Dictionary<AudioSource, float> effectGains = new Dictionary<AudioSource, float>();
        public void Dispose() { if (disposed) return; disposed = true; if (root) { foreach (var source in root.GetComponents<AudioSource>()) source.Stop(); UnityEngine.Object.Destroy(root); } lastEffect.Clear(); effectGains.Clear(); }
    }
}
