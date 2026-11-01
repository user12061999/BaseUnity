using System;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;
namespace BaseUnity.IntegrationModules
{
    [CreateAssetMenu(menuName="BaseUnity/Simulation/Consent")]
    public sealed class SimulatedConsentProvider : ConsentProvider
    {
        public bool allowRequests=true;
        public override IConsentBackend Create() { if(!Application.isEditor&&!Debug.isDebugBuild)throw new InvalidOperationException("Simulation is development-only");return new Backend(allowRequests); }
        private sealed class Backend : IConsentBackend
        {
            private readonly bool allow;private bool disposed;
            public Backend(bool allow) { this.allow=allow; }
            public bool CanRequestAds => !disposed&&allow;
            public bool PrivacyOptionsRequired => false;
            public Task GatherAsync(ConsentSettings settings,CancellationToken token) { token.ThrowIfCancellationRequested();return Task.CompletedTask; }
            public Task ShowPrivacyOptionsAsync(CancellationToken token) { token.ThrowIfCancellationRequested();return Task.CompletedTask; }
            public void ResetForTesting() { }
            public void Dispose() { disposed=true; }
        }
    }
}
