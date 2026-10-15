using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace BaseUnity.Scenes
{
    /// <summary>Unity main thread only. Cancellation is reported after an already-started Unity load finishes.</summary>
    public sealed class SceneService
    {
        public bool IsBusy { get; private set; }

        public async Task LoadAsync(string name, LoadSceneMode mode = LoadSceneMode.Single,
            IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Scene name is required.", nameof(name));
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBusy) throw new InvalidOperationException("A scene operation is already running.");
            IsBusy = true;
            try
            {
                var operation = SceneManager.LoadSceneAsync(name, mode);
                if (operation == null) throw new InvalidOperationException($"Cannot load scene {name}.");
                Exception progressError = null;
                // Keep the busy flag until the actual engine operation completes, even after cancellation.
                while (!operation.isDone)
                {
                    if (!cancellationToken.IsCancellationRequested && progressError == null)
                    {
                        try { progress?.Report(operation.progress); }
                        catch (Exception error) { progressError = error; }
                    }
                    await Task.Yield();
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (progressError != null) throw new InvalidOperationException("Scene loaded, but progress reporting failed.", progressError);
                progress?.Report(1);
            }
            finally { IsBusy = false; }
        }

        public async Task UnloadAsync(string name)
        {
            if (IsBusy) throw new InvalidOperationException("A scene operation is already running.");
            IsBusy = true;
            try
            {
                var operation = SceneManager.UnloadSceneAsync(name);
                if (operation == null) throw new InvalidOperationException($"Cannot unload scene {name}.");
                while (!operation.isDone) await Task.Yield();
            }
            finally { IsBusy = false; }
        }
    }
}
