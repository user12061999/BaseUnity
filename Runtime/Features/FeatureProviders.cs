using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BaseUnity.Features
{
    public enum FeatureStatus { Disabled, MissingProvider, Initializing, Ready, Failed }
    public enum AdFormat { Banner, Interstitial, Rewarded }
    public enum AdResult { Disabled, Unavailable, Busy, Cooldown, Closed, Rewarded, Failed }

    public abstract class AdsProvider : ScriptableObject { public abstract IAdsBackend Create(); }
    public interface IAdsBackend : IDisposable
    {
        Task InitializeAsync(AdsSettings settings, CancellationToken cancellation);
        Task<bool> LoadAsync(AdFormat format, CancellationToken cancellation);
        bool IsReady(AdFormat format);
        Task<AdResult> ShowAsync(AdFormat format, CancellationToken cancellation);
        void HideBanner();
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
    internal static class FeatureTasks
    {
        public static async Task<T> Wait<T>(Task<T> task, CancellationToken cancellation)
        {
            var cancelled = new TaskCompletionSource<bool>();
            using (cancellation.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task)
                {
                    // Observe late SDK failures without blocking shutdown or leaking an unobserved exception.
                    _ = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    throw new OperationCanceledException(cancellation);
                }
                return await task;
            }
        }
        public static async Task Wait(Task task, CancellationToken cancellation)
        { await Wait(Complete(task), cancellation); }
        private static async Task<bool> Complete(Task task) { await task; return true; }
    }
}
