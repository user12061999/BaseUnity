using UnityEngine;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    public sealed class HomeHubView : MonoBehaviour
    {
        public GameObject home, modal;
        public Text wallet, playerName, level, gameTitle;
        public Image avatar;
        public Button play, levels, settings, store, quests, daily, inventory, more;
        public GameObject dailyDot, questDot;
        public Text questCount;
        public HubPageView storePage, questPage;
        private HubPageView current;
        public void SelectPage(bool store) { current = store ? storePage : questPage; storePage.gameObject.SetActive(store); questPage.gameObject.SetActive(!store); }
        public Text heading => current.heading;
        public Text message => current.message;
        public Text countdown => questPage.countdown;
        public Text pageNumber => current.pageNumber;
        public Button coinsTab => storePage.primary;
        public Button iapTab => storePage.secondary;
        public Button dailyTab => questPage.primary;
        public Button achievementTab => questPage.secondary;
        public Button previous => current.previous;
        public Button next => current.next;
        public Button restore => storePage.restore;
        public Button retryPending => storePage.retryPending;
        public Transform rows => current.rows;
        public HubCardView cardPrefab;
        public HubCardView questCardPrefab;
        public GameObject empty => current.empty;
        public GameObject confirmation;
        public Text confirmMessage;
        public Button confirmAccept, confirmCancel;
    }
}
