using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    /// <summary>Makes Unity's configured JDK available to child tools such as EDM4U Gradle.</summary>
    [InitializeOnLoad]
    public static class AndroidJavaEnvironment
    {
        static AndroidJavaEnvironment() { EnsureJavaHome(); }

        // Process scope only: do not change the developer's system environment or explicit JAVA_HOME.
        public static bool EnsureJavaHome()
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JAVA_HOME"))) return false;
            string root = null;
            try
            {
                var type = FeatureConfigurationUtility.FindType("UnityEditor.Android.AndroidExternalToolsSettings");
                root = type?.GetProperty("jdkRootPath", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
            }
            catch (Exception) { return false; }
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK");
            string suffix = Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : "";
            if (!File.Exists(Path.Combine(root, "bin", "java" + suffix)) ||
                !File.Exists(Path.Combine(root, "bin", "javac" + suffix))) return false;
            Environment.SetEnvironmentVariable("JAVA_HOME", root, EnvironmentVariableTarget.Process);
            return true;
        }

        [MenuItem("Tools/BaseUnity/Features/Configure Java for Android Resolver")]
        public static void Configure()
        {
            bool changed = EnsureJavaHome();
            Debug.Log(changed ? "JAVA_HOME configured from Unity's JDK for this Editor process. Run Android Resolver again." :
                "JAVA_HOME is already set, or Unity's JDK is unavailable. Check Preferences > External Tools and the Editor process environment.");
        }
    }
}
