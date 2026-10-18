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
            string root = null;
            foreach (var guid in AssetDatabase.FindAssets("GameFlowSceneBuilder t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/", StringComparison.Ordinal)) { root = Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace('\\', '/'); break; }
            }
            if (root == null) throw new InvalidOperationException("Import the sample into Assets first.");
            bool existed = File.Exists(root + "/GameFlowConfig.asset");
            GenerateAt(root);
            if (!existed) LevelAssetBuilder.Generate(AssetDatabase.LoadAssetAtPath<GameFlowConfig>(root + "/GameFlowConfig.asset"));
        }
        public static void GenerateAt(string root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            root = root.Replace('\\', '/').TrimEnd('/');
            string absolute = Path.GetFullPath(root);
            if (!root.StartsWith("Assets/", StringComparison.Ordinal) || !absolute.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a folder inside Assets.");
            for (string current = absolute; current != null; current = Path.GetDirectoryName(current))
                if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked destination folders are not supported.");
            string scenes = root + "/Scenes";
            var paths = new[] { scenes + "/Bootstrap.unity", scenes + "/Home.unity", scenes + "/InGame.unity" };
            if (paths.Any(path => !File.Exists(path)))
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) throw new InvalidOperationException("Save or close untitled scenes before generating new scenes.");
            Directory.CreateDirectory(scenes); AssetDatabase.Refresh();
            string configPath = root + "/GameFlowConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(configPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<GameFlowConfig>();
                config.homeScene = paths[1]; config.inGameScene = paths[2];
                AssetDatabase.CreateAsset(config, configPath);
                string featuresPath = root + "/FeatureConfig.asset";
                var features = AssetDatabase.LoadAssetAtPath<BaseUnity.Features.FeatureConfig>(featuresPath);
                if (!features) { features = ScriptableObject.CreateInstance<BaseUnity.Features.FeatureConfig>(); AssetDatabase.CreateAsset(features, featuresPath); }
                config.features = features; EditorUtility.SetDirty(config);
            }
            var previous = SceneManager.GetActiveScene();
            try
            {
                foreach (var path in paths)
                {
                    if (File.Exists(path)) continue;
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                    try
                    {
                        SceneManager.SetActiveScene(scene);
                        if (path == paths[0]) new GameObject("Game Flow Bootstrap").AddComponent<GameFlowBootstrap>().Config = config;
                        else new GameObject(path == paths[1] ? "Home Content - customize here" : "Gameplay Content - customize here");
                        if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Failed to save " + path);
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
            var list = paths.Select(path => new EditorBuildSettingsScene(path, true)).Concat(EditorBuildSettings.scenes.Where(scene => !paths.Contains(scene.path))).ToArray();
            if (!list.Select(x => x.path + ":" + x.enabled).SequenceEqual(EditorBuildSettings.scenes.Select(x => x.path + ":" + x.enabled))) EditorBuildSettings.scenes = list;
            AssetDatabase.SaveAssetIfDirty(config);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(paths[0]); EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("BaseUnity Game Flow ready: " + paths[0] + ". If the active Build Profile overrides scenes, add these three scenes there too.");
        }
    }
}
