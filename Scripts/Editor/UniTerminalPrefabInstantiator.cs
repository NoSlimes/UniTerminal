using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NoSlimes.Util.UniTerminal.Editor
{
    internal static class UniTerminalPrefabInstantiator
    {
        [MenuItem("Assets/Create/UniTerminal/UniTerminal", priority = 81)]
        private static void CreateFromProjectWindow() => CreateDeveloperConsole();

        [MenuItem("Tools/UniTerminal/Create UniTerminal Prefab", priority = 81)]
        private static void CreateFromToolsMenu() => CreateDeveloperConsole();

        private static void CreateDeveloperConsole()
        {
            var sourcePrefab = FindSourcePrefab();
            if (sourcePrefab == null)
            {
                Debug.LogError("[UniTerminal] Source prefab not found. Reimport the UniTerminal package.");
                return;
            }

            var existing = Object.FindFirstObjectByType<UniTerminalUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.LogWarning("[UniTerminal] A UniTerminal instance already exists in this scene. Selecting it instead of duplicating.");
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            GameObject tempInstance = null;
            try
            {
                tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
                string dstPath = AssetDatabase.GenerateUniqueAssetPath(GetTargetFolder() + $"/{sourcePrefab.name}.prefab");

                GameObject variant = PrefabUtility.SaveAsPrefabAsset(tempInstance, dstPath);
                if (variant == null)
                {
                    Debug.LogError("[UniTerminal] Failed to create UniTerminal prefab.");
                    return;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(variant);
                Undo.RegisterCreatedObjectUndo(instance, "Create UniTerminal");
                Selection.activeObject = instance;
                EditorGUIUtility.PingObject(variant);
                EditorSceneManager.MarkSceneDirty(instance.scene);

                Debug.Log("[UniTerminal] UniTerminal prefab created and instantiated in the scene.");
            }
            finally
            {
                if (tempInstance != null)
                    Object.DestroyImmediate(tempInstance);
            }
        }

        private static GameObject FindSourcePrefab()
        {
            var loaded = Resources.Load<GameObject>("UniTerminal/UniTerminal");
            if (loaded != null)
                return loaded;

            foreach (string guid in AssetDatabase.FindAssets("UniTerminal t:Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.GetComponent<UniTerminalUI>() != null)
                    return prefab;
            }

            return null;
        }

        private static string GetTargetFolder()
        {
            // Only Project-window selections count. A Hierarchy selection has
            // no asset path and previously produced garbage save paths.
            foreach (Object selected in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (AssetDatabase.IsValidFolder(path))
                    return path;
                string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parent))
                    return parent;
            }

            return "Assets";
        }
    }
}
