using System;
using System.Threading.Tasks;
namespace BaseUnity.Flow
{
    public enum GamePhase { Bootstrap, Loading, Home, Playing, Paused, Won, Lost }
    public readonly struct GameOutcome
    {
        public readonly string runId;
        public readonly int level;
        public readonly bool won;
        public GameOutcome(string runId,int level,bool won) { this.runId=runId;this.level=level;this.won=won; }
    }
    /// <summary>Main-thread flow. Scene loading is injected; no UI, save, economy or SDK dependency.</summary>
    public sealed class GameFlowService : IDisposable
    {
        public GamePhase Phase { get; private set; } = GamePhase.Bootstrap;
        public int Level { get; private set; } = 1;
        public int LevelCount { get; }
        public string RunId { get; private set; }
        public event Action<GameOutcome> Completed;
        private readonly System.Collections.Generic.List<Func<bool>> guards=new System.Collections.Generic.List<Func<bool>>();
        public bool CanLeaveResult { get { if(notifyingOutcome)return false;foreach(var guard in guards.ToArray())try { if(!guard())return false; }catch { return false; }return true; } }
        public IDisposable GuardResults(Func<bool> guard) { if(guard==null)throw new ArgumentNullException(nameof(guard));guards.Add(guard);return new Guard(()=>guards.Remove(guard)); }
        private sealed class Guard : IDisposable { private Action release;public Guard(Action release) { this.release=release; }public void Dispose() { var action=release;release=null;action?.Invoke(); } }
        public void RefreshNavigation() { if(!disposed)Notify(); }
        public bool CanNext => Phase == GamePhase.Won && Level < LevelCount && CanLeaveResult;
        public bool CanPlay => Phase == GamePhase.Playing;
        public event Action Changed;
        public event Action<Exception> ObserverError;
        private readonly Func<bool, Task> load;
        private bool disposed,notifyingOutcome;
        public GameFlowService(int levelCount = 10, Func<bool, Task> loadScene = null)
        { if(levelCount < 1) throw new ArgumentOutOfRangeException(nameof(levelCount)); LevelCount=levelCount; load=loadScene; }
        private void Notify()
        {
            if(Changed==null)return;
            foreach(Action observer in Changed.GetInvocationList())
                try { observer(); } catch(Exception error) { try { ObserverError?.Invoke(error); } catch { } }
        }
        private async Task<bool> Move(bool home, int level)
        {
            if(disposed || Phase==GamePhase.Loading)return false;
            if((Phase==GamePhase.Won||Phase==GamePhase.Lost)&&!CanLeaveResult)return false;
            var previous=Phase; int oldLevel=Level;
            Phase=GamePhase.Loading; Notify();
            try {
                if(load!=null)await load(home);
                if(disposed)return false;
                Level=level;if(!home)RunId=Guid.NewGuid().ToString("N"); Phase=home?GamePhase.Home:GamePhase.Playing; Notify(); return true;
            } catch { if(!disposed) { Level=oldLevel; Phase=previous; Notify(); } throw; }
        }
        /// <summary>Restore a saved selection before a run. Scene transitions and active runs are never modified.</summary>
        public bool RestoreLevel(int level)
        {
            if(level<1||level>LevelCount)throw new ArgumentOutOfRangeException(nameof(level));
            if(disposed||(Phase!=GamePhase.Bootstrap&&Phase!=GamePhase.Home))return false;Level=level;Notify();return true;
        }
        public Task<bool> HomeAsync() => Move(true,Level);
        public Task<bool> PlayAsync() => Phase==GamePhase.Home?Move(false,Level):Task.FromResult(false);
        public Task<bool> RetryAsync() => Phase==GamePhase.Won||Phase==GamePhase.Lost||Phase==GamePhase.Paused?Move(false,Level):Task.FromResult(false);
        public Task<bool> NextAsync() => CanNext?Move(false,Level+1):Task.FromResult(false);
        public bool Pause() => Set(GamePhase.Playing,GamePhase.Paused);
        public bool Resume() => Set(GamePhase.Paused,GamePhase.Playing);
        public bool Win() => Set(GamePhase.Playing,GamePhase.Won);
        public bool Lose() => Set(GamePhase.Playing,GamePhase.Lost);
        private bool Set(GamePhase from,GamePhase to)
        { if(disposed||Phase!=from)return false; Phase=to;
            if(to==GamePhase.Won||to==GamePhase.Lost) {
                var outcome=new GameOutcome(RunId,Level,to==GamePhase.Won);
                notifyingOutcome=true;try { if(Completed!=null)foreach(Action<GameOutcome> listener in Completed.GetInvocationList())try { listener(outcome); }catch(Exception error) { try { ObserverError?.Invoke(error); }catch { } } }finally { notifyingOutcome=false; }
            }
            Notify();return true; }
        public void Dispose() { disposed=true; Changed=null; Completed=null;guards.Clear();ObserverError=null; }
    }
}
