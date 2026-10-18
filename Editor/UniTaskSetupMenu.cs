using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace BaseUnity.Editor
{
    public static class UniTaskSetupMenu
    {
        public const string PackageUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11";
        private static AddRequest request;
        [MenuItem("Tools/BaseUnity/Integrations/Install UniTask 2.5.11")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before installing UniTask.");
            if (request != null && !request.IsCompleted) return;
            request = Client.Add(PackageUrl);
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
        }
        private static void Poll()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success) Debug.Log("BaseUnity: UniTask installed. Reference BaseUnity.UniTask and UniTask from your gameplay asmdef.");
            else Debug.LogError("BaseUnity: UniTask installation failed: " + request.Error?.message);
            request = null;
        }
    }
}
