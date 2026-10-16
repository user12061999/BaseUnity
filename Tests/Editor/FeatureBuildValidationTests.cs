using System.Linq;
using BaseUnity.Editor;
using BaseUnity.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Tests
{
    public sealed class FeatureBuildValidationTests
    {
        private FeatureConfig config;
        [SetUp] public void Setup() { config = ScriptableObject.CreateInstance<FeatureConfig>(); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(config); }

        [Test] public void DisabledModulesIgnoreStaleInvalidSettings()
        {
            config.ads.androidAppId = "invalid";
            config.ads.requestTimeoutSeconds = -1;
            config.firebase.requestTimeoutSeconds = -1;
            Assert.IsEmpty(FeatureBuildValidator.Validate(config, BuildTarget.Android));
        }

        [Test] public void OnlyEnabledFormatsAndSelectedPlatformRequireUnits()
        {
            config.ads.enabled = true; config.ads.testMode = false;
            config.ads.banner = false; config.ads.interstitial = false;
            config.ads.androidRewarded = "ca-app-pub-123/456";
            Assert.IsFalse(FeatureBuildValidator.Validate(config, BuildTarget.Android).Any(i => i.message.Contains("ad-unit ID")));
            Assert.IsTrue(FeatureBuildValidator.Validate(config, BuildTarget.iOS).Any(i => i.message.StartsWith("Rewarded: missing")));
            Assert.AreEqual("ca-app-pub-123/456", config.ads.androidRewarded);
        }

        [Test] public void AdMobTestModeUsesDemoUnitsButMaxNeedsPlacements()
        {
            config.ads.enabled = true;
            Assert.IsFalse(FeatureBuildValidator.Validate(config, BuildTarget.Android).Any(i => i.message.Contains("missing ad-unit ID")));
            config.ads.network = AdsNetwork.AppLovinMax;
            Assert.AreEqual(3, FeatureBuildValidator.Validate(config, BuildTarget.Android).Count(i => i.message.Contains("missing ad-unit ID")));
        }

        [Test] public void AdMobAppIdIsRejectedAsUnitId()
        {
            config.ads.enabled = true; config.ads.testMode = false;
            config.ads.androidBanner = "ca-app-pub-123~456";
            Assert.IsTrue(FeatureBuildValidator.Validate(config, BuildTarget.Android).Any(i => i.message.Contains("not an App ID")));
        }

        [Test] public void FirebaseWithNoProductsSkipsProviderAndFileChecks()
        {
            config.firebase.enabled = true;
            config.firebase.analytics = config.firebase.crashlytics = config.firebase.remoteConfig = false;
            Assert.IsEmpty(FeatureBuildValidator.Validate(config, BuildTarget.Android));
        }

        [Test] public void MissingEnabledBuildSceneIsReportedWithoutChangingBuildSettings()
        {
            var scenes = new[] { new EditorBuildSettingsScene("Assets/DoesNotExist.unity", true) };
            Assert.IsTrue(FeatureBuildValidator.ValidateScenes(null, scenes).Any(i => i.message.StartsWith("Build scene is missing:")));
            Assert.IsTrue(scenes[0].enabled);
        }

        [Test] public void DisabledBuildScenesDoNotCountAsIncluded()
        {
            var scenes = new[] { new EditorBuildSettingsScene("Assets/Disabled.unity", false) };
            Assert.IsTrue(FeatureBuildValidator.ValidateScenes(null, scenes).Any(i => i.message.StartsWith("No scenes")));
        }
    }
}
