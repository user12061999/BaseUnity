using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;
namespace BaseUnity.IntegrationModules
{
    [CreateAssetMenu(menuName="BaseUnity/Simulation/Ads")]
    public sealed class SimulatedAdsProvider : AdsProvider
    {
        public AdResult rewardedResult=AdResult.Closed;
        public override IAdsBackend Create() { if(!Application.isEditor&&!Debug.isDebugBuild)throw new InvalidOperationException("Simulation is development-only");return new Backend(rewardedResult); }
        private sealed class Backend : IAdsBackend
        {
            private readonly AdResult reward;private readonly HashSet<AdFormat> loaded=new HashSet<AdFormat>();
            public Backend(AdResult reward) { this.reward=reward; }
            public Task InitializeAsync(AdsSettings settings,CancellationToken token) { token.ThrowIfCancellationRequested();return Task.CompletedTask; }
            public Task<bool> LoadAsync(AdFormat format,CancellationToken token) { token.ThrowIfCancellationRequested();loaded.Add(format);return Task.FromResult(true); }
            public bool IsReady(AdFormat format) => loaded.Contains(format);
            public Task<AdResult> ShowAsync(AdFormat format,CancellationToken token) { token.ThrowIfCancellationRequested();if(!loaded.Remove(format))return Task.FromResult(AdResult.Unavailable);return Task.FromResult(format==AdFormat.Rewarded?reward:AdResult.Closed); }
            public void HideBanner() { loaded.Remove(AdFormat.Banner); }
            public void Dispose() { loaded.Clear(); }
        }
    }
}
