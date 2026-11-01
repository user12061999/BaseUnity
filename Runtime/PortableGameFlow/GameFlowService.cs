using System;
using System.Threading.Tasks;
namespace BaseUnity.Flow
{
    public enum GamePhase { Bootstrap, Loading, Home, Playing, Paused, Won, Lost }
    /// <summary>Main-thread flow. Scene loading is injected; no UI, save, economy or SDK dependency.</summary>
    public sealed class GameFlowService : IDisposable
    {
        public GamePhase Phase { get; private set; } = GamePhase.Bootstrap;
        public int Level { get; private set; } = 1;
        public int LevelCount { get; }
        public bool CanNext => Phase == GamePhase.Won && Level < LevelCount;
        public bool CanPlay => Phase == GamePhase.Playing;
        public event Action Changed;
        public event Action<Exception> ObserverError;
        private readonly Func<bool, Task> load;
        private bool disposed;
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
            var previous=Phase; int oldLevel=Level;
            Phase=GamePhase.Loading; Notify();
            try {
                if(load!=null)await load(home);
                if(disposed)return false;
                Level=level; Phase=home?GamePhase.Home:GamePhase.Playing; Notify(); return true;
            } catch { if(!disposed) { Level=oldLevel; Phase=previous; Notify(); } throw; }
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
        { if(disposed||Phase!=from)return false; Phase=to; Notify();return true; }
        public void Dispose() { disposed=true; Changed=null; ObserverError=null; }
    }
}
