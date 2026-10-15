using System;
using System.Collections.Generic;

namespace BaseUnity.UI
{
    /// <summary>One active screen and a popup stack. Panels are owned by their scene.</summary>
    public sealed class UIService : IDisposable
    {
        private UIPanel screen;
        private readonly List<UIPanel> popups = new List<UIPanel>();
        public void ShowScreen(UIPanel panel)
        {
            if (!panel) throw new ArgumentNullException(nameof(panel));
            ClosePopups();
            if (screen && screen != panel) screen.Hide();
            screen = panel;
            screen.Show();
        }
        public void PushPopup(UIPanel panel)
        {
            if (!panel) throw new ArgumentNullException(nameof(panel));
            if (panel == screen) throw new InvalidOperationException("A screen cannot also be a popup.");
            popups.RemoveAll(item => !item || item == panel);
            popups.Add(panel);
            panel.transform.SetAsLastSibling();
            panel.Show();
        }
        public bool Back()
        {
            while (popups.Count > 0)
            {
                int last = popups.Count - 1;
                var panel = popups[last];
                popups.RemoveAt(last);
                if (!panel) continue;
                panel.Hide();
                return true;
            }
            return false;
        }
        public void ClosePopups() { while (Back()) { } }
        public void Dispose() { ClosePopups(); if (screen) screen.Hide(); screen = null; }
    }
}
