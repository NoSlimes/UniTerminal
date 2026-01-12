#if UNITY_EDITOR
using UnityEditor;

namespace NoSlimes.Util.UniTerminal
{
    internal class UniTerminalSettings : ScriptableSingleton<UniTerminalSettings>
    {
        public bool IsAutoRebuildEnabled { get; internal set; } = true;
        public bool IncludeBuiltInCommands { get; internal set; } = true;
        public bool IncludeCheatCommand { get; internal set; } = true;
        public bool IsDetailedLoggingEnabled { get; internal set; } = false;

        internal void Save()
        {
            Save(true);
        }
    }
}
#endif