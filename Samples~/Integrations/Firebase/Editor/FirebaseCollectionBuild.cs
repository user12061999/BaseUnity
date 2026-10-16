#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace BaseUnity.Integrations.Editor
{
    public static class FirebaseCollectionBuild
    {
        [PostProcessBuild(10000)]
        public static void DisableAutomaticCollection(BuildTarget target, string directory)
        {
            if (target != BuildTarget.iOS) return;
            string path = Path.Combine(directory, "Info.plist");
            var plist = new PlistDocument(); plist.ReadFromFile(path);
            plist.root.SetBoolean("FIREBASE_ANALYTICS_COLLECTION_ENABLED", false);
            plist.root.SetBoolean("FirebaseCrashlyticsCollectionEnabled", false);
            File.WriteAllText(path, plist.WriteToString());
        }
    }
}
#endif
