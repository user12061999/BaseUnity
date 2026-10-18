using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Save;
using UnityEngine;

namespace BaseUnity.Features
{
    public enum NotificationPermission { Unknown, Denied, Granted }
    public sealed class LocalNotification
    {
        public int id;
        public string title, message, data;
        public DateTime fireUtc;
    }
    public interface ILocalNotificationBackend : IDisposable
    {
        NotificationPermission Permission { get; }
        void Initialize(LocalNotificationSettings settings);
        Task<NotificationPermission> RequestPermissionAsync(CancellationToken cancellation);
        void Schedule(LocalNotification notification, LocalNotificationSettings settings);
        void Cancel(int id);
    }
    public abstract class LocalNotificationProvider : ScriptableObject { public abstract ILocalNotificationBackend Create(); }
    public sealed class LocalNotificationService : IDisposable
    {
        [Serializable] public sealed class Data { public int schema = 1; public bool optedIn; public List<int> owned = new List<int>(); }
        private const string Slot = "baseunity_notifications";
        private readonly LocalNotificationSettings settings;
        private readonly ISaveStore saves;
        private readonly Func<ILocalNotificationBackend> factory;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private CancellationTokenSource permissionRequest;
        private ILocalNotificationBackend backend;
        private Data state = new Data();
        private bool disposed;
        public string LastError { get; private set; }
        public string LoadError { get; private set; }
        public bool Enabled => settings.enabled && !disposed;
        public bool OptedIn => state.optedIn;
        public bool IsBusy => permissionRequest != null;
        public NotificationPermission Permission => backend?.Permission ?? NotificationPermission.Unknown;
        public LocalNotificationService(LocalNotificationSettings settings, ISaveStore saves, Func<ILocalNotificationBackend> factory = null)
        {
            this.settings = settings ?? new LocalNotificationSettings(); this.saves = saves; this.factory = factory;
            try { state = ModulePersistence.Load(saves, Slot, Valid, state); }
            catch (Exception error) { LoadError = LastError = error.Message; }
        }
        private static bool Valid(Data value)
        {
            if (value.schema > 1) throw new NotSupportedException("Notification settings need a newer BaseUnity.");
            return value.schema == 1 && value.owned != null && value.owned.Count <= 64 && value.owned.All(x => x > 0) && value.owned.Distinct().Count() == value.owned.Count;
        }
        private ILocalNotificationBackend Backend()
        {
            if (backend == null) { var created = factory?.Invoke(); if (created != null) { try { created.Initialize(settings); backend = created; } catch { created.Dispose(); throw; } } }
            return backend ?? throw new InvalidOperationException("Assign a Mobile Notifications provider.");
        }
        public void Initialize()
        {
            if (disposed || LoadError != null) return;
            try { if ((!Enabled || !state.optedIn) && state.owned.Count > 0) Disable(); }
            catch (Exception error) { LastError = error.Message; }
        }
        public async Task<bool> EnableAsync(CancellationToken cancellation = default)
        {
            if (!Enabled || IsBusy || LoadError != null) return false;
            var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellation); permissionRequest = request;
            try
            {
                var result = await FeatureTasks.Wait(Backend().RequestPermissionAsync(request.Token), request.Token);
                request.Token.ThrowIfCancellationRequested();
                if (result != NotificationPermission.Granted) { LastError = "Notification permission was not granted."; return false; }
                return Commit(next => next.optedIn = true);
            }
            catch (Exception error) { if (!(error is OperationCanceledException)) LastError = error.Message; return false; }
            finally { if (permissionRequest == request) permissionRequest = null; request.Dispose(); }
        }
        public bool Disable()
        {
            if (disposed || LoadError != null) return false;
            permissionRequest?.Cancel();
            if (!Commit(next => next.optedIn = false)) return false;
            try { foreach (int id in state.owned.ToArray()) { Backend().Cancel(id); if (!Commit(next => next.owned.Remove(id))) return false; } return true; }
            catch (Exception error) { LastError = error.Message; return false; }
        }
        public bool Schedule(LocalNotification notification)
        {
            if (!Enabled || !OptedIn || LoadError != null || notification == null || notification.id <= 0 || notification.fireUtc.Kind != DateTimeKind.Utc || notification.fireUtc <= DateTime.UtcNow || string.IsNullOrWhiteSpace(notification.title)) return false;
            try
            {
                if (Backend().Permission != NotificationPermission.Granted) return false;
                if (!state.owned.Contains(notification.id) && (state.owned.Count >= 64 || !Commit(next => next.owned.Add(notification.id)))) return false;
                Backend().Schedule(notification, settings); LastError = null; return true;
            }
            catch (Exception error) { LastError = error.Message; return false; }
        }
        public bool ScheduleDailyReminder(DateTime fireUtc) => Schedule(new LocalNotification { id = settings.dailyReminderId, title = settings.dailyTitle, message = settings.dailyMessage, fireUtc = fireUtc, data = "daily_reward" });
        public bool Cancel(int id)
        {
            if (disposed || !state.owned.Contains(id)) return false;
            try { Backend().Cancel(id); return Commit(next => next.owned.Remove(id)); }
            catch (Exception error) { LastError = error.Message; return false; }
        }
        private bool Commit(Action<Data> edit)
        {
            if (LoadError != null) return false;
            try { var next = JsonUtility.FromJson<Data>(JsonUtility.ToJson(state)); edit(next); saves.Save(Slot, next); state = next; LastError = null; return true; }
            catch (Exception error) { LastError = error.Message; return false; }
        }
        public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); backend?.Dispose(); lifetime.Dispose(); }
    }
}
