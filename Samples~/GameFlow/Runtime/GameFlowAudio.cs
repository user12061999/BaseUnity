using BaseUnity.Audio;
using UnityEngine;
namespace BaseUnity.GameFlow
{
    /// <summary>Owns only generated demo clips. Assigned project clips remain owned by the project.</summary>
    public sealed class GameFlowAudio : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private AudioService audioService;
        private AudioClip homeDemo, gameDemo;
        public void Initialize(GameFlowBootstrap value)
        {
            flow = value; audioService = flow.Services.Get<AudioService>();
            var profile = flow.Config ? flow.Config.audioProfile : null;
            if (!profile || !profile.enabled) return;
            int voices = Mathf.Clamp(profile.maxEffectVoices, 1, 64);
            audioService.ConfigureEffects(voices, Mathf.Clamp(profile.maxSimultaneousPerClip, 1, voices), Seconds(profile.effectCooldownSeconds));
            flow.Changed += Refresh; Refresh();
        }
        private static float Seconds(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
        private void Refresh()
        {
            var profile = flow.Config ? flow.Config.audioProfile : null;
            if (!profile || !profile.enabled) { audioService.FadeOutMusic(); return; }
            AudioClip clip;
            switch (flow.Phase)
            {
                case FlowPhase.Home: case FlowPhase.LevelSelect:
                    clip = profile.homeMusic;
                    if (!clip && profile.demoMusic) clip = homeDemo ? homeDemo : (homeDemo = CreateLoop("Home demo", new[] { 261.63f, 329.63f, 392f, 329.63f, 293.66f, 349.23f, 440f, 349.23f }));
                    break;
                case FlowPhase.Preparing: case FlowPhase.Countdown: case FlowPhase.Playing:
                    clip = profile.inGameMusic;
                    if (!clip && profile.demoMusic) clip = gameDemo ? gameDemo : (gameDemo = CreateLoop("InGame demo", new[] { 392f, 493.88f, 440f, 523.25f, 392f, 440f, 349.23f, 293.66f }));
                    break;
                case FlowPhase.Result:
                    clip = flow.Outcome == LevelOutcome.Win ? profile.winMusic : profile.loseMusic;
                    if (!clip) clip = profile.homeMusic ? profile.homeMusic : homeDemo;
                    break;
                case FlowPhase.Error: audioService.FadeOutMusic(Seconds(profile.crossfadeSeconds)); return;
                default: return; // Loading, pause and ending retain current music.
            }
            if (clip) audioService.CrossFadeMusic(clip, Seconds(profile.crossfadeSeconds));
            else audioService.FadeOutMusic(Seconds(profile.crossfadeSeconds));
        }
        private static AudioClip CreateLoop(string name, float[] notes)
        {
            const int rate = 22050, noteSamples = 11025;
            var samples = new float[noteSamples * notes.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                int n = i / noteSamples; float t = (i % noteSamples) / (float)rate;
                float envelope = Mathf.Min(1, t / .02f) * Mathf.Max(0, 1 - t / .5f);
                samples[i] = Mathf.Sin(2 * Mathf.PI * notes[n] * t) * envelope * .035f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        private void OnDestroy()
        {
            if (flow) flow.Changed -= Refresh;
            if (audioService != null && ((homeDemo && audioService.MusicClip == homeDemo) || (gameDemo && audioService.MusicClip == gameDemo))) audioService.StopMusic();
            if (homeDemo) Destroy(homeDemo); if (gameDemo) Destroy(gameDemo);
        }
    }
}
