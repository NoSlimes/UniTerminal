using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NoSlimes.Util.UniTerminal.Editor
{
#if UNITERMINAL_EDITOR_BUILTIN
    internal static class EditorBuiltInCommands
    {
        #region Assets

        [ConsoleCommand("saveall", Description = "Saves all dirty assets.", Group = "editor", Flags = CommandFlags.EditorOnly, AllowInEditor = true)]
        public static void SaveAllCommand(Action<string> response)
        {
            AssetDatabase.SaveAssets();
            response("Saved all assets.");
        }

        [ConsoleCommand("refresh", Description = "Refreshes the asset database.", Group = "editor", Flags = CommandFlags.EditorOnly, AllowInEditor = true)]
        public static void RefreshCommand(Action<string> response)
        {
            AssetDatabase.Refresh();
            response("Asset database refreshed.");
        }

        #endregion

        #region Scene Info

        [ConsoleCommand("activescene", Description = "Prints the active scene name, path and dirty state.", Group = "editor", Flags = CommandFlags.EditorOnly, AllowInEditor = true)]
        public static void ActiveSceneCommand(Action<string> response)
        {
            var scene = EditorSceneManager.GetActiveScene();
            response($"Active scene: {scene.name} ({scene.path}){(scene.isDirty ? " [dirty]" : "")} — {scene.rootCount} root objects.");
        }

        [ConsoleCommand("buildscenes", Description = "Lists scenes in the build settings.", Group = "editor", Flags = CommandFlags.EditorOnly, AllowInEditor = true)]
        public static void BuildScenesCommand(Action<string> response)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                response("No scenes in build settings.");
                return;
            }
            IEnumerable<string> lines = scenes.Select((s, i) => $"{i}. {(s.enabled ? "[x]" : "[ ]")} {s.path}");
            response("Scenes in build settings:\n" + string.Join("\n", lines));
        }

        [ConsoleCommand("findmissing", Description = "Lists GameObjects with missing scripts in open scenes (report only).", Group = "editor", Flags = CommandFlags.EditorOnly, AllowInEditor = true)]
        public static void FindMissingCommand(Action<string> response)
        {
            var hits = new List<string>();
            int checkedCount = 0;

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                foreach (GameObject root in EditorSceneManager.GetSceneAt(i).GetRootGameObjects())
                    Walk(root);
            }

            void Walk(GameObject go)
            {
                checkedCount++;
                foreach (Component c in go.GetComponents<Component>())
                {
                    if (c == null)
                    {
                        hits.Add(FullPath(go));
                        break;
                    }
                }
                foreach (Transform child in go.transform)
                    Walk(child.gameObject);
            }

            static string FullPath(GameObject go)
            {
                var parts = new List<string>();
                for (Transform t = go.transform; t != null; t = t.parent)
                    parts.Add(t.name);
                parts.Reverse();
                return string.Join("/", parts);
            }

            if (hits.Count == 0)
            {
                response($"Checked {checkedCount} objects — no missing scripts.");
                return;
            }
            const int maxShown = 15;
            string shown = string.Join("\n", hits.Take(maxShown).Select(h => $"  {h}"));
            string more = hits.Count > maxShown ? $"\n  … and {hits.Count - maxShown} more." : "";
            response($"Checked {checkedCount} objects — {hits.Count} with missing scripts:\n{shown}{more}");
        }

        #endregion

        #region Settings Windows

        [ConsoleCommand("settings", Description = "Opens Project Settings, optionally at a page.", Group = "editor", Flags = CommandFlags.EditorOnly, AllowInEditor = true)]
        public static void SettingsCommand(Action<string> response,
            [SuggestValues("Player", "Editor", "Graphics", "Quality", "Physics", "Tags and Layers", "Script Execution Order", "Audio", "Time", "Preset Manager")] string page = "")
        {
            if (string.IsNullOrWhiteSpace(page))
                SettingsService.OpenProjectSettings();
            else
                SettingsService.OpenProjectSettings($"Project/{page.Trim()}");
            response(string.IsNullOrWhiteSpace(page) ? "Opened Project Settings." : $"Opened Project Settings at '{page}'.");
        }

        [ConsoleCommand("prefs", Description = "Opens Preferences, optionally at a page.", Group = "editor", Flags = CommandFlags.EditorOnly, AllowInEditor = true)]
        public static void PreferencesCommand(Action<string> response,
            [SuggestValues("General", "External Tools", "Colors", "Keybindings")] string page = "")
        {
            if (string.IsNullOrWhiteSpace(page))
                SettingsService.OpenUserPreferences();
            else
                SettingsService.OpenUserPreferences($"Preferences/{page.Trim()}");
            response(string.IsNullOrWhiteSpace(page) ? "Opened Preferences." : $"Opened Preferences at '{page}'.");
        }

        #endregion

        #region Diagnostics

        [ConsoleCommand("gc", Description = "Unloads unused assets and collects garbage, reporting managed memory.", Group = "editor", Flags = CommandFlags.EditorOnly, AllowInEditor = true)]
        public static void GCCommand(Action<string> response)
        {
            long before = GC.GetTotalMemory(false);
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
            long after = GC.GetTotalMemory(true);
            response($"Managed memory: {before / 1024f / 1024f:F1} MB → {after / 1024f / 1024f:F1} MB.");
        }

        #endregion
    }
#endif
}
