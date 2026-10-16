using System.IO;
using BaseUnity.Editor;
using NUnit.Framework;

namespace BaseUnity.Tests
{
    public sealed class FeatureConfigurationTests
    {
        private const string Json = "{\"project_info\":{\"project_id\":\"demo-project\"},\"client\":[" +
            "{\"client_info\":{\"mobilesdk_app_id\":\"first-app\",\"android_client_info\":{\"package_name\":\"com.example.first\"}}}," +
            "{\"client_info\":{\"mobilesdk_app_id\":\"second-app\",\"android_client_info\":{\"package_name\":\"com.example.second\"}}}]}";
        [Test] public void AndroidImportSelectsMatchingClient()
        {
            var info = FeatureConfigurationUtility.ReadFirebaseInfo(Json, false, "com.example.second");
            Assert.AreEqual("second-app", info.appId); Assert.AreEqual("demo-project", info.projectId);
        }
        [Test] public void AndroidImportRejectsDifferentPackage()
        { Assert.Throws<InvalidDataException>(() => FeatureConfigurationUtility.ReadFirebaseInfo(Json, false, "com.example.other")); }
        [Test] public void IncompleteFirebaseFileIsRejected()
        { Assert.Throws<InvalidDataException>(() => FeatureConfigurationUtility.ReadFirebaseInfo("{\"project_info\":{\"project_id\":\"x\"}}", false)); }
        [Test] public void PlistSupportsOfficialDoctypeWithoutResolvingExternalFiles()
        {
            const string plist = "<?xml version=\"1.0\"?><!DOCTYPE plist SYSTEM \"https://invalid.example/never-fetch\"><plist><dict>" +
                "<key>PROJECT_ID</key>\n<string>project</string><key>GOOGLE_APP_ID</key><string>app</string><key>BUNDLE_ID</key><string>com.example.game</string></dict></plist>";
            var info = FeatureConfigurationUtility.ReadFirebaseInfo(plist, true, "com.example.game");
            Assert.AreEqual("project", info.projectId); Assert.AreEqual("app", info.appId);
            Assert.Throws<InvalidDataException>(() => FeatureConfigurationUtility.ReadFirebaseInfo(plist, true, "different.bundle"));
        }
        [Test] public void AppIdValidationRejectsAdUnitId()
        {
            Assert.DoesNotThrow(() => FeatureConfigurationUtility.ValidateAppId("ca-app-pub-3940256099942544~3347511713"));
            Assert.DoesNotThrow(() => FeatureConfigurationUtility.ValidateAppId(""));
            Assert.Throws<InvalidDataException>(() => FeatureConfigurationUtility.ValidateAppId("ca-app-pub-3940256099942544/6300978111"));
        }
    }
}
