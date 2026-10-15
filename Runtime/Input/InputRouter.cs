using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaseUnity.Input
{
    public interface IInputSource
    {
        float ReadAxis(string action);
        Vector2 ReadVector2(string action);
        bool IsPressed(string action);
        bool WasPressedThisFrame(string action);
    }

    /// <summary>Independent of a specific input package. Install an adapter or use ManualInputSource for touch.</summary>
    public sealed class InputRouter : IInputSource
    {
        private readonly IInputSource source;
        public bool Enabled { get; set; } = true;
        public InputRouter(IInputSource source) { this.source = source ?? throw new ArgumentNullException(nameof(source)); }
        public float ReadAxis(string action) => Enabled ? source.ReadAxis(action) : 0;
        public Vector2 ReadVector2(string action) => Enabled ? source.ReadVector2(action) : Vector2.zero;
        public bool IsPressed(string action) => Enabled && source.IsPressed(action);
        public bool WasPressedThisFrame(string action) => Enabled && source.WasPressedThisFrame(action);
    }

    public sealed class ManualInputSource : IInputSource
    {
        private readonly Dictionary<string, float> axes = new Dictionary<string, float>();
        private readonly Dictionary<string, Vector2> vectors = new Dictionary<string, Vector2>();
        private readonly HashSet<string> held = new HashSet<string>();
        private readonly Dictionary<string, int> pressedFrames = new Dictionary<string, int>();
        public void SetAxis(string action, float value) => axes[action] = value;
        public void SetVector2(string action, Vector2 value) => vectors[action] = value;
        public void SetButton(string action, bool pressed)
        {
            if (pressed) { if (held.Add(action)) pressedFrames[action] = UnityEngine.Time.frameCount; }
            else held.Remove(action);
        }
        public float ReadAxis(string action) => axes.TryGetValue(action, out var value) ? value : 0;
        public Vector2 ReadVector2(string action) => vectors.TryGetValue(action, out var value) ? value : Vector2.zero;
        public bool IsPressed(string action) => held.Contains(action);
        public bool WasPressedThisFrame(string action) => pressedFrames.TryGetValue(action, out var frame) && frame == UnityEngine.Time.frameCount;
        public void Reset() { axes.Clear(); vectors.Clear(); held.Clear(); pressedFrames.Clear(); }
    }
}
