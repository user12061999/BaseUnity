using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.RemoteConfig;
using UnityEngine;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/Firebase")]
    public sealed class FirebaseSdkProvider : FirebaseProvider
    {
        public override IFirebaseBackend Create() => new Backend();
        private sealed class Backend : IFirebaseBackend
        {
            private FirebaseSettings settings;
            private bool disposed, ready;
            public async Task InitializeAsync(FirebaseSettings value, CancellationToken token)
            {
                settings = value;
#if UNITY_EDITOR
                // Avoid native DefaultInstance errors in offline Editor projects without app credentials.
                if (!System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, "google-services-desktop.json")) &&
                    !System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, "google-services.json")))
                    throw new InvalidOperationException("Firebase Editor configuration is missing. Import the official app config through Feature Manager and let Firebase generate StreamingAssets/google-services-desktop.json.");
#endif
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                token.ThrowIfCancellationRequested(); if (disposed) throw new ObjectDisposedException("Firebase backend");
                if (status != DependencyStatus.Available) throw new InvalidOperationException("Firebase dependencies: " + status);
                var app = FirebaseApp.DefaultInstance;
                ready = true;
                // Native auto-collection is disabled by the supplied platform config until this opt-in.
                SetCollection(settings.analytics, settings.crashlytics);
                if (settings.remoteConfig)
                {
                    var remote = FirebaseRemoteConfig.DefaultInstance;
                    var defaults = new Dictionary<string, object>();
                    foreach (var item in settings.defaults ?? Array.Empty<RemoteDefault>())
                        if (item != null && !string.IsNullOrWhiteSpace(item.key)) defaults[item.key] = item.value ?? "";
                    await remote.SetDefaultsAsync(defaults);
                    token.ThrowIfCancellationRequested();
                    await remote.SetConfigSettingsAsync(new ConfigSettings {
                        MinimumFetchIntervalInMilliseconds = (ulong)Math.Max(0, settings.minimumFetchIntervalSeconds) * 1000,
                        FetchTimeoutInMilliseconds = (ulong)Math.Max(1, settings.requestTimeoutSeconds) * 1000
                    });
                    token.ThrowIfCancellationRequested();
                }
            }
            public void SetCollection(bool analytics, bool crashlytics)
            {
                if (!ready) return;
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(analytics && !disposed);
                Crashlytics.IsCrashlyticsCollectionEnabled = crashlytics && !disposed;
            }
            public void LogEvent(string name, IDictionary<string, string> parameters)
            {
                if (disposed || !settings.analytics) return;
                FirebaseAnalytics.LogEvent(name, parameters == null ? Array.Empty<Parameter>() : parameters.Select(p => new Parameter(p.Key, p.Value)).ToArray());
            }
            public void RecordException(Exception exception) { if (!disposed && settings.crashlytics) Crashlytics.LogException(exception); }
            public async Task<IDictionary<string, string>> FetchAsync(CancellationToken token)
            {
                var remote = FirebaseRemoteConfig.DefaultInstance;
                await remote.FetchAndActivateAsync();
                token.ThrowIfCancellationRequested();
                if (disposed) throw new ObjectDisposedException("Firebase backend");
                return remote.AllValues.ToDictionary(item => item.Key, item => item.Value.StringValue);
            }
            public void Dispose() { disposed = true; SetCollection(false, false); }
        }
    }
}
