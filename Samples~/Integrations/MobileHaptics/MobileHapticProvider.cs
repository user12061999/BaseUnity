using System;
using System.Runtime.InteropServices;
using BaseUnity.Features;
using UnityEngine;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/Mobile Haptics")]
    public sealed class MobileHapticProvider : HapticProvider
    {
        public override IHapticBackend Create() => new Backend();
        private sealed class Backend : IHapticBackend
        {
#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")] private static extern void BaseUnityPlayHaptic(int kind);
#endif
            public bool Play(HapticKind kind)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var build = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return false;
                    long duration = kind == HapticKind.Light ? 25 : kind == HapticKind.Medium ? 45 : kind == HapticKind.Success ? 65 : 100;
                    if (build.GetStatic<int>("SDK_INT") >= 26)
                    {
                        using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var pulse = effect.CallStatic<AndroidJavaObject>("createOneShot", duration, kind == HapticKind.Light ? 80 : 180)) vibrator.Call("vibrate", pulse);
                    }
                    else vibrator.Call("vibrate", duration);
                    return true;
                }
#elif UNITY_IOS && !UNITY_EDITOR
                BaseUnityPlayHaptic((int)kind); return true;
#else
                return false;
#endif
            }
            public void Dispose() { }
        }
    }
}
