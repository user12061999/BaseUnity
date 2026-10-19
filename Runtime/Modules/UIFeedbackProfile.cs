using System;
using UnityEngine;

namespace BaseUnity.Features
{
    public enum UICue { Click, Hover, Open, Close, Success, Error, Purchase, Reward, Win, Lose, Change }
    [CreateAssetMenu(menuName = "BaseUnity/UI Feedback Profile")]
    public sealed class UIFeedbackProfile : ScriptableObject
    {
        [Serializable] public sealed class Sound
        {
            public UICue cue;
            public AudioClip clip;
            [Range(0, 1)] public float volume = .6f;
        }
        [Tooltip("Use quiet generated tones when a cue has no custom clip. No external sound pack required.")]
        public bool fallbackTones = true;
        public Sound[] sounds = Array.Empty<Sound>();
        public Sprite rewardSprite;
        [Range(.8f, 1)] public float pressedScale = .94f;
        [Range(.04f, .4f)] public float buttonSeconds = .09f;
        [Range(.08f, .6f)] public float panelSeconds = .18f;
        [Range(.2f, 2)] public float rewardSeconds = .75f;
    }
}
