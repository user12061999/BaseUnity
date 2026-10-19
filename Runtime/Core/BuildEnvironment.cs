using System;
using System.IO;
using UnityEngine;

namespace BaseUnity.Core
{
    /// <summary>Development data never shares the production save directory.</summary>
    public static class BuildEnvironment
    {
        public static string Name
        {
            get
            {
#if UNITY_EDITOR
                return "Editor";
#elif DEVELOPMENT_BUILD
                return "Development";
#else
                return "Release";
#endif
            }
        }
        public static bool IsDevelopment
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
        public static string SaveDirectory => ResolveSaveDirectory(Application.persistentDataPath, Name);
        public static string ResolveSaveDirectory(string persistentPath, string environment)
        {
            if (environment != "Editor" && environment != "Development" && environment != "Release")
                throw new ArgumentException("Unknown build environment.", nameof(environment));
            return Path.Combine(persistentPath, environment == "Release" ? "BaseUnity" : "BaseUnity_" + environment);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Call only after service disposal. Preserves every test save in a sibling backup.</summary>
        public static string ArchiveTestSaves(string persistentPath, string environment)
        {
            if (environment != "Editor" && environment != "Development")
                throw new InvalidOperationException("Production saves cannot be reset by development tools.");
            string source = Path.GetFullPath(ResolveSaveDirectory(persistentPath, environment));
            if (!Directory.Exists(source)) return null;
            string backup = source + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
            Directory.Move(source, backup);
            return backup;
        }
#endif
    }
}
