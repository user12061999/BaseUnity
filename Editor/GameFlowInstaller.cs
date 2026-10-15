using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BaseUnity.Editor
{
    public static class GameFlowInstaller
    {
        public const string Destination = "Assets/BaseUnityGameFlow";
        private const string PendingKey = "BaseUnity.InstallGameFlow";

        [MenuItem("Tools/BaseUnity/Install Game Flow Sample")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before installing the sample.");
            if (Type.GetType("UnityEngine.UI.Button, UnityEngine.UI") == null)
                throw new InvalidOperationException("Install Unity UI (com.unity.ugui) in Package Manager before installing this sample.");
            if (Directory.Exists(Destination))
                throw new InvalidOperationException("Assets/BaseUnityGameFlow already exists. Existing game code is never overwritten. Use Tools/BaseUnity/Game Flow/Generate Missing Scenes instead.");
            string source = null;
            foreach (var guid in AssetDatabase.FindAssets("GameFlowInstaller t:MonoScript"))
            {
                var script = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(script), "../Samples~/GameFlow"));
                if (Directory.Exists(candidate)) { source = candidate; break; }
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(script);
                if (package != null)
                {
                    candidate = Path.Combine(package.resolvedPath, "Samples~/GameFlow");
                    if (Directory.Exists(candidate)) { source = candidate; break; }
                }
            }
            // The development sandbox links Editor separately from the repository root.
            if (source == null && Directory.Exists("Samples~/GameFlow")) source = Path.GetFullPath("Samples~/GameFlow");
            if (source == null) throw new DirectoryNotFoundException("Cannot locate Samples~/GameFlow next to the BaseUnity library.");
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                // New GUIDs: allow a game to own and edit its installed copy independently.
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Path.Combine(Destination, file.Substring(source.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, false);
            }
            SessionState.SetBool(PendingKey, true);
            AssetDatabase.Refresh();
            Debug.Log("BaseUnity sample copied. Scene generation will run after compilation.");
        }

        [DidReloadScripts]
        private static void AfterCompile()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                var type = Type.GetType("BaseUnity.GameFlow.Editor.GameFlowSceneBuilder, BaseUnity.GameFlow.Editor");
                if (type == null) return;
                SessionState.EraseBool(PendingKey);
                try { type.GetMethod("Generate").Invoke(null, null); }
                catch (Exception error) { Debug.LogException(error); }
            };
        }
    }
}
