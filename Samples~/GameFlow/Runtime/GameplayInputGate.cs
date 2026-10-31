using BaseUnity.Core;
using BaseUnity.Features;
using BaseUnity.Input;
namespace BaseUnity.GameFlow
{
    public static class GameplayInputGate
    {
        public static FeatureServices Services => GameBootstrap.Instance && GameBootstrap.Instance.IsReady ? GameBootstrap.Instance.Services.Get<FeatureServices>() : null;
        public static bool Open => Services != null && GameBootstrap.Instance.Services.Get<InputRouter>().Enabled;
        public static ManualInputSource Manual => GameBootstrap.Instance && GameBootstrap.Instance.IsReady ? GameBootstrap.Instance.Services.Get<ManualInputSource>() : null;
    }
}
