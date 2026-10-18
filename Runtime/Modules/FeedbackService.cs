using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BaseUnity.Features
{
    public sealed class ToastMessage { public string text; public float seconds; public bool error; }
    public sealed class FeedbackService : IDisposable
    {
        private readonly FeedbackSettings settings;
        private readonly PopupService popups;
        private readonly Dictionary<long, string> loading = new Dictionary<long, string>();
        private long next;
        private bool disposed;
        public bool Enabled => settings.enabled && !disposed;
        public bool IsLoading => Enabled && loading.Count > 0;
        public string LoadingMessage => loading.Count == 0 ? "" : loading.OrderByDescending(x => x.Key).First().Value;
        public event Action<ToastMessage> ToastRequested;
        public event Action Changed;
        public FeedbackService(FeedbackSettings settings, PopupService popups) { this.settings = settings ?? new FeedbackSettings(); this.popups = popups; }
        public void Toast(string text, bool error = false)
        { if (Enabled && !string.IsNullOrWhiteSpace(text)) ToastRequested?.Invoke(new ToastMessage { text = text, error = error, seconds = Math.Max(.1f, settings.toastSeconds) }); }
        public IDisposable Loading(string message = "Loading...")
        {
            if (!Enabled) return new ModuleScope(null);
            long id = ++next; loading[id] = message; Changed?.Invoke();
            return new ModuleScope(() => { loading.Remove(id); if (!disposed) Changed?.Invoke(); });
        }
        public async Task<bool> ConfirmAsync(string title, string message, string id = null, int priority = 0, string scope = "game", CancellationToken cancellation = default)
        {
            if (!Enabled) return false;
            return await popups.Enqueue(id ?? Guid.NewGuid().ToString("N"), new PopupContent { title = title, message = message }, priority, scope, cancellation) == PopupResult.Accepted;
        }
        public void Dispose() { disposed = true; loading.Clear(); ToastRequested = null; Changed = null; }
    }
}
