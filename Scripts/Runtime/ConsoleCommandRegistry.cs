using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static NoSlimes.Util.UniTerminal.ConsoleCommandCache;
using System.Threading.Tasks;
using System.Linq.Expressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NoSlimes.Util.UniTerminal
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal static class ConsoleCommandRegistry
    {
        private static ConsoleCommandCache cache;
        private static readonly HashSet<Assembly> runtimeAssemblies = new();

        private static readonly Dictionary<string, List<CommandEntry>> _commands = new();
        private static readonly Dictionary<string, List<CommandEntry>> _commandsByGroup = new();

        // NEW: alias tracking dictionary
        private static readonly Dictionary<string, CommandEntry> _aliasLookup = new();

        internal static IReadOnlyDictionary<string, List<CommandEntry>> Commands => _commands;
        internal static IReadOnlyDictionary<string, List<CommandEntry>> CommandsBygroup => _commandsByGroup;
        internal static IReadOnlyDictionary<string, CommandEntry> AliasLookup => _aliasLookup;

        internal static event Action<double> OnCacheLoaded;

        public static bool IsAlias(string input, out CommandEntry entry)
        {
            return _aliasLookup.TryGetValue(input.ToLowerInvariant(), out entry);
        }

#if UNITY_EDITOR
        static ConsoleCommandRegistry()
        {
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReload;
        }

        private static void AfterAssemblyReload()
        {
            if (UniTerminalSettings.instance != null && UniTerminalSettings.instance.IsAutoRebuildEnabled && EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += () => DiscoverCommandsEditor();
            }
        }

        [MenuItem("Tools/UniTerminal/Manual Build Command Cache")]
        internal static async void DiscoverCommandsEditor()
        {
            int taskId = Progress.Start("UniTerminal", "Building Command Cache...");
            try
            {
                await DiscoverCommandsAsync(
                    AppDomain.CurrentDomain.GetAssemblies(),
                    true,
                    (progress, message) => Progress.Report(taskId, progress, message)
                );
            }
            finally
            {
                Progress.Finish(taskId);
            }
        }
#endif

        private static void RegisterEntryInDictionary(CommandEntry entry)
        {
            string primaryKey = BuildKey(entry.Group, entry.CommandName);
            AddKeyToDictionary(primaryKey, entry);

            if (entry.Aliases != null)
            {
                foreach (var alias in entry.Aliases)
                {
                    if (string.IsNullOrWhiteSpace(alias)) continue;

                    string aliasKey = BuildKey(entry.Group, alias);

                    AddKeyToDictionary(aliasKey, entry);

                    if (!_aliasLookup.ContainsKey(aliasKey))
                        _aliasLookup[aliasKey] = entry;
                }
            }

            AddToGroupDictionary(entry);
        }

        private static void AddKeyToDictionary(string key, CommandEntry entry)
        {
            if (!_commands.TryGetValue(key, out var list))
            {
                list = new List<CommandEntry>();
                _commands[key] = list;
            }

            if (!list.Contains(entry))
            {
                list.Add(entry);
            }
        }

        private static void AddToGroupDictionary(CommandEntry entry)
        {
            string groupKey = string.IsNullOrWhiteSpace(entry.Group)
                ? string.Empty
                : entry.Group.ToLowerInvariant();

            if (!_commandsByGroup.TryGetValue(groupKey, out var list))
            {
                list = new List<CommandEntry>();
                _commandsByGroup[groupKey] = list;
            }

            if (!list.Contains(entry))
            {
                list.Add(entry);
            }
        }

        private static string BuildKey(string group, string name)
        {
            return string.IsNullOrWhiteSpace(group)
                ? name.ToLowerInvariant()
                : $"{group.ToLowerInvariant()}.{name.ToLowerInvariant()}";
        }

        internal static void DiscoverCommands(IEnumerable<Assembly> assemblies = null, bool overwrite = true)
        {
            if (overwrite)
            {
                _commands.Clear();
                _aliasLookup.Clear();
            }

            assemblies ??= AppDomain.CurrentDomain.GetAssemblies();
            var validCommands = new List<CommandEntry>();

            foreach (var assembly in assemblies)
            {
                foreach (var type in GetSafeTypes(assembly))
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
                        if (attr == null) continue;
                        if (!method.IsStatic && !type.IsSubclassOf(typeof(UnityEngine.Object))) continue;

                        var aliasAttrs = method.GetCustomAttributes<CommandAliasAttribute>();

                        var entry = new CommandEntry
                        {
                            CommandName = attr.Name,
                            Group = attr.Group,
                            Description = attr.Description,
                            Flags = attr.Flags,
                            AutoCompleteProvider = attr.AutoCompleteProvider,
                            Aliases = aliasAttrs.Select(a => a.Alias).ToArray(),
                            DeclaringTypeName = type.AssemblyQualifiedName,
                            MethodName = method.Name,
                            ParameterTypes = method.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName).ToArray(),
                            MethodInfo = method,
                            IsStatic = method.IsStatic,
                            DeclaringType = type
                        };

                        validCommands.Add(entry);
                        RegisterEntryInDictionary(entry);
                    }
                }
            }

