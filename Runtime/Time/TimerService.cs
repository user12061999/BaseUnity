using System;
using System.Collections.Generic;

namespace BaseUnity.Time
{
    /// <summary>Tick manually or through GameBootstrap. Repeating timers fire at most once per tick.</summary>
    public sealed class TimerService : IDisposable
    {
        public sealed class TimerHandle : IDisposable
        {
            internal double Remaining;
            internal double Interval;
            internal bool Unscaled;
            internal Action Callback;
            public bool IsCancelled { get; private set; }
            public void Dispose() { IsCancelled = true; Callback = null; }
        }

        private readonly List<TimerHandle> timers = new List<TimerHandle>();
        private bool disposed;

        public TimerHandle Schedule(double seconds, Action callback, bool unscaled = false, bool repeat = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(TimerService));
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || (repeat && seconds == 0))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var timer = new TimerHandle { Remaining = seconds, Interval = repeat ? seconds : 0, Unscaled = unscaled, Callback = callback };
            timers.Add(timer);
            return timer;
        }

        public void Tick(float scaledDelta, float unscaledDelta)
        {
            if (disposed) return;
            if (float.IsNaN(scaledDelta) || float.IsInfinity(scaledDelta) || scaledDelta < 0 ||
                float.IsNaN(unscaledDelta) || float.IsInfinity(unscaledDelta) || unscaledDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDelta));
            var errors = new List<Exception>();
            foreach (var timer in timers.ToArray())
            {
                if (timer.IsCancelled) continue;
                timer.Remaining -= timer.Unscaled ? unscaledDelta : scaledDelta;
                if (timer.Remaining > 0) continue;
                var callback = timer.Callback;
                if (timer.Interval > 0) timer.Remaining = timer.Interval;
                else timer.Dispose();
                try { callback(); }
                catch (Exception error) { errors.Add(error); }
            }
            timers.RemoveAll(timer => timer.IsCancelled);
            if (errors.Count > 0) throw new AggregateException(errors);
        }

        public void Dispose()
        {
            disposed = true;
            foreach (var timer in timers) timer.Dispose();
            timers.Clear();
        }
    }
}
