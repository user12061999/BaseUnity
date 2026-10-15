using BaseUnity.Core;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    public static class FoundationMenu
    {
        [MenuItem("GameObject/BaseUnity/Bootstrap", false, 10)]
        private static void CreateBootstrap()
        {
            var root = new GameObject("Game Bootstrap");
            Undo.RegisterCreatedObjectUndo(root, "Create BaseUnity Bootstrap");
            Undo.AddComponent<GameBootstrap>(root);
            Selection.activeGameObject = root;
        }
    }
}
