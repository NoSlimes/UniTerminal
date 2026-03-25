using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoSlimes.Util.UniTerminal
{
    internal static class BuiltInCommands
    {
#if UNITERMINAL_BUILTIN
        #region Application

        [ConsoleCommand("quit", Description = "Quits the application.", Group = "application")]
        public static void QuitCommand(Action<string> response)
        {
            response("Quitting application...");
            Application.Quit();
        }

        [ConsoleCommand("crash", Description = "Crashes the application (for testing purposes).", Group = "application")]
        public static void CrashCommand(Action<string> response)
        {
            response("Crashing application...");
            UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.Abort);
        }

        [ConsoleCommand("version", Description = "Prints the application version.", Group = "application")]
        public static void VersionCommand(Action<string> response) =>
            response($"Application version: {Application.version}");

        [ConsoleCommand("platform", Description = "Prints the current runtime platform.", Group = "application")]
        public static void PlatformCommand(Action<string> response) =>
            response($"Running on: {Application.platform}");

        [ConsoleCommand("datapath", Description = "Prints the data path of the application.", Group = "application")]
        public static void DataPathCommand(Action<string> response) =>
            response($"Data path: {Application.dataPath}");

        [ConsoleCommand("persistentdatapath", Description = "Prints the persistent data path.", Group = "application")]
        public static void PersistentDataPathCommand(Action<string> response) =>
            response($"Persistent data path: {Application.persistentDataPath}");

        [ConsoleCommand("settargetfps", Description = "Sets Application.targetFrameRate.", Group = "application")]
        public static void SetTargetFPSCommand(Action<string> response, int fps)
        {
            Application.targetFrameRate = fps;
            response($"Target frame rate set to {fps}");
        }

        [ConsoleCommand("uptime", Description = "Prints the time since startup.", Group = "application")]
        public static void UptimeCommand(Action<string> response) =>
            response($"Uptime: {Time.realtimeSinceStartup:F2} seconds");

        #endregion

        #region Scene Management

        [ConsoleCommand("reloadscene", Description = "Reloads the current scene.", Group = "scene")]
        public static void ReloadSceneCommand(Action<string> response)
        {
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            response($"Reloading scene: {scene.name}");
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }

        [ConsoleCommand("loadscene", Description = "Loads a scene by name.", Group = "scene")]
        public static void LoadSceneCommand(Action<string> response, string sceneName)
        {
            response($"Loading scene: {sceneName}");
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        [ConsoleCommand("listscenes", Description = "Lists all scenes in the build settings.", Group = "scene")]
        public static void ListScenesCommand(Action<string> response)
        {
            IEnumerable<string> scenes = Enumerable
                .Range(0, UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
                .Select(i => System.IO.Path.GetFileNameWithoutExtension(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i)));

            response("Scenes in build settings: " + string.Join(", ", scenes));
        }

        #endregion

        #region Time & Physics

        [ConsoleCommand("gravityscale", Description = "Sets the global gravity scale.", Group = "physics")]
        public static void GravityScaleCommand(Action<string> response, float scale)
        {
            Physics.gravity = new Vector3(0, -9.81f * scale, 0);
            response($"Global gravity scale set to {scale}. New gravity: {Physics.gravity}");
        }

        [ConsoleCommand("timescale", Description = "Sets the global time scale.", Group = "time")]
        public static void TimeScaleCommand(Action<string> response, float scale)
        {
            Time.timeScale = scale;
            response($"Global time scale set to {scale}.");
        }

        [ConsoleCommand("fixeddeltatime", Description = "Sets Time.fixedDeltaTime.", Group = "time")]
        public static void FixedDeltaTimeCommand(Action<string> response, float seconds)
        {
            Time.fixedDeltaTime = seconds;
            response($"FixedDeltaTime set to {seconds}");
        }

        #endregion

        #region Graphics & Quality

        [ConsoleCommand("vsync", Description = "Sets VSync count (0 = off, 1 = every vsync, 2 = every 2nd vsync).", Group = "graphics")]
        public static void VSyncCommand(Action<string> response, int count)
        {
            QualitySettings.vSyncCount = count;
            response($"VSync set to {count}");
        }

        [ConsoleCommand("setquality", Description = "Sets the graphics quality level by index or name.", Group = "graphics")]
        public static void SetQualityCommand(Action<string> response, string quality)
        {
            if (int.TryParse(quality, out var index))
                QualitySettings.SetQualityLevel(index, true);
            else
                QualitySettings.SetQualityLevel(QualitySettings.names.ToList().IndexOf(quality), true);

            response($"Graphics quality set to {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        }

        [ConsoleCommand("listquality", Description = "Lists available graphics quality levels.", Group = "graphics")]
        public static void ListQualityCommand(Action<string> response) =>
            response("Available quality levels: " + string.Join(", ", QualitySettings.names));

        [ConsoleCommand("fullscreen", Description = "Toggles fullscreen mode.", Group = "graphics")]
        public static void FullscreenCommand(Action<string> response, bool enabled)
        {
            Screen.fullScreen = enabled;
            response($"Fullscreen set to {enabled}");
        }

        [ConsoleCommand("resolutions", Description = "Lists supported screen resolutions.", Group = "graphics")]
        public static void ResolutionsCommand(Action<string> response)
        {
            IEnumerable<string> resolutions = Screen.resolutions.Select(r => $"{r.width}x{r.height}@{r.refreshRateRatio}Hz");
            response("Supported resolutions:\n" + string.Join("\n", resolutions));
        }

        [ConsoleCommand("setresolution", Description = "Sets the screen resolution (width, height, fullscreen).", Group = "graphics")]
        public static void SetResolutionCommand(Action<string> response, int width, int height, bool fullscreen)
        {
            Screen.SetResolution(width, height, fullscreen);
            response($"Resolution set to {width}x{height}, fullscreen={fullscreen}");
        }

        #endregion

        #region Camera & Debug

        [ConsoleCommand("setfov", Description = "Sets the main camera's field of view.", Group = "camera")]
        public static void SetFOVCommand(Action<string> response, float fov)
        {
            if (Camera.main != null)
            {
                Camera.main.fieldOfView = fov;
                response($"Main camera FOV set to {fov}");
            }
            else response("<color=yellow>No main camera found.</color>");
        }

        [ConsoleCommand("screenshot", Description = "Takes a screenshot and saves it.", Group = "debug")]
        public static void ScreenshotCommand(Action<string> response, string filename = "screenshot.png")
        {
            ScreenCapture.CaptureScreenshot(filename);
            response($"Screenshot saved: {filename}");
        }

        #endregion

        #region System Information

        [ConsoleCommand("systeminfo", Description = "Prints system information (GPU, CPU, RAM).", Group = "system")]
        public static void SystemInfoCommand(Action<string> response)
        {
            response($"Device: {SystemInfo.deviceName} ({SystemInfo.deviceModel})");
            response($"OS: {SystemInfo.operatingSystem}");
            response($"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} cores)");
            response($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB VRAM)");
            response($"RAM: {SystemInfo.systemMemorySize} MB");
        }

        #endregion

        #region PlayerPrefs

        [ConsoleCommand("set", Description = "Sets a PlayerPref string value.", Group = "prefs")]
        public static void SetPrefCommand(Action<string> response, string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            response($"PlayerPref set: {key} = {value}");
        }

        [ConsoleCommand("get", Description = "Gets a PlayerPref string value.", Group = "prefs")]
        public static void GetPrefCommand(Action<string> response, string key)
        {
            string value = PlayerPrefs.GetString(key, "(not found)");
            response($"PlayerPref: {key} = {value}");
        }

        [ConsoleCommand("delete", Description = "Deletes a PlayerPref key.", Group = "prefs")]
        public static void DeletePrefCommand(Action<string> response, string key)
        {
            PlayerPrefs.DeleteKey(key);
            response($"Deleted PlayerPref: {key}");
        }

        [ConsoleCommand("clear", Description = "Clears all PlayerPrefs.", Group = "prefs")]
        public static void ClearPrefsCommand(Action<string> response)
        {
            PlayerPrefs.DeleteAll();
            response("All PlayerPrefs cleared.");
        }

        #endregion
#endif

#if UNITERMINAL_ENABLECHEATS
        [ConsoleCommand("enablecheats", Description = "Enables cheat commands.")]
        private static void EnableCheatsCommand(Action<string> response, bool enable = true)
        {
            ConsoleCommandInvoker.CheatsEnabled = enable;

            string status = enable ? "enabled" : "disabled";
            response($"Cheats {status}.");
        }
#endif
    }
}