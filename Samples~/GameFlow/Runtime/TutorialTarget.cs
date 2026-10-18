using BaseUnity.Features;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    public sealed class TutorialTarget : MonoBehaviour
    {
        public string key;
        private TutorialService tutorial;
        private Button button;
        public void Initialize(string id, TutorialService service)
        {
            if (button) button.onClick.RemoveListener(Activate);
            key = id; tutorial = service; button = GetComponent<Button>();
            if (button) button.onClick.AddListener(Activate);
        }
        public void Activate() { if (tutorial?.Current?.waitForTargetAction == true && tutorial.Current.target == key) tutorial.Advance(key); }
        private void OnDestroy() { if (button) button.onClick.RemoveListener(Activate); }
    }
}
