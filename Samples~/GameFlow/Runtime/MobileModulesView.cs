using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    /// <summary>Project-owned presentation. Runtime changes content and visibility, preserving the authored theme.</summary>
    public sealed class MobileModulesView : MonoBehaviour
    {
        public RectTransform content, menu, menuPanel, popup, tutorial, toast, loading;
        public Text popupTitle, popupMessage, tutorialMessage, toastMessage, loadingMessage;
        public Button popupAccept, popupCancel, tutorialNext, tutorialSkip;
        public RectTransform[] masks, borders;
        public Button buttonTemplate;
        public Text labelTemplate;
        public RectTransform panelTemplate;
    }
}