#if UNITY_EDITOR
            UpdateCacheEditor(validCommands);
#endif
        }

        internal static async Task DiscoverCommandsAsync(IEnumerable<Assembly> assemblies = null, bool overwrite = true, Action<float, string> onProgress = null)
        {
            var assemblyList = (assemblies ?? AppDomain.CurrentDomain.GetAssemblies()).ToArray();
            int total = assemblyList.Length;

            var results = await Task.Run(() =>
            {
                var valid = new List<CommandEntry>();

                for (int i = 0; i < total; i++)
                {
                    var assembly = assemblyList[i];
                    float progress = (float)i / total;
                    onProgress?.Invoke(progress, $"Scanning {assembly.GetName().Name}...");

                    foreach (var type in GetSafeTypes(assembly))
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                        {
                            var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
                            if (attr == null) continue;
                            if (!method.IsStatic && !type.IsSubclassOf(typeof(UnityEngine.Object))) continue;

                            var aliasAttrs = method.GetCustomAttributes<CommandAliasAttribute>();

                            var entry = new CommandEntry
                            {
                                CommandName = attr.Name,
                                Group = attr.Group,
                                Description = attr.Description,
                                Flags = attr.Flags,
                                AutoCompleteProvider = attr.AutoCompleteProvider,
                                Aliases = aliasAttrs.Select(a => a.Alias).ToArray(),
                                DeclaringTypeName = type.AssemblyQualifiedName,
                                MethodName = method.Name,
                                ParameterTypes = method.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName).ToArray(),
                                MethodInfo = method,
                                IsStatic = method.IsStatic,
                                DeclaringType = type
                            };

                            valid.Add(entry);
                        }
                    }
                }

                return valid;
            });

            if (overwrite)
            {
                _commands.Clear();
                _aliasLookup.Clear();
            }

            onProgress?.Invoke(0.95f, "Updating Command Dictionary...");

            foreach (var entry in results)
            {
                RegisterEntryInDictionary(entry);
            }

#if UNITY_EDITOR
            onProgress?.Invoke(0.99f, "Saving to Disk...");
            UpdateCacheEditor(results);
#endif
        }

#if UNITY_EDITOR
        private static void UpdateCacheEditor(List<CommandEntry> entries)
        {
            cache = Resources.Load<ConsoleCommandCache>("UniTerminal/UniTerminalCommandCache");
            if (cache == null)
            {
                string folderPath = "Assets/Resources/UniTerminal";
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder(folderPath))
                    AssetDatabase.CreateFolder("Assets/Resources", "UniTerminal");

                cache = ScriptableObject.CreateInstance<ConsoleCommandCache>();
                AssetDatabase.CreateAsset(cache, folderPath + "/UniTerminalCommandCache.asset");
            }

            cache.Commands = entries.ToArray();

            EditorUtility.SetDirty(cache);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UniTerminal] Built command cache with {cache.Commands.Length} entries.");
        }
#endif

        internal static void DiscoverCommandsInAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (!runtimeAssemblies.Contains(assembly)) runtimeAssemblies.Add(assembly);
            DiscoverCommands(new[] { assembly }, false);
        }

        public static void LoadCache()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            cache = Resources.Load<ConsoleCommandCache>("UniTerminal/UniTerminalCommandCache");
            if (cache == null)
            {
                Debug.LogError("UniTerminalCommandCache asset not found at 'Resources/UniTerminal/UniTerminalCommandCache'");
                return;
            }

            _commands.Clear();
            _commandsByGroup.Clear();
            _aliasLookup.Clear();

            foreach (var entry in cache.Commands)
            {
                var type = Type.GetType(entry.DeclaringTypeName);
                if (type == null) continue;

                var paramTypes = entry.ParameterTypes.Select(Type.GetType).ToArray();
                if (paramTypes.Any(t => t == null)) continue;

                var method = type.GetMethod(entry.MethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                    null, paramTypes, null);

                if (method == null) continue;
                if (!FilterCommand(entry)) continue;

                entry.MethodInfo = method;
                entry.IsStatic = method.IsStatic;
                entry.DeclaringType = type;

                if (method.IsStatic)
                {
                    try
                    {
                        entry.Delegate = Delegate.CreateDelegate(Expression.GetActionType(paramTypes), method);
                    }
                    catch { }
                }

                RegisterEntryInDictionary(entry);
            }

            if (runtimeAssemblies.Count > 0)
            {
                DiscoverCommands(runtimeAssemblies, false);
            }

            stopwatch.Stop();
            OnCacheLoaded?.Invoke(stopwatch.Elapsed.TotalMilliseconds);
        }

        private static IEnumerable<Type> GetSafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        private static bool FilterCommand(CommandEntry entry)
        {
            if (entry == null)
                return false;

            if (entry.Flags.HasFlag(CommandFlags.DebugOnly) && !Debug.isDebugBuild)
                return false;

            if (entry.Flags.HasFlag(CommandFlags.EditorOnly) && !Application.isEditor)
                return false;

            return true;
        }
    }
}