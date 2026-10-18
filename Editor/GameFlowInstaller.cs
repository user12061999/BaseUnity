using System;
using System.IO;
using UnityEditor;

namespace BaseUnity.Editor
{
    public static class GameFlowInstaller
    {
        public const string Destination = "Assets/BaseUnityGameFlow";
        [MenuItem("Tools/BaseUnity/Install Game Flow Sample")]
        public static void Install() => BaseUnitySetupWindow.Open();
        public static string FindSampleSource()
        {
            foreach (var guid in AssetDatabase.FindAssets("GameFlowInstaller t:MonoScript"))
            {
                var script = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(script), "../Samples~/GameFlow"));
                if (Directory.Exists(candidate)) return candidate;
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(script);
                if (package != null)
                { candidate = Path.Combine(package.resolvedPath, "Samples~/GameFlow"); if (Directory.Exists(candidate)) return candidate; }
            }
            if (Directory.Exists("Samples~/GameFlow")) return Path.GetFullPath("Samples~/GameFlow");
            throw new DirectoryNotFoundException("Cannot locate the BaseUnity Game Flow sample source.");
        }
        [Serializable] private sealed class PackageVersion { public string version; }
        public static string SourceVersion => UnityEngine.JsonUtility.FromJson<PackageVersion>(File.ReadAllText(Path.Combine(FindSampleSource(), "../../package.json"))).version;
    }
}
