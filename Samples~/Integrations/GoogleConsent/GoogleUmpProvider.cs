using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/Google UMP Consent")]
    public sealed class GoogleUmpProvider : ConsentProvider
    {
        public override IConsentBackend Create() => new Backend();
        private sealed class Backend : IConsentBackend
        {
            private static bool nativeBusy;
            private readonly SynchronizationContext mainThread = SynchronizationContext.Current;
            private bool disposed, updated;
            public bool CanRequestAds => !disposed && updated && ConsentInformation.CanRequestAds();
            public bool PrivacyOptionsRequired => !disposed && updated && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
            private void OnMain(Action action)
            {
                if (mainThread == null) throw new InvalidOperationException("Create the consent backend on Unity's main thread.");
                mainThread.Post(_ => action(), null);
            }
            private static void PlatformCheck()
            {
                if (Application.isEditor || (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer))
                    throw new PlatformNotSupportedException("Google UMP consent requires an Android/iOS player. Editor never grants simulated consent.");
            }
            public Task GatherAsync(ConsentSettings settings, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); PlatformCheck();
                var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = settings.tagForUnderAgeOfConsent };
                if (settings.debugMode && (Application.isEditor || Debug.isDebugBuild))
                    request.ConsentDebugSettings = new ConsentDebugSettings {
                        DebugGeography = settings.debugRegion == ConsentDebugRegion.Eea ? DebugGeography.EEA : settings.debugRegion == ConsentDebugRegion.NotEea ? DebugGeography.NotEEA : DebugGeography.Disabled,
                        TestDeviceHashedIds = new List<string>(settings.testDeviceHashedIds ?? Array.Empty<string>())
                    };
                return Native(() => {
                    var result = new TaskCompletionSource<bool>();
                    ConsentInformation.Update(request, error => OnMain(() => {
                        updated = true;
                        if (error != null) { result.TrySetException(new InvalidOperationException(error.Message)); return; }
                        if (disposed || token.IsCancellationRequested) { result.TrySetCanceled(); return; }
                        try { ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() => Finish(result, formError))); }
                        catch (Exception failure) { result.TrySetException(failure); }
                    }));
                    return result.Task;
                });
            }
            public Task ShowPrivacyOptionsAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); PlatformCheck();
                return Native(() => {
                    var result = new TaskCompletionSource<bool>();
                    ConsentForm.ShowPrivacyOptionsForm(error => OnMain(() => Finish(result, error)));
                    return result.Task;
                });
            }
            private static void Finish(TaskCompletionSource<bool> result, FormError error)
            { if (error == null) result.TrySetResult(true); else result.TrySetException(new InvalidOperationException(error.Message)); }
            private static async Task Native(Func<Task> operation)
            {
                if (nativeBusy) throw new InvalidOperationException("A native consent operation is still running. Wait before retrying.");
                nativeBusy = true;
                try { await operation(); } finally { nativeBusy = false; }
            }
            public void ResetForTesting()
            {
                if (!Application.isEditor && !Debug.isDebugBuild) throw new InvalidOperationException("Development builds only.");
                if (nativeBusy) throw new InvalidOperationException("Native consent operation is still running.");
                ConsentInformation.Reset(); updated = false;
            }
            public void Dispose() { disposed = true; }
        }
    }
}
