using UnityEngine;
namespace BaseUnity.GameFlow
{
    [CreateAssetMenu(menuName = "BaseUnity/Game Flow Audio Profile")]
    public sealed class GameFlowAudioProfile : ScriptableObject
    {
        public bool enabled = true;
        public AudioClip homeMusic, inGameMusic, winMusic, loseMusic;
        [Tooltip("Play quiet generated sample loops when Home/InGame clips are unassigned.")]
        public bool demoMusic = true;
        [Min(0)] public float crossfadeSeconds = .5f;
        [Range(1, 64)] public int maxEffectVoices = 16;
        [Range(1, 64)] public int maxSimultaneousPerClip = 3;
        [Min(0)] public float effectCooldownSeconds = .04f;
    }
}
