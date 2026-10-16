using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace BaseUnity.Features
{
    /// <summary>Optional Firebase facade. Local defaults remain usable offline and when disabled.</summary>
    public sealed class FirebaseService : IDisposable
    {
        private readonly FirebaseSettings settings;
        private readonly Func<IFirebaseBackend> factory;
        private readonly Dictionary<string, string> defaults = new Dictionary<string, string>();
        private Dictionary<string, string> remote = new Dictionary<string, string>();
        private IFirebaseBackend backend;
        private CancellationTokenSource lifetime = new CancellationTokenSource();
        private Task<bool> initialization;
        private bool disposed, fetching;
        public bool Enabled { get; private set; }
        public FeatureStatus Status { get; private set; }
        public string LastError { get; private set; }
        public FirebaseService(FirebaseSettings settings, Func<IFirebaseBackend> factory = null)
        {
            this.settings = settings ?? new FirebaseSettings(); this.factory = factory; Enabled = this.settings.enabled;
            Status = Enabled ? (factory == null ? FeatureStatus.MissingProvider : FeatureStatus.Initializing) : FeatureStatus.Disabled;
            foreach (var item in this.settings.defaults ?? Array.Empty<RemoteDefault>())
                if (item != null && !string.IsNullOrWhiteSpace(item.key)) defaults[item.key] = item.value ?? "";
        }
        public Task<bool> InitializeAsync()
        {
            if (disposed || !Enabled || factory == null || (!settings.analytics && !settings.crashlytics && !settings.remoteConfig)) return Task.FromResult(false);
            return initialization ?? (initialization = InitializeCore());
        }
        private async Task<bool> InitializeCore()
        {
            var token = lifetime.Token;
            try
            {
                Status = FeatureStatus.Initializing; backend = factory();
                if (backend == null) throw new InvalidOperationException("Firebase provider returned no backend.");
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.requestTimeoutSeconds)));
                    await FeatureTasks.Wait(backend.InitializeAsync(settings, timeout.Token), timeout.Token);
                }
                token.ThrowIfCancellationRequested();
                backend.SetCollection(settings.analytics, settings.crashlytics);
                LastError = null; Status = FeatureStatus.Ready; return true;
            }
            catch (Exception error)
            {
                if (!token.IsCancellationRequested) { LastError = error.Message; Status = FeatureStatus.Failed; backend?.Dispose(); backend = null; }
                return false;
            }
        }
        public bool LogEvent(string name, IDictionary<string, string> parameters = null)
        {
            if (disposed || !Enabled || !settings.analytics || Status != FeatureStatus.Ready || string.IsNullOrWhiteSpace(name)) return false;
            try { backend.LogEvent(name, parameters); return true; }
            catch (Exception error) { LastError = error.Message; return false; }
        }
        public bool RecordException(Exception error)
        {
            if (disposed || !Enabled || !settings.crashlytics || Status != FeatureStatus.Ready || error == null) return false;
            try { backend.RecordException(error); return true; }
            catch (Exception failure) { LastError = failure.Message; return false; }
        }
        public async Task<bool> FetchRemoteConfigAsync()
        {
            if (disposed || !Enabled || !settings.remoteConfig || fetching) return false;
            fetching = true; var token = lifetime.Token;
            try
            {
                if (!await InitializeAsync() || token.IsCancellationRequested) return false;
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.requestTimeoutSeconds)));
                    var values = await FeatureTasks.Wait(backend.FetchAsync(timeout.Token), timeout.Token);
                    token.ThrowIfCancellationRequested();
                    remote = values == null ? new Dictionary<string, string>() : new Dictionary<string, string>(values);
                    LastError = null; return true;
                }
            }
            catch (Exception error) { if (!token.IsCancellationRequested) LastError = error.Message; return false; }
            finally { if (!token.IsCancellationRequested) fetching = false; }
        }
        public string GetString(string key, string fallback = "")
        {
            if (key == null) return fallback;
            if (Enabled && settings.remoteConfig && remote.TryGetValue(key, out var value)) return value;
            return defaults.TryGetValue(key, out value) ? value : fallback;
        }
        public bool GetBool(string key, bool fallback = false) => bool.TryParse(GetString(key), out var value) ? value : fallback;
        public long GetLong(string key, long fallback = 0) => long.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        public void SetEnabled(bool enabled)
        {
            if (disposed || Enabled == enabled) return;
            Enabled = enabled; if (!enabled) Shutdown();
            Status = enabled ? (factory == null ? FeatureStatus.MissingProvider : FeatureStatus.Initializing) : FeatureStatus.Disabled;
        }
        private void Shutdown()
        {
            lifetime.Cancel(); backend?.Dispose(); backend = null; remote.Clear(); initialization = null; fetching = false;
            lifetime.Dispose(); lifetime = new CancellationTokenSource();
        }
        public void Dispose() { if (disposed) return; disposed = true; Enabled = false; Shutdown(); lifetime.Dispose(); Status = FeatureStatus.Disabled; }
    }
}
