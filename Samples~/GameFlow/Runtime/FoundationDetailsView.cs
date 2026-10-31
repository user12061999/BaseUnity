using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class FoundationDetailsView : MonoBehaviour
    {
        private static readonly Color[] AvatarColors={Color.blue,new Color(.2f,.6f,.3f),new Color(.7f,.35f,.1f),new Color(.6f,.2f,.6f)};
        public static Color AvatarTint(int index) => AvatarColors[Mathf.Clamp(index,0,AvatarColors.Length-1)];
        public Button energy, profile, controls;
        public GameObject profilePanel, levelPopup;
        public InputField playerName;
        public Button saveName, avatar, closeLevel;
        public Text profileTitle, nameCaption, avatarLabel, experience, levelMessage;
        public Image experienceFill, avatarBackground;
    }
}
