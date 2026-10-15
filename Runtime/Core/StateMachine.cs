using System;

namespace BaseUnity.Core
{
    public interface IGameState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }

    public sealed class StateMachine : IDisposable
    {
        public IGameState Current { get; private set; }
        private bool transitioning;
        private bool disposed;

        public void Change(IGameState next)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StateMachine));
            if (transitioning) throw new InvalidOperationException("Cannot change state inside Enter/Exit.");
            if (ReferenceEquals(Current, next)) return;
            transitioning = true;
            try
            {
                var previous = Current;
                Current = null;
                previous?.Exit();
                try { next?.Enter(); Current = next; }
                catch { next?.Exit(); throw; }
            }
            finally { transitioning = false; }
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
        public void Dispose()
        {
            if (disposed) return;
            try { Change(null); }
            finally { disposed = true; }
        }
    }
}
