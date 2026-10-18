using System;
using System.Threading;
using BaseUnity.Features;
using BaseUnity.Scenes;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace BaseUnity.Async
{
    /// <summary>Compatibility adapters; the underlying Task-based services retain their cancellation and SDK ownership rules.</summary>
    public static class UniTaskServiceExtensions
    {
        public static UniTask LoadUniTaskAsync(this SceneService service, string scene, LoadSceneMode mode = LoadSceneMode.Single,
            IProgress<float> progress = null, CancellationToken cancellationToken = default)
        { Require(service); return service.LoadAsync(scene, mode, progress, cancellationToken).AsUniTask(); }
        public static UniTask UnloadUniTaskAsync(this SceneService service, string scene)
        { Require(service); return service.UnloadAsync(scene).AsUniTask(); }
        public static UniTask InitializeUniTaskAsync(this FeatureServices service)
        { Require(service); return service.InitializeAsync().AsUniTask(); }
        public static UniTask<bool> InitializeUniTaskAsync(this AdsService service)
        { Require(service); return service.InitializeAsync().AsUniTask(); }
        public static UniTask<bool> LoadUniTaskAsync(this AdsService service, AdFormat format)
        { Require(service); return service.LoadAsync(format).AsUniTask(); }
        public static UniTask<AdResult> ShowUniTaskAsync(this AdsService service, AdFormat format)
        { Require(service); return service.ShowAsync(format).AsUniTask(); }
        public static UniTask<bool> InitializeUniTaskAsync(this FirebaseService service)
        { Require(service); return service.InitializeAsync().AsUniTask(); }
        public static UniTask<bool> FetchRemoteConfigUniTaskAsync(this FirebaseService service)
        { Require(service); return service.FetchRemoteConfigAsync().AsUniTask(); }
        private static void Require(object service)
        { if (service == null) throw new ArgumentNullException(nameof(service)); }
    }
}
