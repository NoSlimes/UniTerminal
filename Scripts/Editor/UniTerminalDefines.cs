using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace NoSlimes.Util.UniTerminal.Editor
{
    [InitializeOnLoad]
    internal static class UniTerminalDefines
    {
        private static readonly string UNITERMINAL_DEFINE = "UNITERMINAL";
        private static readonly string UNITERMINAL_BUILTIN = "UNITERMINAL_BUILTIN";
        private static readonly string UNITERMINAL_ENABLECHEATS = "UNITERMINAL_ENABLECHEATS";

        internal static string KeyPrefix => $"{PlayerSettings.companyName}_{PlayerSettings.productName}_{PlayerSettings.productGUID}";

        internal static string AutoRebuildCacheKey => $"{KeyPrefix}_UniTerminal_AutoRebuildCache";
        internal static string IncludeBuiltInCommandsKey => $"{KeyPrefix}_UniTerminal_IncludeBuiltInCommands";
        internal static string IncludeCheatCommandKey => $"{KeyPrefix}_UniTerminal_IncludeCheatCommand";
        internal static string DetailedLoggingKey => $"{KeyPrefix}_UniTerminal_DetailedLogging";

        static UniTerminalDefines()
        {
            static void ApplyDefines()
            {
                if (UniTerminalSettings.instance.IncludeBuiltInCommands)
                    EnableBuiltinCommands();
                else
                    DisableBuiltinCommands();

                if (UniTerminalSettings.instance.IncludeCheatCommand)
                    EnableBuiltinCheatCommand();
                else
                    DisableBuiltinCheatCommand();

                EditorApplication.delayCall -= ApplyDefines;
            }

            EditorApplication.delayCall += ApplyDefines;

            DefineSymbol(UNITERMINAL_DEFINE);
        }

        public static void EnableBuiltinCommands() => DefineSymbol(UNITERMINAL_BUILTIN);
        public static void DisableBuiltinCommands() => UndefineSymbol(UNITERMINAL_BUILTIN);

        public static void EnableBuiltinCheatCommand() => DefineSymbol(UNITERMINAL_ENABLECHEATS);
        public static void DisableBuiltinCheatCommand() => UndefineSymbol(UNITERMINAL_ENABLECHEATS);

        private static void DefineSymbol(string symbol)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                ForEachBuildTarget(buildTarget =>
                {
                    try
                    {
                        NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(buildTarget);
                        string symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                        HashSet<string> parts = new(symbols.Split(';').Select(s => s.Trim()));

                        if (parts.Add(symbol))
                        {
                            PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", parts));
                        }

                        if (UniTerminalSettings.instance.IsDetailedLoggingEnabled)
                            Debug.Log($"Added symbol {symbol} to {buildTarget}");
                    }
                    catch
                    {
                        // Skip invalid/unsupported targets
                    }
                });
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static void UndefineSymbol(string symbol)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                ForEachBuildTarget(buildTarget =>
                {
                    try
                    {
                        NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(buildTarget);
                        string symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                        HashSet<string> parts = new(symbols.Split(';').Select(s => s.Trim()));

                        if (parts.Remove(symbol))
                        {
                            PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", parts));
                        }

                        if (UniTerminalSettings.instance.IsDetailedLoggingEnabled)
                            Debug.Log($"Removed symbol {symbol} from {buildTarget}");
                    }
                    catch
                    {
                        // Skip invalid/unsupported targets
                    }
                });
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static void ForEachBuildTarget(Action<BuildTargetGroup> action)
        {
            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown) continue;
                action(group);
            }
        }
    }
}
