using System.Threading.Tasks;
using BaseUnity.Core;
using BaseUnity.Features;
using UnityEngine;

namespace BaseUnity.Samples
{
    /// <summary>Connect methods to your own buttons. Gameplay references no optional SDK types.</summary>
    public sealed class MobileModulesExample : MonoBehaviour
    {
        public string textAddress, tutorialId, productId;
        private FeatureServices Features => GameBootstrap.Instance.Services.Get<FeatureServices>();
        public async void PreviewText() { await Preview(); }
        private async Task Preview()
        {
            using (Features.Feedback.Loading())
            using (var lease = await Features.Assets.LoadAsync<TextAsset>(textAddress, destroyCancellationToken))
            { if (lease != null && this) Debug.Log(lease.Asset.text, this); }
        }
        public void StartTutorial() => Features.Tutorial.Start(tutorialId);
        public void CompleteTutorialAction(string key) => Features.Tutorial.Advance(key);
        public void Tap() => Features.Haptics.Play(HapticKind.Light);
        public async void Confirm()
        {
            if (await Features.Feedback.ConfirmAsync("Continue?", "Confirm this action", scope: "scene", cancellation: destroyCancellationToken))
                Features.Feedback.Toast("Saved");
        }
        // Trigger explicitly from the player's choice, never automatically from Awake/Start.
        public async void EnableReminders()
        {
            if (await Features.Notifications.EnableAsync(destroyCancellationToken))
                Features.Notifications.ScheduleDailyReminder(System.DateTime.UtcNow.Date.AddDays(1).AddMinutes(1));
        }
        public void DisableReminders() => Features.Notifications.Disable();
        public async void Purchase() { await Features.Purchases.BuyAsync(productId); }
        public async void RestorePurchases() { await Features.Purchases.RestoreAsync(); }
    }
}
