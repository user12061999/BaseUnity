using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;
namespace BaseUnity.IntegrationModules
{
    [CreateAssetMenu(menuName="BaseUnity/Simulation/Firebase")]
    public sealed class SimulatedFirebaseProvider : FirebaseProvider
    {
        public RemoteDefault[] remoteValues=new RemoteDefault[0];
        [NonSerialized] public readonly List<string> events=new List<string>();
        public override IFirebaseBackend Create() { if(!Application.isEditor&&!Debug.isDebugBuild)throw new InvalidOperationException("Simulation is development-only");return new Backend(this); }
        private sealed class Backend : IFirebaseBackend
        {
            private readonly SimulatedFirebaseProvider owner;private bool analytics,crashes;
            public Backend(SimulatedFirebaseProvider owner) { this.owner=owner; }
            public Task InitializeAsync(FirebaseSettings settings,CancellationToken token) { token.ThrowIfCancellationRequested();return Task.CompletedTask; }
            public void SetCollection(bool analytics,bool crashlytics) { this.analytics=analytics;crashes=crashlytics; }
            public void LogEvent(string name,IDictionary<string,string> parameters) { if(analytics)owner.events.Add(name); }
            public void RecordException(Exception error) { if(crashes)owner.events.Add("exception:"+error.GetType().Name); }
            public Task<IDictionary<string,string>> FetchAsync(CancellationToken token) { token.ThrowIfCancellationRequested();return Task.FromResult<IDictionary<string,string>>(owner.remoteValues.Where(x=>x!=null&&!string.IsNullOrEmpty(x.key)).GroupBy(x=>x.key).ToDictionary(g=>g.Key,g=>g.Last().value)); }
            public void Dispose() { analytics=false;crashes=false; }
        }
    }
}
