using System;
using System.Threading.Tasks;
using BaseUnity.Core;
using UnityEngine;

namespace BaseUnity.Features
{
    public sealed class FeatureServices : IDisposable
    {
        private readonly FeatureConfig runtimeConfig;
        public AdsService Ads { get; }
        public FirebaseService Firebase { get; }
        public FeatureServices(FeatureConfig source)
        {
            runtimeConfig = source ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<FeatureConfig>();
            Ads = new AdsService(runtimeConfig.ads, runtimeConfig.adsProvider ? (Func<IAdsBackend>)runtimeConfig.adsProvider.Create : null);
            Firebase = new FirebaseService(runtimeConfig.firebase, runtimeConfig.firebaseProvider ? (Func<IFirebaseBackend>)runtimeConfig.firebaseProvider.Create : null);
        }
        public void Register(ServiceRegistry services)
        { services.Register(this); services.Register(Ads); services.Register(Firebase); }
        public async Task InitializeAsync()
        {
            // Neither SDK initialization nor an unavailable network delays the game's OnReady.
            await Task.WhenAll(Ads.InitializeAsync(), Firebase.InitializeAsync());
        }
        public void Dispose()
        {
            Ads.Dispose(); Firebase.Dispose();
            if (runtimeConfig) UnityEngine.Object.Destroy(runtimeConfig);
        }
    }
}
