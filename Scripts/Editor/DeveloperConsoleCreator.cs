using UnityEditor;
using UnityEngine;

namespace NoSlimes.Util.UniTerminal.Editor
{
    internal static class DeveloperConsoleCreator
    {
        [MenuItem("Assets/Create/UniTerminal/UniTerminal", priority = 81)]
        [MenuItem("Tools/UniTerminal/Create UniTerminal Prefab", priority = 81)]
        private static void CreateDeveloperConsole()
        {
            var prefab = Resources.Load<GameObject>("UniTerminal/UniTerminal");

            var newPrefab = CreatePrefabVariant(prefab);
            if (newPrefab != null)
            {
                InstantiateInScene(newPrefab);
                Debug.Log("[UniTerminal] UniTerminal prefab created and instantiated in the scene.");
            }
            else
            {
                Debug.LogError("[UniTerminal] Failed to create UniTerminal prefab.");
            }
        }

        private static GameObject CreatePrefabVariant(GameObject sourcePrefab)
        {
            if (sourcePrefab == null)
            {
                Debug.LogError("[UniTerminal] Source prefab is null.");
                return null;
            }

            GameObject tempInstance = Object.Instantiate(sourcePrefab);
            string dstPath = GetCurrentFolderPath() + $"/{sourcePrefab.name}.prefab";
            dstPath = AssetDatabase.GenerateUniqueAssetPath(dstPath);

            GameObject variant = PrefabUtility.SaveAsPrefabAsset(tempInstance, dstPath);
            Object.DestroyImmediate(tempInstance);

            return variant;
        }

        private static string GetCurrentFolderPath()
        {
            string path = "Assets";
            if (Selection.activeObject != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (System.IO.File.Exists(selectedPath))
                    path = System.IO.Path.GetDirectoryName(selectedPath);
                else
                    path = selectedPath;
            }
            else
            {
                Debug.Log("[UniTerminal] No folder selected in Project window, defaulting to 'Assets'.");
            }
            return path;
        }


        private static void InstantiateInScene(GameObject prefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance != null)
                instance.transform.position = Vector3.zero;
        }

    }
}