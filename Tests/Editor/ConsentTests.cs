using System;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using NUnit.Framework;
using System.Linq;
using BaseUnity.Editor;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Tests
{
    public sealed class ConsentTests
    {
        private sealed class Consent : IConsentBackend
        {
            public bool allowed, required;
            public int gathers, forms, resets, disposed;
            public TaskCompletionSource<bool> pending;
            public Exception failure;
            public bool CanRequestAds => allowed;
            public bool PrivacyOptionsRequired => required;
            public Task GatherAsync(ConsentSettings settings, CancellationToken token)
            { gathers++; return pending != null ? pending.Task : failure != null ? Task.FromException(failure) : Task.CompletedTask; }
            public Task ShowPrivacyOptionsAsync(CancellationToken token)
            { forms++; return pending != null ? pending.Task : failure != null ? Task.FromException(failure) : Task.CompletedTask; }
            public void ResetForTesting() { resets++; allowed = required = false; }
            public void Dispose() { disposed++; }
        }
        private sealed class Ads : IAdsBackend
        {
            public int initialized, loaded, disposed;
            public TaskCompletionSource<AdResult> pendingShow;
            public Task InitializeAsync(AdsSettings s, CancellationToken t) { initialized++; return Task.CompletedTask; }
            public Task<bool> LoadAsync(AdFormat f, CancellationToken t) { loaded++; return Task.FromResult(true); }
            public bool IsReady(AdFormat f) => true;
            public Task<AdResult> ShowAsync(AdFormat f, CancellationToken t) => pendingShow?.Task ?? Task.FromResult(AdResult.Rewarded);
            public void HideBanner() { }
            public void Dispose() { disposed++; }
        }
        [Test] public void ValidationChecksConsentOnlyWhenAdsAreEnabled()
        {
            var config = ScriptableObject.CreateInstance<FeatureConfig>();
            try
            {
                Assert.IsFalse(FeatureBuildValidator.Validate(config, BuildTarget.Android).Any(x => x.message.StartsWith("Consent:")));
                config.ads.enabled = true;
                var issues = FeatureBuildValidator.Validate(config, BuildTarget.Android);
                Assert.IsTrue(issues.Any(x => x.message.Contains("assign a Google UMP provider")));
                Assert.IsTrue(issues.Any(x => x.message.Contains("real AdMob App ID")));
                config.consent.enabled = false;
                Assert.IsFalse(FeatureBuildValidator.Validate(config, BuildTarget.Android).Any(x => x.message.StartsWith("Consent:")));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void ValidationRejectsEmptyConsentTestDevices()
        {
            var config = ScriptableObject.CreateInstance<FeatureConfig>();
            try
            {
                config.ads.enabled = true; config.consent.debugMode = true;
                Assert.IsTrue(FeatureBuildValidator.Validate(config, BuildTarget.Android).Any(x => x.message.StartsWith("Consent testing:")));
                config.consent.testDeviceHashedIds = new[] { "device-hash" };
                Assert.IsFalse(FeatureBuildValidator.Validate(config, BuildTarget.Android).Any(x => x.message.StartsWith("Consent testing:")));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public async Task DisabledConsentNeverCreatesBackend()
        {
            int created = 0;
            using (var service = new ConsentService(new ConsentSettings { enabled = false }, () => { created++; return new Consent(); }))
            { Assert.IsTrue(await service.InitializeAsync()); Assert.IsTrue(service.CanRequestAds); Assert.IsFalse(await service.ShowPrivacyOptionsAsync()); Assert.AreEqual(0, created); }
        }
        [Test] public async Task MissingProviderBlocksAllAdsWithoutCreatingAdsBackend()
        {
            int created = 0;
            using (var consent = new ConsentService(new ConsentSettings()))
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => { created++; return new Ads(); }, consent))
            { Assert.IsFalse(await ads.LoadAsync(AdFormat.Rewarded)); Assert.AreEqual(0, created); Assert.IsFalse(consent.CanRequestAds); Assert.AreEqual(FeatureStatus.MissingProvider, consent.Status); }
        }
        [Test] public async Task ConcurrentInitializationWaitsForConsentAndStartsAdsOnce()
        {
            var fake = new Consent { pending = new TaskCompletionSource<bool>() }; var ad = new Ads();
            using (var consent = new ConsentService(new ConsentSettings(), () => fake))
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => ad, consent))
            {
                var first = ads.InitializeAsync(); var second = ads.InitializeAsync();
                Assert.AreSame(first, second); Assert.AreEqual(0, ad.initialized); Assert.IsFalse(consent.CanRequestAds);
                fake.allowed = true; fake.pending.SetResult(true);
                Assert.IsTrue(await first); Assert.AreEqual(1, fake.gathers); Assert.AreEqual(1, ad.initialized);
            }
        }
        [Test] public async Task CompletedFormWithoutPermissionDoesNotStartAds()
        {
            var fake = new Consent(); var ad = new Ads();
            using (var consent = new ConsentService(new ConsentSettings(), () => fake))
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => ad, consent))
            { Assert.IsFalse(await ads.InitializeAsync()); Assert.AreEqual(0, ad.initialized); Assert.IsFalse(consent.CanRequestAds); }
        }
        [Test] public async Task UpdateErrorUsesOnlySdkCachedPermission()
        {
            var fake = new Consent { allowed = true, failure = new Exception("offline") }; var ad = new Ads();
            using (var consent = new ConsentService(new ConsentSettings(), () => fake))
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => ad, consent))
            { Assert.IsTrue(await ads.InitializeAsync()); Assert.AreEqual("offline", consent.LastError); Assert.AreEqual(FeatureStatus.Ready, consent.Status); }
        }
        [Test] public async Task FailedUpdateCanRetryThenLoadAds()
        {
            var fake = new Consent { failure = new Exception("offline") }; var ad = new Ads();
            using (var consent = new ConsentService(new ConsentSettings(), () => fake))
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => ad, consent))
            {
                Assert.IsFalse(await ads.LoadAsync(AdFormat.Banner)); fake.failure = null; fake.allowed = true;
                Assert.IsTrue(await consent.RetryAsync()); Assert.IsTrue(await ads.LoadAsync(AdFormat.Banner));
                Assert.AreEqual(2, fake.gathers); Assert.AreEqual(1, ad.initialized);
            }
        }
        [Test] public async Task PrivacyFormInvalidatesCachedAdsAndSuppressesLateReward()
        {
            var fake = new Consent { allowed = true, required = true }; var ad = new Ads { pendingShow = new TaskCompletionSource<AdResult>() };
            using (var consent = new ConsentService(new ConsentSettings(), () => fake))
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => ad, consent))
            {
                await ads.LoadAsync(AdFormat.Rewarded); var reward = ads.ShowAsync(AdFormat.Rewarded);
                fake.pending = new TaskCompletionSource<bool>(); var form = consent.ShowPrivacyOptionsAsync();
                Assert.IsFalse(ads.IsReady(AdFormat.Rewarded)); Assert.AreEqual(1, ad.disposed);
                Assert.IsFalse(await consent.ShowPrivacyOptionsAsync()); Assert.IsFalse(await consent.RetryAsync());
                ad.pendingShow.SetResult(AdResult.Rewarded); Assert.AreEqual(AdResult.Disabled, await reward);
                fake.allowed = false; fake.pending.SetResult(true); await form;
                Assert.IsFalse(await ads.LoadAsync(AdFormat.Rewarded));
            }
        }
        [Test] public async Task PrivacyFormReloadsAdsAfterCompletionEvenIfPermissionStaysTrue()
        {
            var fake = new Consent { allowed = true, required = true }; int created = 0;
            using (var consent = new ConsentService(new ConsentSettings(), () => fake))
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => { created++; return new Ads(); }, consent))
            {
                await ads.LoadAsync(AdFormat.Banner); Assert.IsTrue(await consent.ShowPrivacyOptionsAsync());
                Assert.IsTrue(await ads.LoadAsync(AdFormat.Banner)); Assert.AreEqual(2, created);
            }
        }
        [Test] public async Task DisposeWhileGatheringCannotReviveAds()
        {
            var fake = new Consent { pending = new TaskCompletionSource<bool>() }; int created = 0;
            var consent = new ConsentService(new ConsentSettings(), () => fake);
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => { created++; return new Ads(); }, consent))
            {
                var pending = ads.InitializeAsync(); consent.Dispose(); fake.allowed = true; fake.pending.SetResult(true);
                Assert.IsFalse(await pending); Assert.AreEqual(0, created); Assert.IsFalse(consent.CanRequestAds); consent.Dispose(); Assert.AreEqual(1, fake.disposed);
            }
        }
        [Test] public async Task ResetRequiresNewGatherAndInvalidatesReadyAds()
        {
            var fake = new Consent { allowed = true, required = true }; var ad = new Ads();
            using (var consent = new ConsentService(new ConsentSettings(), () => fake))
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => ad, consent))
            {
                await ads.InitializeAsync(); consent.ResetForTesting(); Assert.IsFalse(consent.CanRequestAds); Assert.AreEqual(1, ad.disposed);
                fake.allowed = true; await consent.InitializeAsync(); Assert.AreEqual(2, fake.gathers); Assert.AreEqual(1, fake.resets);
            }
        }
        [Test] public async Task MaxUnderAgeConfigurationNeverStartsSdk()
        {
            var fake = new Consent { allowed = true }; int created = 0;
            using (var consent = new ConsentService(new ConsentSettings { tagForUnderAgeOfConsent = true }, () => fake))
            using (var ads = new AdsService(new AdsSettings { enabled = true, network = AdsNetwork.AppLovinMax }, () => { created++; return new Ads(); }, consent))
            { Assert.IsFalse(await ads.InitializeAsync()); Assert.AreEqual(0, created); }
        }
    }
}
