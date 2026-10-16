using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using NUnit.Framework;

namespace BaseUnity.Tests
{
    public sealed class FeatureTests
    {
        private sealed class Ads : IAdsBackend
        {
            public int initialized, loaded, shown, disposed;
            public TaskCompletionSource<bool> pendingInit;
            public TaskCompletionSource<AdResult> pendingShow;
            public AdResult result = AdResult.Closed;
            public Task InitializeAsync(AdsSettings settings, CancellationToken token)
            { initialized++; return pendingInit == null ? Task.CompletedTask : pendingInit.Task; }
            public Task<bool> LoadAsync(AdFormat format, CancellationToken token) { loaded++; return Task.FromResult(true); }
            public bool IsReady(AdFormat format) => true;
            public Task<AdResult> ShowAsync(AdFormat format, CancellationToken token)
            { shown++; return pendingShow == null ? Task.FromResult(result) : pendingShow.Task; }
            public void HideBanner() { }
            public void Dispose() { disposed++; }
        }
        private sealed class Firebase : IFirebaseBackend
        {
            public int initialized, events, exceptions, fetches, disposed;
            public bool analytics, crashlytics;
            public TaskCompletionSource<IDictionary<string, string>> pendingFetch;
            public Task InitializeAsync(FirebaseSettings settings, CancellationToken token) { initialized++; return Task.CompletedTask; }
            public void SetCollection(bool a, bool c) { analytics = a; crashlytics = c; }
            public void LogEvent(string name, IDictionary<string, string> parameters) { events++; }
            public void RecordException(Exception exception) { exceptions++; }
            public Task<IDictionary<string, string>> FetchAsync(CancellationToken token)
            { fetches++; return pendingFetch == null ? Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string> { ["coins"] = "25", ["ads_enabled"] = "false" }) : pendingFetch.Task; }
            public void Dispose() { disposed++; analytics = crashlytics = false; }
        }
        [Test] public async Task DisabledAndMissingProvidersRemainSafe()
        {
            int created = 0;
            using (var ads = new AdsService(new AdsSettings(), () => { created++; return new Ads(); }))
            {
                Assert.IsFalse(await ads.InitializeAsync()); Assert.IsFalse(await ads.LoadAsync(AdFormat.Rewarded));
                Assert.AreEqual(AdResult.Disabled, await ads.ShowAsync(AdFormat.Rewarded)); Assert.AreEqual(0, created);
            }
            using (var ads = new AdsService(new AdsSettings { enabled = true }))
            { Assert.AreEqual(FeatureStatus.MissingProvider, ads.Status); Assert.AreEqual(AdResult.Unavailable, await ads.ShowAsync(AdFormat.Rewarded)); }
            using (var firebase = new FirebaseService(new FirebaseSettings(), () => { created++; return new Firebase(); }))
            { Assert.IsFalse(await firebase.InitializeAsync()); Assert.IsFalse(await firebase.FetchRemoteConfigAsync()); Assert.IsFalse(firebase.LogEvent("test")); Assert.AreEqual(0, created); }
        }
        [Test] public async Task AdsFormatsAndRewardResultsAreGated()
        {
            var fake = new Ads();
            using (var ads = new AdsService(new AdsSettings { enabled = true, banner = false }, () => fake))
            {
                Assert.IsFalse(await ads.LoadAsync(AdFormat.Banner)); Assert.AreEqual(0, fake.initialized);
                Assert.IsTrue(await ads.LoadAsync(AdFormat.Rewarded)); Assert.AreEqual(1, fake.initialized);
                Assert.AreEqual(AdResult.Closed, await ads.ShowAsync(AdFormat.Rewarded));
                fake.result = AdResult.Rewarded;
                Assert.AreEqual(AdResult.Rewarded, await ads.ShowAsync(AdFormat.Rewarded));
                Assert.AreEqual(AdResult.Closed, await ads.ShowAsync(AdFormat.Interstitial));
                Assert.AreEqual(AdResult.Cooldown, await ads.ShowAsync(AdFormat.Interstitial));
                Assert.AreEqual(AdResult.Disabled, await ads.ShowAsync(AdFormat.Banner));
            }
            Assert.AreEqual(1, fake.disposed);
        }
        [Test] public async Task DisableDuringRewardSuppressesLateRewardAndDoubleShow()
        {
            var fake = new Ads { pendingShow = new TaskCompletionSource<AdResult>() };
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => fake))
            {
                await ads.InitializeAsync(); var pending = ads.ShowAsync(AdFormat.Rewarded);
                Assert.AreEqual(AdResult.Busy, await ads.ShowAsync(AdFormat.Rewarded));
                ads.SetEnabled(false); fake.pendingShow.SetResult(AdResult.Rewarded);
                Assert.AreEqual(AdResult.Disabled, await pending); Assert.AreEqual(FeatureStatus.Disabled, ads.Status);
                Assert.AreEqual(1, fake.shown); Assert.AreEqual(1, fake.disposed);
            }
        }
        [Test] public async Task CancelledInitializationCannotReviveOldBackend()
        {
            var old = new Ads { pendingInit = new TaskCompletionSource<bool>() }; var fresh = new Ads(); int count = 0;
            using (var ads = new AdsService(new AdsSettings { enabled = true }, () => count++ == 0 ? old : fresh))
            {
                var pending = ads.InitializeAsync(); ads.SetEnabled(false); ads.SetEnabled(true);
                Assert.IsTrue(await ads.InitializeAsync()); old.pendingInit.SetResult(true);
                Assert.IsFalse(await pending); Assert.AreEqual(FeatureStatus.Ready, ads.Status);
                Assert.AreEqual(1, old.disposed); Assert.AreEqual(0, fresh.disposed);
            }
        }
        [Test] public async Task FirebaseFlagsAndRemoteDefaultsAreIndependent()
        {
            var fake = new Firebase();
            var settings = new FirebaseSettings { enabled = true, analytics = false, crashlytics = true,
                defaults = new[] { new RemoteDefault { key = "coins", value = "10" }, new RemoteDefault { key = "ads_enabled", value = "true" } } };
            using (var firebase = new FirebaseService(settings, () => fake))
            {
                Assert.AreEqual(10, firebase.GetLong("coins")); Assert.IsTrue(await firebase.InitializeAsync());
                Assert.IsFalse(fake.analytics); Assert.IsTrue(fake.crashlytics);
                Assert.IsFalse(firebase.LogEvent("level_start")); Assert.IsTrue(firebase.RecordException(new Exception("test")));
                Assert.IsTrue(await firebase.FetchRemoteConfigAsync()); Assert.AreEqual(25, firebase.GetLong("coins"));
                Assert.IsFalse(firebase.GetBool("ads_enabled"));
                firebase.SetEnabled(false); Assert.AreEqual(10, firebase.GetLong("coins"));
                Assert.IsFalse(firebase.RecordException(new Exception())); Assert.IsFalse(await firebase.FetchRemoteConfigAsync());
                Assert.AreEqual(0, fake.events); Assert.AreEqual(1, fake.exceptions); Assert.AreEqual(1, fake.fetches);
            }
        }
        [Test] public async Task RemoteConfigDisabledNeverFetchesAndLateFetchCannotOverwriteDefaults()
        {
            var fake = new Firebase { pendingFetch = new TaskCompletionSource<IDictionary<string, string>>() };
            var settings = new FirebaseSettings { enabled = true, remoteConfig = false };
            using (var firebase = new FirebaseService(settings, () => fake))
            { Assert.IsFalse(await firebase.FetchRemoteConfigAsync()); Assert.AreEqual(0, fake.initialized); }
            settings.remoteConfig = true;
            using (var firebase = new FirebaseService(settings, () => fake))
            {
                var fetch = firebase.FetchRemoteConfigAsync(); firebase.SetEnabled(false);
                fake.pendingFetch.SetResult(new Dictionary<string, string> { ["key"] = "late" });
                Assert.IsFalse(await fetch); Assert.AreEqual("default", firebase.GetString("key", "default"));
            }
        }
    }
}
