using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using static NoSlimes.Util.UniTerminal.ConsoleCommandCache;
using static NoSlimes.Util.UniTerminal.UniTerminal;

namespace NoSlimes.Util.UniTerminal
{
    public partial class ConsoleCommandInvoker
    {
        public static class Settings
        {
            public static Color TextColor = Color.white;
            public static Color WarningColor = Color.yellow;
            public static Color ErrorColor = Color.red;
            public static Color SecondaryErrorColor = new(1f, 0.6f, 0.6f);

            // Help text colors
            public static Color Help_SectionColor = Color.white;
            public static Color Help_CommandColor = new(0.5f, 1f, 1f);
            public static Color Help_MutedColor = new(0.7f, 0.7f, 0.7f);
            public static Color Help_InfoColor = new(0.5f, 1f, .5f);
            public static Color Help_OptionalColor = new(1f, 0.8f, 0.5f);
        }

        internal static string Colorize(string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
        }
    }

    public static partial class ConsoleCommandInvoker
    {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitOnPlay()
        {
            _ = typeof(ConsoleCommandInvoker); // forces the static constructor to run
        }

        static ConsoleCommandInvoker()
        {

            RegisterArgConverter<Vector3>(static arg =>
            {
                string[] parts = arg.Trim('(', ')').Split(',');
                if (parts.Length != 3)
                    throw new ArgumentException($"Could not convert '{arg}' to {typeof(Vector3).Name}");

                return new Vector3(
                    float.Parse(parts[0]),
                    float.Parse(parts[1]),
                    float.Parse(parts[2])
                );
            });

            RegisterArgConverter<Vector3Int>(static arg =>
            {
                string[] parts = arg.Trim('(', ')').Split(',');
                if (parts.Length != 3)
                    throw new ArgumentException($"Could not convert '{arg}' to {typeof(Vector3Int).Name}");

                return new Vector3Int(
                    int.Parse(parts[0]),
                    int.Parse(parts[1]),
                    int.Parse(parts[2])
                );
            });

            RegisterArgConverter<Vector2>(static arg =>
            {
                string[] parts = arg.Trim('(', ')').Split(',');
                if (parts.Length != 2)
                    throw new ArgumentException($"Could not convert '{arg}' to {typeof(Vector2).Name}");
                return new Vector2(
                    float.Parse(parts[0]),
                    float.Parse(parts[1])
                );
            });

            RegisterArgConverter<Vector2Int>(static arg =>
            {
                string[] parts = arg.Trim('(', ')').Split(',');
                if (parts.Length != 2)
                    throw new ArgumentException($"Could not convert '{arg}' to {typeof(Vector2Int).Name}");
                return new Vector2Int(
                    int.Parse(parts[0]),
                    int.Parse(parts[1])
                );
            });

            RegisterArgConverter<Color>(static arg =>
            {
                if (ColorUtility.TryParseHtmlString(arg, out Color col))
                    return col;

                string[] parts = arg.Trim('(', ')').Split(',');
                if (parts.Length == 4)
                {
                    return new Color(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    );
                }

                if (parts.Length == 3)
                {
                    return new Color(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), 1f);
                }

                throw new ArgumentException($"Could not convert '{arg}' to Color. Use 'red', '#FF0000' or '(r,g,b,a)'.");
            });

