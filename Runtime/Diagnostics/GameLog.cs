using UnityEngine;

namespace BaseUnity.Diagnostics
{
    public enum LogLevel { Info, Warning, Error, Off }

    public static class GameLog
    {
        public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => MinimumLevel = LogLevel.Info;
        public static void Info(string category, object message)
        { if (MinimumLevel <= LogLevel.Info) Debug.Log($"[{category}] {message}"); }
        public static void Warning(string category, object message)
        { if (MinimumLevel <= LogLevel.Warning) Debug.LogWarning($"[{category}] {message}"); }
        public static void Error(string category, object message)
        { if (MinimumLevel <= LogLevel.Error) Debug.LogError($"[{category}] {message}"); }
    }
}
