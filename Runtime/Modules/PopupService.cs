using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BaseUnity.Features
{
    public enum PopupResult { Accepted, Dismissed, Cancelled, Unavailable }
    public sealed class PopupContent
    {
        public string title, message, accept = "OK", cancel = "Cancel";
    }
    public sealed class PopupRequest
    {
        public string Id { get; internal set; }
        public string Scope { get; internal set; }
        public int Priority { get; internal set; }
        public PopupContent Content { get; internal set; }
    }
    /// <summary>Main-thread queue. Priority applies to pending entries, never preempts an active interaction.</summary>
    public sealed class PopupService : IDisposable
    {
        private sealed class Entry
        {
            public PopupRequest request;
            public long sequence;
            public readonly TaskCompletionSource<PopupResult> completion = new TaskCompletionSource<PopupResult>();
            public CancellationTokenRegistration cancellation;
        }
        private readonly PopupSettings settings;
        private readonly List<Entry> pending = new List<Entry>();
        private readonly SynchronizationContext context = SynchronizationContext.Current;
        private Entry active;
        private int presenters, suspensions;
        private long sequence;
        private bool disposed;
        public PopupRequest Active => active?.request;
        public bool IsSuspended => suspensions > 0;
        public int PendingCount => pending.Count;
        public bool Enabled => settings.enabled && !disposed;
        public event Action Changed;
        public PopupService(PopupSettings settings) { this.settings = settings ?? new PopupSettings(); }
        public IDisposable AttachPresenter()
        {
            if (disposed) return new ModuleScope(null);
            presenters++; return new ModuleScope(() => { if (--presenters == 0) CancelAll(); });
        }
        public IDisposable Suspend()
        {
            suspensions++; Changed?.Invoke();
            return new ModuleScope(() => { suspensions = Math.Max(0, suspensions - 1); Pump(); Changed?.Invoke(); });
        }
        public Task<PopupResult> Enqueue(string id, PopupContent content, int priority = 0, string scope = "game", CancellationToken cancellation = default)
        {
            if (!Enabled || presenters == 0 || content == null || string.IsNullOrWhiteSpace(id) || cancellation.IsCancellationRequested)
                return Task.FromResult(PopupResult.Unavailable);
            var existing = active?.request.Id == id ? active : pending.FirstOrDefault(x => x.request.Id == id);
            if (existing != null) return existing.completion.Task;
            if (pending.Count + (active == null ? 0 : 1) >= Math.Max(1, settings.capacity)) return Task.FromResult(PopupResult.Unavailable);
            var entry = new Entry { sequence = sequence++, request = new PopupRequest { Id = id, Scope = scope, Priority = priority,
                Content = new PopupContent { title = content.title, message = content.message, accept = content.accept, cancel = content.cancel } } };
            pending.Add(entry);
            entry.cancellation = cancellation.Register(() => {
                if (context != null && SynchronizationContext.Current != context) context.Post(_ => Cancel(entry), null);
                else Cancel(entry);
            });
            Pump(); return entry.completion.Task;
        }
        private void Pump()
        {
            if (disposed || active != null || IsSuspended || pending.Count == 0) return;
            active = pending.OrderByDescending(x => x.request.Priority).ThenBy(x => x.sequence).First();
            pending.Remove(active); Changed?.Invoke();
        }
        public bool Resolve(string id, bool accepted)
        {
            if (active == null || active.request.Id != id || IsSuspended) return false;
            Finish(active, accepted ? PopupResult.Accepted : PopupResult.Dismissed); return true;
        }
        private void Cancel(Entry entry) { if (entry == active || pending.Contains(entry)) Finish(entry, PopupResult.Cancelled); }
        private void Finish(Entry entry, PopupResult result)
        {
            if (active == entry) active = null; else pending.Remove(entry);
            entry.cancellation.Dispose(); Pump(); Changed?.Invoke(); entry.completion.TrySetResult(result);
        }
        public void CancelScope(string scope)
        {
            var entries = pending.Where(x => x.request.Scope == scope).ToList();
            if (active?.request.Scope == scope) entries.Add(active);
            foreach (var entry in entries) Cancel(entry);
        }
        public void CancelAll()
        {
            var entries = pending.ToList(); pending.Clear(); if (active != null) entries.Add(active); active = null;
            foreach (var entry in entries) { entry.cancellation.Dispose(); entry.completion.TrySetResult(PopupResult.Cancelled); }
            Changed?.Invoke();
        }
        public void Dispose() { if (disposed) return; disposed = true; CancelAll(); Changed = null; }
    }
}
