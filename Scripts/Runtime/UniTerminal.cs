using System;
using System.Collections.Generic;
using System.Reflection;
using static NoSlimes.Util.UniTerminal.ConsoleCommandCache;

namespace NoSlimes.Util.UniTerminal
{
    public static class UniTerminal
    {
        #region Console Command Registry

        /// <summary>
        /// Manually scans the specified assembly for methods marked with <see cref="ConsoleCommandAttribute"/>.
        /// Useful for registering commands from mods or DLCs loaded at runtime.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public static void DiscoverCommandsInAssembly(Assembly assembly) => ConsoleCommandRegistry.DiscoverCommandsInAssembly(assembly);

        /// <summary>
        /// Manually loads the command cache. 
        /// Required if <see cref="UniTerminalUI.loadCacheOnAwake"/> is set to false in the inspector.
        /// </summary>
        public static void LoadCommandCache() => ConsoleCommandRegistry.LoadCache();

        /// <summary>
        /// Gets a read-only dictionary of all currently registered commands.
        /// Keys are command names (lowercase).
        /// </summary>
        public static IReadOnlyDictionary<string, List<CommandEntry>> Commands => ConsoleCommandRegistry.Commands;

        /// <summary>
        /// Event invoked when the initial command cache has finished loading.
        /// The <see cref="double"/> parameter represents the initialization time in milliseconds.
        /// </summary>
        public static event Action<double> OnCacheLoaded
        {
            add => ConsoleCommandRegistry.OnCacheLoaded += value;
            remove => ConsoleCommandRegistry.OnCacheLoaded -= value;
        }

        #endregion

        #region Console Command Invoker

        /// <summary>
        /// Registers a custom argument converter for a specific type <typeparamref name="T"/>.
        /// Allows UniTerminal to automatically parse arguments of this type from string input.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="converter">A function that takes a string and returns an instance of T.</param>
        public static void RegisterArgConverter<T>(Func<string, T> converter) => ConsoleCommandInvoker.RegisterArgConverter(converter);

        /// <summary>
        /// Executes a raw command string (including arguments) as if it were typed into the console.
        /// <para>Example: <c>UniTerminal.ExecuteCommand("spawn_item sword 5");</c></para>
        /// </summary>
        /// <param name="input">The full command line string.</param>
        public static void ExecuteCommand(string input) => ConsoleCommandInvoker.Execute(input);

        /// <summary>
        /// Logs a message to the UniTerminal console window.
        /// </summary>
        /// <param name="message">The text to log.</param>
        /// <param name="success">If <c>false</c>, the message will be logged as an error (usually red).</param>
        public static void Log(string message, bool success = true) => ConsoleCommandInvoker.Log(message, success);

        /// <summary>
        /// Gets or sets whether Cheat commands are allowed to run.
        /// Commands marked with <c>CommandFlags.Cheat</c> will fail if this is false.
        /// </summary>
        public static bool CheatsEnabled { get => ConsoleCommandInvoker.CheatsEnabled; set => ConsoleCommandInvoker.CheatsEnabled = value; }

        #endregion

        #region UI 

        /// <summary>
        /// Manually opens or closes the terminal UI.
        /// </summary>
        /// <param name="show">If true, opens the console. If false, closes it.</param>
        public static void ShowTerminalUI(bool show) => UniTerminalUI.ShowConsole(show);

        /// <summary>
        /// Toggles the visibility of the terminal UI based on its current state.
        /// </summary>
        public static void ToggleTerminalUI()
        {
            UniTerminalUI.ShowConsole(!UniTerminalUI.IsConsoleVisible);
        }

        /// <summary>
        /// Checks if the terminal UI is currently visible to the user.
        /// </summary>
        public static bool IsTerminalUIVisible => UniTerminalUI.IsConsoleVisible;

        /// <summary>
        /// Event invoked whenever the terminal UI is opened or closed.
        /// The boolean indicates the new visibility state (true = open).
        /// </summary>
        public static event Action<bool> OnConsoleToggled
        {
            add => UniTerminalUI.OnConsoleToggled += value;
            remove => UniTerminalUI.OnConsoleToggled -= value;
        }

        #endregion

    }

    public delegate void CommandResponseDelegate(string message, bool success = true);
}