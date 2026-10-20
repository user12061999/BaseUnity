using UnityEngine;
namespace BaseUnity.GameFlow
{
    public sealed class GameFlowThemeController : MonoBehaviour
    {
        public UIThemeConfig Theme { get; private set; }
        public void SetTheme(UIThemeConfig value)
        {
            Theme = value;
            foreach (var binding in GetComponentsInChildren<UIThemeBinding>(true)) binding.Apply(value);
            foreach (var menu in GetComponentsInChildren<OfflineMenuView>(true))
            {
                var success = value ? value.Find(UIThemeRole.Success) : null; var error = value ? value.Find(UIThemeRole.Error) : null;
                if (success != null) menu.successColor = success.text; if (error != null) menu.errorColor = error.text;
            }
        }
    }
}
