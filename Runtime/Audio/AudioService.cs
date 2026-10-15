using System;
using UnityEngine;
using BaseUnity.Save;

namespace BaseUnity.Audio
{
    public sealed class AudioService : IDisposable
    {
        [Serializable]
        public sealed class Settings
        {
            public float master = 1;
            public float music = 1;
            public float effects = 1;
            public bool muted;
        }

        private readonly GameObject root;
        private readonly AudioSource music;
        private readonly AudioSource effects;
        private readonly ISaveStore saves;
        private Settings settings;
        public float MasterVolume => settings.master;
        public float MusicVolume => settings.music;
        public float EffectsVolume => settings.effects;
        public bool Muted => settings.muted;

        public AudioService(Transform parent, ISaveStore saves = null)
        {
            this.saves = saves;
            settings = new Settings();
            if (saves != null && saves.TryLoad<Settings>("baseunity_audio", out var loaded)) settings = loaded;
            root = new GameObject("Audio");
            root.transform.SetParent(parent, false);
            music = root.AddComponent<AudioSource>();
            effects = root.AddComponent<AudioSource>();
            music.playOnAwake = effects.playOnAwake = false;
            music.loop = true;
            Apply();
        }

        public void SetVolume(float master, float musicVolume, float effectsVolume, bool muted)
        {
            settings.master = Sanitize(master);
            settings.music = Sanitize(musicVolume);
            settings.effects = Sanitize(effectsVolume);
            settings.muted = muted;
            Apply();
        }

        private static float Sanitize(float value) => float.IsNaN(value) ? 1 : Mathf.Clamp01(value);
        private void Apply()
        {
            settings.master = Sanitize(settings.master);
            settings.music = Sanitize(settings.music);
            settings.effects = Sanitize(settings.effects);
            music.volume = settings.master * settings.music;
            effects.volume = settings.master * settings.effects;
            music.mute = effects.mute = settings.muted;
        }

        public void SaveSettings() => saves?.Save("baseunity_audio", settings);
        public void PlayMusic(AudioClip clip)
        {
            if (!clip) throw new ArgumentNullException(nameof(clip));
            if (music.clip == clip && music.isPlaying) return;
            music.clip = clip;
            music.Play();
        }
        public void StopMusic() => music.Stop();
        public void PlayEffect(AudioClip clip, float volume = 1)
        { if (clip) effects.PlayOneShot(clip, Sanitize(volume)); }
        public void Dispose() { if (root) UnityEngine.Object.Destroy(root); }
    }
}
