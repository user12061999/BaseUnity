using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace BaseUnity.Features {
    [Serializable] public sealed class RemoteDefault
    {
        public string key;
        public string value;
    }
    [Serializable] public sealed class FirebaseSettings
    {
        public bool enabled;
        public bool analytics = true, crashlytics = true, remoteConfig = true;
        [Min(1)] public int requestTimeoutSeconds = 15;
        [Min(0)] public int minimumFetchIntervalSeconds = 3600;
        public RemoteDefault[] defaults = Array.Empty<RemoteDefault>();
    }
    public abstract class FirebaseProvider : ScriptableObject { public abstract IFirebaseBackend Create(); }
    public interface IFirebaseBackend : IDisposable
    {
        Task InitializeAsync(FirebaseSettings settings, CancellationToken cancellation);
        void SetCollection(bool analytics, bool crashlytics);
        void LogEvent(string name, IDictionary<string, string> parameters);
        void RecordException(Exception exception);
        Task<IDictionary<string, string>> FetchAsync(CancellationToken cancellation);
    }
}
