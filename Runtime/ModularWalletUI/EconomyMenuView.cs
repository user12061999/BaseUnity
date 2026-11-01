using BaseUnity.Core;
using BaseUnity.UI;
namespace BaseUnity.EconomyModules.UI
{
    public sealed class EconomyMenuView : EconomyViewBase
    {
        public UIPanel home,shop,daily,quests,achievements;
        private UIService ui;
        private bool shown;
        protected override bool Bind(ServiceRegistry services) { shown=false;return services.TryGet(out ui); }
        protected override void RefreshView() { if(!shown&&home) { shown=true;ui.ShowScreen(home); } }
        public void Shop() { if(Ready&&shop)ui.PushPopup(shop); }
        public void Daily() { if(Ready&&daily)ui.PushPopup(daily); }
        public void Quests() { if(Ready&&quests)ui.PushPopup(quests); }
        public void Achievements() { if(Ready&&achievements)ui.PushPopup(achievements); }
        public void Close() { if(Ready)ui.Back(); }
    }
}
