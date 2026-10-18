using BaseUnity.Features;
using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Attach to an existing button, or create it in a custom settings screen.</summary>
    public sealed class GameFlowPrivacyButton : MonoBehaviour
    {
        private ConsentService consent;
        private Button button;
        private Text label;
        private Text error;
        public void Initialize(ConsentService value, Text errorLabel = null)
        {
            consent = value; error = errorLabel; button = GetComponent<Button>(); label = GetComponentInChildren<Text>();
            button.onClick.AddListener(Open); consent.Changed += Refresh; Refresh();
        }
        private void Refresh()
        {
            gameObject.SetActive(consent.PrivacyOptionsRequired || (consent.Enabled && consent.Status == FeatureStatus.Failed));
            button.interactable = !consent.IsBusy;
            if (label) label.text = consent.PrivacyOptionsRequired ? "PRIVACY OPTIONS" : "RETRY PRIVACY";
        }
        private async void Open()
        {
            if (error) error.text = "";
            bool success = consent.PrivacyOptionsRequired ? await consent.ShowPrivacyOptionsAsync() : await consent.RetryAsync();
            if (!this) return;
            if (!success && error) error.text = consent.LastError ?? "Privacy options are unavailable. Please try again.";
            Refresh();
        }
        private void OnDestroy() { if (consent != null) consent.Changed -= Refresh; }
        public static void AddToSettings(Button template, ConsentService consent, Text error)
        {
            var button = Instantiate(template, template.transform.parent);
            button.name = "Privacy Options"; button.onClick = new Button.ButtonClickedEvent();
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(0, 420); rect.sizeDelta = new Vector2(600, 65);
            var label = button.GetComponentInChildren<Text>();
            if (label)
            {
                label.fontSize = 26;
                label.rectTransform.sizeDelta = new Vector2(576, 53);
                label.rectTransform.anchoredPosition = Vector2.zero;
            }
            button.gameObject.AddComponent<GameFlowPrivacyButton>().Initialize(consent, error);
        }
    }
}
