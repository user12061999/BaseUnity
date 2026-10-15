using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BaseUnity.Input
{
    /// <summary>Owns a cloned action asset; caller must dispose. Use Map/Action paths to avoid ambiguous names.</summary>
    public sealed class InputSystemSource : IInputSource, IDisposable
    {
        private readonly InputActionAsset actions;
        public InputSystemSource(InputActionAsset template, string initialMap)
        {
            if (!template) throw new ArgumentNullException(nameof(template));
            actions = UnityEngine.Object.Instantiate(template);
            try { SwitchMap(initialMap); }
            catch { UnityEngine.Object.Destroy(actions); throw; }
        }
        public void SwitchMap(string name)
        {
            var map = actions.FindActionMap(name, true);
            actions.Disable();
            map.Enable();
        }
        public float ReadAxis(string action) => actions.FindAction(action, true).ReadValue<float>();
        public Vector2 ReadVector2(string action) => actions.FindAction(action, true).ReadValue<Vector2>();
        public bool IsPressed(string action) => actions.FindAction(action, true).IsPressed();
        public bool WasPressedThisFrame(string action) => actions.FindAction(action, true).WasPressedThisFrame();
        public void Dispose()
        {
            if (!actions) return;
            actions.Disable();
            UnityEngine.Object.Destroy(actions);
        }
    }
}