            RegisterArgConverter<Quaternion>(static arg =>
            {
                string[] parts = arg.Trim('(', ')').Split(',');
                if (parts.Length != 4)
                    throw new ArgumentException($"Could not convert '{arg}' to {typeof(Quaternion).Name}");
                return new Quaternion(
                    float.Parse(parts[0]),
                    float.Parse(parts[1]),
                    float.Parse(parts[2]),
                    float.Parse(parts[3])
                );
            });
        }
    }

    public static partial class ConsoleCommandInvoker
    {
        /// <summary>
        /// Where command responses and console feedback are routed.
        /// </summary>
        internal static Action<string, bool> LogHandler { get; set; } = (msg, success) => { };

        /// <summary>
        /// Indicates whether cheat commands are currently allowed to execute.
        /// Commands marked with the <see cref="CommandFlags.Cheat"/> flag
        /// will only run if this property is <c>true</c>.
        /// </summary>
        public static bool CheatsEnabled { get; set; } = false;

        // Regex that matches quoted strings OR non-space sequences
        private static readonly Regex ArgTokenizer = new(
            @"[\""].+?[\""]|[^ ]+",
            RegexOptions.Compiled
        );

        private static readonly Dictionary<Type, Func<string, object>> ArgConverters = new();
        private static readonly Dictionary<MethodInfo, MethodInfo> SuggestionMethodCache = new();
        private static readonly Dictionary<Type, UnityEngine.Object> UnityInstanceCache = new();

        internal static void RegisterArgConverter<T>(Func<string, T> converter)
        {
            ArgConverters[typeof(T)] = arg => converter(arg);
        }

        private static string[] Tokenize(string input)
        {
            var matches = ArgTokenizer.Matches(input);
            string[] results = new string[matches.Count];

            for (int i = 0; i < matches.Count; i++)
            {
                results[i] = matches[i].Value.Trim('"');
            }
            return results;
        }

        internal static object ConvertArg(string arg, Type targetType)
        {
            if (targetType == typeof(string)) return arg;

            Type typeToUse = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (ArgConverters.TryGetValue(typeToUse, out Func<string, object> converter))
                return converter(arg);

            if (targetType == typeof(int) && int.TryParse(arg, out int i)) return i;
            if (targetType == typeof(double) && double.TryParse(arg, out double d)) return d;
            if (targetType == typeof(long) && long.TryParse(arg, out long l)) return l;
            if (targetType == typeof(short) && short.TryParse(arg, out short s)) return s;
            if (targetType == typeof(byte) && byte.TryParse(arg, out byte by)) return by;
            if (targetType == typeof(decimal) && decimal.TryParse(arg, out decimal dec)) return dec;
            if (targetType == typeof(uint) && uint.TryParse(arg, out uint ui)) return ui;
            if (targetType == typeof(ulong) && ulong.TryParse(arg, out ulong ul)) return ul;
            if (targetType == typeof(ushort) && ushort.TryParse(arg, out ushort us)) return us;
            if (targetType == typeof(sbyte) && sbyte.TryParse(arg, out sbyte sb)) return sb;

            if (targetType == typeof(float) && float.TryParse(arg, out float f)) return f;
            if (targetType == typeof(bool) && bool.TryParse(arg, out bool b)) return b;
            if (targetType.IsEnum && Enum.TryParse(targetType, arg, true, out object e)) return e;
            throw new ArgumentException($"Could not convert '{arg}' to {targetType.Name}");
        }

        internal static void Log(string message, bool success)
        {
            LogHandler(message, success);
        }

        internal static void Log(string message, Color color)
        {
            message = Colorize(message, color);
            LogHandler(message, true);
        }

        internal static void Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            LogHandler(Colorize("> ", Settings.TextColor) + input, true);

            string[] parts = Tokenize(input);
            if (parts.Length == 0) return;

            string command = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            if (!ConsoleCommandRegistry.Commands.TryGetValue(command, out List<CommandEntry> entryList))
            {
                LogHandler(Colorize($"Unknown command: '{command}'. Type 'help' for a list of commands.", Settings.WarningColor), false);
                return;
            }

            MethodInfo matchedMethod = null;
            object[] finalArgs = null;
            int bestScore = int.MinValue;

            List<string> candidateErrors = new();

            foreach (CommandEntry entry in entryList)
            {
                ParameterInfo[] parameters = entry.MethodInfo.GetParameters();

                int paramOffset = 0;

                bool hasResponse = parameters.Length > 0 &&
                    (parameters[0].ParameterType == typeof(Action<string>) ||
                     parameters[0].ParameterType == typeof(Action<string, bool>) ||
                     parameters[0].ParameterType == typeof(CommandResponseDelegate));

                if (hasResponse) paramOffset = 1;

                var effectiveParams = parameters.Skip(paramOffset).ToArray();

                bool hasParams = effectiveParams.Length > 0 &&
                    effectiveParams[^1].GetCustomAttribute<ParamArrayAttribute>() != null;

                int requiredCount = effectiveParams
                    .Count(p => !p.HasDefaultValue && p.GetCustomAttribute<ParamArrayAttribute>() == null);

                if (args.Length < requiredCount)
                {
                    candidateErrors.Add($"[{GetMethodSignature(entry.MethodInfo)}] Missing required arguments.");
                    continue;
                }

                if (!hasParams && args.Length > effectiveParams.Length)
                {
                    candidateErrors.Add($"[{GetMethodSignature(entry.MethodInfo)}] Too many arguments provided.");
                    continue;
                }

                object[] tempArgs = new object[parameters.Length];

                // Inject callback
                if (hasResponse)
                {
                    Type callbackType = parameters[0].ParameterType;

                    if (callbackType == typeof(Action<string, bool>))
                        tempArgs[0] = LogHandler;
                    else if (callbackType == typeof(Action<string>))
                        tempArgs[0] = new Action<string>(msg => LogHandler(msg, true));
                    else if (callbackType == typeof(CommandResponseDelegate))
                        tempArgs[0] = new CommandResponseDelegate((msg, success) => LogHandler(msg, success));
                }

                int argIndex = 0;
                int score = 0;
                bool success = true;

                for (int i = paramOffset; i < parameters.Length; i++)
                {
                    var p = parameters[i];
                    bool isParams = p.GetCustomAttribute<ParamArrayAttribute>() != null;

                    int remainingArgs = args.Length - argIndex;

                    int minRemainingParams = parameters
                        .Skip(i + 1)
                        .Count(x => !x.HasDefaultValue && x.GetCustomAttribute<ParamArrayAttribute>() == null);

                    if (isParams)
                    {
                        Type elemType = p.ParameterType.GetElementType();
                        int count = Math.Max(0, args.Length - argIndex);

                        Array arr = Array.CreateInstance(elemType, count);

                        for (int j = 0; j < count; j++)
                        {
                            try
                            {
                                object converted = ConvertArg(args[argIndex + j], elemType);
                                arr.SetValue(converted, j);
                                score += 1;
                            }
                            catch (Exception ex)
                            {
                                bool isOptional = p.HasDefaultValue && !isParams;

                                if (isOptional)
                                {
                                    tempArgs[i] = p.DefaultValue;
                                    continue;
                                }

                                if (hasParams)
                                {
                                    success = false;
                                    break;
                                }

                                string msg = ex.InnerException?.Message ?? ex.Message;
                                candidateErrors.Add($"[{GetMethodSignature(entry.MethodInfo)}] Error parsing arg '{p.Name}' ({p.ParameterType.Name}): {msg}");
                                success = false;
                                break;
                            }
                        }

                        tempArgs[i] = arr;
                        argIndex = args.Length;
                        break;
                    }

                    bool shouldConsumeArg = true;

                    if (p.HasDefaultValue)
                    {
                        if (remainingArgs - 1 < minRemainingParams)
                            shouldConsumeArg = false;
                    }

                    if (shouldConsumeArg && argIndex < args.Length)
                    {
                        try
                        {
                            object converted = ConvertArg(args[argIndex], p.ParameterType);
                            tempArgs[i] = converted;

                            score += (converted.GetType() == p.ParameterType) ? 2 : 1;
                            argIndex++;
                        }
                        catch
                        {
                            if (p.HasDefaultValue)
                            {
                                tempArgs[i] = p.DefaultValue;
                                score += 1;
                                continue;
                            }

                            candidateErrors.Add($"[{GetMethodSignature(entry.MethodInfo)}] Error parsing arg '{p.Name}' ({p.ParameterType.Name}).");
                            success = false;
                            break;
                        }
                    }
                    else if (p.HasDefaultValue)
                    {
                        tempArgs[i] = p.DefaultValue;
                        score += 1;
                    }
                    else
                    {
                        candidateErrors.Add($"[{GetMethodSignature(entry.MethodInfo)}] Missing required argument '{p.Name}'.");
                        success = false;
                        break;
                    }
                }

                if (!success)
                    continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    matchedMethod = entry.MethodInfo;
                    finalArgs = tempArgs;
                }
            }

            if (matchedMethod == null)
            {
                LogHandler(Colorize($"Could not execute '{command}'. Potential reasons:", Settings.ErrorColor), false);
                foreach (string error in candidateErrors)
                {
                    LogHandler(Colorize($"- {error}", Settings.SecondaryErrorColor), false);
                }
                return;
            }

            object target = ResolveTarget(matchedMethod);
            if (target != null || matchedMethod.IsStatic)
            {
                try
                {
                    ConsoleCommandAttribute attr = matchedMethod.GetCustomAttribute<ConsoleCommandAttribute>();

                    bool BlockLocal(string reason)
                    {
                        LogHandler(Colorize($"Cannot run '{attr.Name}': {reason}.", Settings.WarningColor), false);
                        return true;
                    }

                    if (attr.Flags.HasFlag(CommandFlags.Cheat) && !CheatsEnabled &&
                        BlockLocal("cheats are disabled")) return;

                    if (attr.Flags.HasFlag(CommandFlags.DebugOnly) && !Debug.isDebugBuild &&
                        BlockLocal("debug-only commands are not allowed in this build")) return;

                    if (attr.Flags.HasFlag(CommandFlags.EditorOnly) && !Application.isEditor &&
                        BlockLocal("editor-only commands are not allowed in builds")) return;

                    matchedMethod.Invoke(target, finalArgs);
                }
                catch (Exception e)
                {
                    var realException = (e is TargetInvocationException && e.InnerException != null) ? e.InnerException : e;
                    string exceptionString = realException.Message;
#if DEBUG
                    exceptionString += Colorize($"\n{realException.StackTrace}", Settings.SecondaryErrorColor);
#endif

                    LogHandler(Colorize($"Error: An exception occurred while executing command '{command}'\n{exceptionString}", Settings.ErrorColor), false);
                }
            }
            else
            {
                LogHandler(Colorize($"Error: Could not find instance of '{matchedMethod.DeclaringType.Name}' for command '{command}'.", Settings.ErrorColor), false);
            }
        }

        private static string GetMethodSignature(MethodInfo method)
        {
            string[] pars = method.GetParameters()
                .Where(p => !(p.ParameterType == typeof(Action<string>) || p.ParameterType == typeof(Action<string, bool>) || p.ParameterType == typeof(CommandResponseDelegate)))
                .Select(p => $"{p.ParameterType.Name} {p.Name}")
                .ToArray();

            if (pars.Length == 0) return "void";

            return string.Join(", ", pars);
        }

        private static object ResolveTarget(MethodInfo method)
        {
            if (method.IsStatic) return null;
            Type targetType = method.DeclaringType;

            if (UnityInstanceCache.TryGetValue(targetType, out UnityEngine.Object cachedInstance))
            {
                if (cachedInstance != null)
                    return cachedInstance;

                UnityInstanceCache.Remove(targetType);
            }

            object targetInstance = null;

            if (targetType.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                targetInstance = UnityEngine.Object.FindFirstObjectByType(targetType);

                if (targetInstance != null)
                {
                    UnityInstanceCache[targetType] = (UnityEngine.Object)targetInstance;
                }
            }
            else
            {
                LogHandler(Colorize("Error: Non-static command methods must belong to a UnityEngine.Object subclass", Settings.ErrorColor), false);
            }

            return targetInstance;
        }

        public static string GetHelp(string commandName = "")
        {
            StringBuilder helpBuilder = new();

            static string FormatDefault(object value)
            {
                if (value is null) return "null";
                if (value is string s && s == string.Empty) return "\"\"";
                if (value is char c) return $"'{c}'";
                return value.ToString();
            }

            static string FormatParameter(ParameterInfo p)
            {
                string typeName = p.ParameterType.IsArray
                    ? $"{p.ParameterType.GetElementType()?.Name}[]"
                    : p.ParameterType.Name;

                bool isParams = p.GetCustomAttribute<ParamArrayAttribute>() != null;

                if (isParams)
                    return Colorize($"...{p.Name}:{typeName} (params)", Settings.Help_InfoColor);

                if (p.HasDefaultValue)
                    return Colorize(
                        $"{p.Name}:{typeName}={FormatDefault(p.DefaultValue)} (optional)",
                        Settings.Help_OptionalColor
                    );

                return $"{p.Name}:{typeName}";
            }

            static bool IsInternalDelegate(ParameterInfo p)
            {
                return p.ParameterType == typeof(Action<string>) ||
                       p.ParameterType == typeof(Action<string, bool>) ||
                       p.ParameterType == typeof(CommandResponseDelegate);
            }

            List<string> GetAliasesForKey(string key)
            {
                var aliases = new HashSet<string>();

                foreach (var kv in ConsoleCommandRegistry.AliasLookup)
                {
                    var entry = kv.Value;
                    var entryKey = string.IsNullOrWhiteSpace(entry.Group)
                        ? entry.CommandName.ToLower()
                        : $"{entry.Group.ToLower()}.{entry.CommandName.ToLower()}";

                    if (entryKey == key)
                    {
                        aliases.Add(kv.Key.Split('.').Last());
                    }
                }

                return aliases.OrderBy(a => a).ToList();
            }

            void AppendCommandLine(string name, IEnumerable<CommandEntry> entries)
            {
                var list = entries.Distinct().ToList();
                var aliases = GetAliasesForKey(name);

                string aliasText = aliases.Count > 0
                    ? $" {Colorize("(aliases: " + string.Join(", ", aliases) + ")", Settings.Help_MutedColor)}"
                    : "";

                helpBuilder.AppendLine(
                    $"{Colorize(name, Settings.Help_CommandColor)}{aliasText} " +
                    $"{Colorize($"({list.Count} overload{(list.Count > 1 ? "s" : "")})", Settings.Help_MutedColor)}:"
                );

                foreach (var entry in list)
                {
                    if (entry.Flags.HasFlag(CommandFlags.Hidden))
                        continue;

                    var parameters = entry.MethodInfo.GetParameters();

                    var visibleParams = parameters
                        .Where(p => !IsInternalDelegate(p))
                        .Select(FormatParameter);

                    string args = string.Join(" ", visibleParams);

                    helpBuilder.AppendLine(
                        $"  {Colorize("", Settings.Help_CommandColor)} {args}"
                    );

                    helpBuilder.AppendLine(
                        $"    {Colorize(entry.Description, Settings.Help_MutedColor)}"
                    );
                }

                helpBuilder.AppendLine();
            }

            // =========================
            // GLOBAL HELP
            // =========================
            if (string.IsNullOrEmpty(commandName))
            {
                helpBuilder.AppendLine(Colorize("Available Commands:", Settings.Help_SectionColor));
                helpBuilder.AppendLine();

                foreach (var kv in ConsoleCommandRegistry.Commands.OrderBy(c => c.Key))
                {
                    if (ConsoleCommandRegistry.IsAlias(kv.Key, out _))
                        continue;

                    AppendCommandLine(kv.Key, kv.Value);
                }

                return helpBuilder.ToString();
            }

            // =========================
            // SINGLE COMMAND HELP
            // =========================
            string cmdName = commandName.ToLower();

            if (ConsoleCommandRegistry.IsAlias(cmdName, out var resolved))
            {
                if (resolved == null)
                {
                    helpBuilder.AppendLine(
                        Colorize($"Unknown command: '{cmdName}'", Settings.WarningColor)
                    );

                    return helpBuilder.ToString();
                }

                cmdName = string.IsNullOrWhiteSpace(resolved.Group)
                    ? resolved.CommandName.ToLower()
                    : $"{resolved.Group.ToLower()}.{resolved.CommandName.ToLower()}";
            }

            if (!ConsoleCommandRegistry.Commands.TryGetValue(cmdName, out List<CommandEntry> entryList))
            {
                helpBuilder.AppendLine(
                    Colorize($"Unknown command: '{cmdName}'", Settings.WarningColor)
                );

                return helpBuilder.ToString();
            }

            var entriesSingle = entryList.Distinct().ToList();
            var aliasesSingle = GetAliasesForKey(cmdName);

            string aliasLine = aliasesSingle.Count > 0
                ? $" {Colorize("(aliases: " + string.Join(", ", aliasesSingle) + ")", Settings.Help_MutedColor)}"
                : "";

            helpBuilder.AppendLine(
                $"{Colorize("Command:", Settings.Help_SectionColor)} " +
                $"{Colorize(cmdName, Settings.Help_CommandColor)}{aliasLine}"
            );

            helpBuilder.AppendLine(
                Colorize($"({entriesSingle.Count} overload{(entriesSingle.Count > 1 ? "s" : "")})", Settings.Help_MutedColor)
            );

            helpBuilder.AppendLine();

            foreach (var entry in entriesSingle)
            {
                if (entry.Flags.HasFlag(CommandFlags.Hidden))
                    continue;

                var parameters = entry.MethodInfo.GetParameters();

                var visibleParams = parameters
                    .Where(p => !IsInternalDelegate(p))
                    .Select(FormatParameter);

                string args = string.Join(" ", visibleParams);

                helpBuilder.AppendLine(
                    $"{Colorize("Description:", Settings.Help_MutedColor)} {entry.Description}"
                );

                if (visibleParams.Any())
                {
                    helpBuilder.AppendLine(
                        $"{Colorize("Usage:", Settings.Help_MutedColor)} {entry.CommandName} {args}"
                    );
                }

                helpBuilder.AppendLine();
            }

            return helpBuilder.ToString();
        }

        public static IEnumerable<string> GetAutoCompleteSuggestions(MethodInfo method, int argIndex, string prefix)
        {
            var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
            var parameters = method.GetParameters();

            bool hasCallback = parameters.Length > 0 &&
                (parameters[0].ParameterType == typeof(Action<string>) ||
                 parameters[0].ParameterType == typeof(Action<string, bool>) ||
                 parameters[0].ParameterType == typeof(CommandResponseDelegate));

            if (hasCallback) argIndex++;
            if (argIndex >= parameters.Length) return Array.Empty<string>();

            var paramType = parameters[argIndex].ParameterType;

            if (!string.IsNullOrEmpty(attr?.AutoCompleteProvider))
            {
                if (!SuggestionMethodCache.TryGetValue(method, out MethodInfo providerMethod))
                {
                    providerMethod = method.DeclaringType.GetMethod(
                        attr.AutoCompleteProvider,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                    if (providerMethod != null &&
                        typeof(IEnumerable<string>).IsAssignableFrom(providerMethod.ReturnType))
                    {
                        SuggestionMethodCache[method] = providerMethod;
                    }
                    else
                    {
                        SuggestionMethodCache[method] = null;
                        LogHandler(Colorize($"Could not find static IEnumerable<string> {attr.AutoCompleteProvider}() in {method.DeclaringType.Name}", Settings.WarningColor), false);
                        Debug.LogWarning($"[UniTerminal] Could not find static IEnumerable<string> {attr.AutoCompleteProvider}() in {method.DeclaringType.Name}");
                    }
                }

                if (providerMethod != null)
                {
                    var providerParams = providerMethod.GetParameters();
                    var relativeArgIndex = argIndex - (hasCallback ? 1 : 0);

                    switch (providerParams.Length)
                    {
                        case 2 when providerParams[0].ParameterType == typeof(string) && providerParams[1].ParameterType == typeof(int):
                            return (IEnumerable<string>)providerMethod.Invoke(null, new object[] { prefix, relativeArgIndex });
                        case 1 when providerParams[0].ParameterType == typeof(string):
                            return (IEnumerable<string>)providerMethod.Invoke(null, new object[] { prefix });
                        case 1 when providerParams[0].ParameterType == typeof(int):
                        {
                            var suggestions = (IEnumerable<string>)providerMethod.Invoke(null, new object[] { relativeArgIndex });
                            return (suggestions ?? Array.Empty<string>())
                                .Where(s => s.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                                .OrderByDescending(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                .ThenBy(s => s);
                        }
                        case 0:
                        {
                            var suggestions = (IEnumerable<string>)providerMethod.Invoke(null, null);
                            return (suggestions ?? Array.Empty<string>())
                                .Where(s => s.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                                .OrderByDescending(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                .ThenBy(s => s);
                        }
                        default:
#if DEBUG
                            LogHandler(Colorize($"AutoComplete method '{providerMethod.Name}' has invalid parameters. Expected () or (string).", Settings.WarningColor), false);
#endif

                            Debug.LogWarning($"[UniTerminal] AutoComplete method '{providerMethod.Name}' has invalid parameters. Expected () or (string).");
                            break;
                    }
                }
            }

            if (paramType == typeof(bool))
                return new[] { "true", "false" }
                    .Where(v => v.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(v => v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            if (paramType.IsEnum)
            {
                return Enum.GetNames(paramType)
                      .Where(name => name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                      .OrderByDescending(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                      .ThenBy(name => name);
            }

            return Array.Empty<string>();
        }
    }
}
