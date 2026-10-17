#if UNITY_EDITOR
using System.Collections.Generic;
using BaseUnity.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.GameFlow.Tests
{
    /// <summary>Existing scene-flow tests must not spend the developer's wallet or contact live SDKs.</summary>
    [SetUpFixture]
    public sealed class SceneFeatureIsolation
    {
        private readonly Dictionary<GameFlowConfig, FeatureConfig> originals = new Dictionary<GameFlowConfig, FeatureConfig>();
        private readonly List<FeatureConfig> temporary = new List<FeatureConfig>();
        [OneTimeSetUp] public void IsolateSceneFeatures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GameFlowConfig", new[] { "Assets" }))
            {
                var flow = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (!flow) continue;
                originals.Add(flow, flow.features);
                var config = ScriptableObject.CreateInstance<FeatureConfig>();
                temporary.Add(config); flow.features = config;
            }
        }
        [OneTimeTearDown] public void RestoreSceneFeatures()
        {
            foreach (var pair in originals) if (pair.Key) pair.Key.features = pair.Value;
            foreach (var config in temporary) if (config) Object.DestroyImmediate(config);
            originals.Clear(); temporary.Clear();
        }
    }
}
#endif
