using System;
using System.Collections.Generic;
namespace BaseUnity.Features
{
    /// <summary>Derived in-game badges; never sends operating-system notifications.</summary>
    public sealed class InGameNoticeService
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        public event Action Changed;
        public int Count(string id) => id != null && counts.TryGetValue(id, out var count) ? count : 0;
        public void Set(string id, int count)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 80) throw new ArgumentException("A short badge ID is required", nameof(id));
            count = Math.Max(0, count); if (Count(id) == count) return;
            if (count == 0) counts.Remove(id);
            else { if (!counts.ContainsKey(id) && counts.Count >= 128) throw new InvalidOperationException("Badge capacity reached"); counts[id] = count; }
            Changed?.Invoke();
        }
    }
}
