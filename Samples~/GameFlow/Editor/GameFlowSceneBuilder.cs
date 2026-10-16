using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BaseUnity.GameFlow.Editor
{
    public static class GameFlowSceneBuilder
    {
        [MenuItem("Tools/BaseUnity/Game Flow/Generate Missing Scenes")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            string root = null;
            foreach (var guid in AssetDatabase.FindAssets("GameFlowSceneBuilder t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/", StringComparison.Ordinal)) { root = Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace('\\', '/'); break; }
            }
            if (root == null) throw new InvalidOperationException("Import the sample into Assets first.");
            string scenes = root + "/Scenes";
            Directory.CreateDirectory(scenes);
            AssetDatabase.Refresh();
            string configPath = root + "/GameFlowConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(configPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<GameFlowConfig>();
                config.homeScene = scenes + "/Home.unity";
                config.inGameScene = scenes + "/InGame.unity";
                AssetDatabase.CreateAsset(config, configPath);
                LevelAssetBuilder.Generate(config);
            }
            var previous = SceneManager.GetActiveScene();
            var paths = new[] { scenes + "/Bootstrap.unity", scenes + "/Home.unity", scenes + "/InGame.unity" };
            foreach (var path in paths)
            {
                if (File.Exists(path)) continue; // Never overwrite customized scenes.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    if (path == paths[0])
                    {
                        var bootstrap = new GameObject("Game Flow Bootstrap").AddComponent<GameFlowBootstrap>();
                        bootstrap.Config = config;
                    }
                    else new GameObject(path == paths[1] ? "Home Content - customize here" : "Gameplay Content - customize here");
                    if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Failed to save " + path);
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            // Bootstrap must be first; retain every unrelated project scene and its enabled state.
            EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path, true))
                .Concat(EditorBuildSettings.scenes.Where(scene => !paths.Contains(scene.path))).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(paths[0]);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("BaseUnity Game Flow is ready. Open " + paths[0] + " and press Play. If an active Build Profile overrides the scene list, add these three scenes there too.");
        }
    }
}
