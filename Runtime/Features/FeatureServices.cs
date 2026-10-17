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
        public OfflineService Offline { get; private set; }
        public LocalizationService Localization { get; private set; }
        public GameIntegrationSettings GameIntegration => UnityEngine.JsonUtility.FromJson<GameIntegrationSettings>(UnityEngine.JsonUtility.ToJson(runtimeConfig.gameIntegration ?? new GameIntegrationSettings()));
        public FeatureServices(FeatureConfig source)
        {
            runtimeConfig = source ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<FeatureConfig>();
            Ads = new AdsService(runtimeConfig.ads, runtimeConfig.adsProvider ? (Func<IAdsBackend>)runtimeConfig.adsProvider.Create : null);
            Firebase = new FirebaseService(runtimeConfig.firebase, runtimeConfig.firebaseProvider ? (Func<IFirebaseBackend>)runtimeConfig.firebaseProvider.Create : null);
        }
        public void Register(ServiceRegistry services)
        {
            services.Register(this); services.Register(Ads); services.Register(Firebase);
            var saves = services.Get<BaseUnity.Save.ISaveStore>();
            Offline = new OfflineService(runtimeConfig.offline, saves);
            Localization = new LocalizationService(runtimeConfig.localization, saves);
            services.Register(Offline); services.Register(Localization);
        }
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
