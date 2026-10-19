using System;
using System.Collections.Generic;
using System.Linq;
using BaseUnity.Audio;
using UnityEngine;

namespace BaseUnity.Features
{
    /// <summary>Main-thread presentation only. Never performs economy transactions.</summary>
    public sealed class UIFeedbackService : IDisposable
    {
        private readonly FeedbackSettings settings;
        private readonly AudioService audio;
        private readonly Func<float> now;
        private readonly Dictionary<UICue, float> last = new Dictionary<UICue, float>();
        private readonly Dictionary<UICue, AudioClip> tones = new Dictionary<UICue, AudioClip>();
        private bool disposed;
        public UIFeedbackProfile Profile { get; }
        public bool Enabled => settings.enabled && !disposed;
        public bool MotionEnabled => Enabled && settings.uiMotion && !settings.reducedMotion;
        public bool SoundsEnabled => Enabled && settings.uiSounds;
        public bool HoverEnabled => Enabled && settings.uiHover;
        public event Action<UICue, Vector2?> Played;
        public UIFeedbackService(FeedbackSettings source, AudioService audio, UIFeedbackProfile profile = null, Func<float> now = null)
        {
            settings = JsonUtility.FromJson<FeedbackSettings>(JsonUtility.ToJson(source ?? new FeedbackSettings()));
            this.audio = audio; this.now = now ?? (() => UnityEngine.Time.unscaledTime);
            Profile = profile ? UnityEngine.Object.Instantiate(profile) : ScriptableObject.CreateInstance<UIFeedbackProfile>();
            Profile.pressedScale = Range(Profile.pressedScale, .8f, 1, .94f);
            Profile.buttonSeconds = Range(Profile.buttonSeconds, .04f, .4f, .09f);
            Profile.panelSeconds = Range(Profile.panelSeconds, .08f, .6f, .18f);
            Profile.rewardSeconds = Range(Profile.rewardSeconds, .2f, 2, .75f);
        }
        private static float Range(float value, float min, float max, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        public bool Play(UICue cue, Vector2? screenPosition = null)
        {
            if (!Enabled || !Enum.IsDefined(typeof(UICue), cue) || (cue == UICue.Hover && !HoverEnabled)) return false;
            float time = now();
            float interval = cue == UICue.Hover ? .15f : cue == UICue.Reward ? .12f : cue == UICue.Change ? .075f : .045f;
            if (last.TryGetValue(cue, out var previous) && time - previous < interval) return false;
            last[cue] = time;
            if (SoundsEnabled && audio != null && !audio.Muted && audio.MasterVolume > 0 && audio.EffectsVolume > 0)
            {
                var sound = (Profile.sounds ?? Array.Empty<UIFeedbackProfile.Sound>()).FirstOrDefault(item => item != null && item.cue == cue);
                var clip = sound != null && sound.clip ? sound.clip : Profile.fallbackTones ? Tone(cue) : null;
                audio.PlayEffect(clip, sound == null ? .6f : sound.volume);
            }
            if (Played != null) foreach (Action<UICue, Vector2?> listener in Played.GetInvocationList())
                try { listener(cue, screenPosition); } catch (Exception error) { Debug.LogException(error); }
            return true;
        }
        private AudioClip Tone(UICue cue)
        {
            if (tones.TryGetValue(cue, out var existing)) return existing;
            int rate = 22050;
            float duration = cue == UICue.Reward || cue == UICue.Win ? .32f : cue == UICue.Lose ? .25f : .09f;
            var data = new float[Mathf.CeilToInt(rate * duration)];
            float low;
            switch (cue)
            {
                case UICue.Click: low = 640; break; case UICue.Hover: low = 760; break;
                case UICue.Open: low = 440; break; case UICue.Close: low = 600; break;
                case UICue.Success: low = 880; break; case UICue.Purchase: low = 1040; break;
                case UICue.Change: low = 920; break;
                case UICue.Reward: low = 660; break; case UICue.Error: low = 185; break;
                case UICue.Lose: low = 220; break; default: low = 520; break;
            }
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, normalized = t / duration;
                float frequency = low * (cue == UICue.Reward || cue == UICue.Win ? Mathf.Pow(2, Mathf.Floor(normalized * 3) / 3) : cue == UICue.Close || cue == UICue.Lose ? 1 - normalized * .35f : 1 + normalized * .15f);
                phase += 2 * Math.PI * frequency / rate;
                float envelope = Mathf.Min(1, t / .006f) * Mathf.Pow(1 - normalized, 2);
                data[i] = (float)Math.Sin(phase) * envelope * .17f;
            }
            var clip = AudioClip.Create("BaseUnity " + cue, data.Length, 1, rate, false); clip.SetData(data, 0); tones.Add(cue, clip); return clip;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; Played = null;
            foreach (var clip in tones.Values) if (clip) UnityEngine.Object.Destroy(clip);
            tones.Clear(); last.Clear(); if (Profile) UnityEngine.Object.Destroy(Profile);
        }
    }
}
