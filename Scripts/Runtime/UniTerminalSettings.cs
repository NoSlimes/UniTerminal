#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NoSlimes.Util.UniTerminal
{
    [FilePath("ProjectSettings/UniTerminalSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class UniTerminalSettings : ScriptableSingleton<UniTerminalSettings>
    {
        public bool IsAutoRebuildEnabled { get => isAutoRebuildEnabled; set => isAutoRebuildEnabled = value; }
        public bool IncludeBuiltInCommands { get => includeBuiltInCommands; set => includeBuiltInCommands = value; }
        public bool IncludeCheatCommand { get => includeCheatCommand; set => includeCheatCommand = value; }
        public bool IsDetailedLoggingEnabled { get => isDetailedLoggingEnabled; set => isDetailedLoggingEnabled = value; }

        [SerializeField] private bool isAutoRebuildEnabled = true;
        [SerializeField] private bool includeBuiltInCommands = true;
        [SerializeField] private bool includeCheatCommand = true;
        [SerializeField] private bool isDetailedLoggingEnabled = false;

        internal void Save()
        {
            Save(true);
        }
    }
}
#endif