using BaseUnity.Features;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    public sealed class InGameBadgeView : MonoBehaviour
    {
        public string badgeId = "gifts";
        public GameObject dot;
        public Text count;
        private InGameNoticeService notices;
        private void OnEnable() { TryBind(); }
        private void Update() { if (notices == null) TryBind(); }
        private void TryBind()
        { var services = GameplayInputGate.Services; if (services == null) return; notices = services.Notices; notices.Changed += Refresh; Refresh(); }
        private void Refresh() { int value = notices != null ? notices.Count(badgeId) : 0; if (dot) dot.SetActive(value > 0); if (count) count.text = value > 99 ? "99+" : value.ToString(); }
        private void OnDisable() { if (notices != null) notices.Changed -= Refresh; notices = null; }
    }
}
