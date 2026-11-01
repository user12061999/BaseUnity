using UnityEngine;
namespace BaseUnity.UI
{
    /// <summary>Optional local UI sound. Connect Play to UIButtonFeedback.clicked; Audio module is not required.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class UIAudioFeedback : MonoBehaviour
    {
        public AudioClip clip;
        [Range(0,1)] public float volume=1;
        public bool muted;
        private AudioSource source;
        private void Awake() { source=GetComponent<AudioSource>(); source.playOnAwake=false; source.spatialBlend=0; }
        public void Play() { if(!muted&&clip)source.PlayOneShot(clip,volume); }
    }
}
