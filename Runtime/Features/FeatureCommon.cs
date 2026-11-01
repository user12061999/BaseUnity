using System;
using System.Threading;
using System.Threading.Tasks;
namespace BaseUnity.Features {
    public enum FeatureStatus { Disabled, MissingProvider, Initializing, Ready, Failed }
    public static class FeatureTasks
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
