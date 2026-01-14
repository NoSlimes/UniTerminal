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
        internal static IReadOnlyDictionary<string, List<CommandEntry>> Commands => _commands;
        internal static event Action<double> OnCacheLoaded;

#if UNITY_EDITOR
        static ConsoleCommandRegistry()
        {
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReload;
        }

        private static void AfterAssemblyReload()
        {
            static void callback()
            {
                DiscoverCommandsEditor();
                EditorApplication.delayCall -= callback;
            }

            if (UniTerminalSettings.instance.IsAutoRebuildEnabled)
                EditorApplication.delayCall += callback;
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

        internal static void DiscoverCommands(IEnumerable<Assembly> assemblies = null, bool overwrite = true)
        {
            if (overwrite)
                _commands.Clear();

            assemblies ??= AppDomain.CurrentDomain.GetAssemblies();
            var validCommands = new List<CommandEntry>();

            foreach (var assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var type in types)
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
                        if (attr == null) continue;
                        if (!method.IsStatic && !type.IsSubclassOf(typeof(UnityEngine.Object))) continue;

                        var entry = new CommandEntry
                        {
                            CommandName = attr.Command,
                            Description = attr.Description,
                            Flags = attr.Flags,
                            DeclaringTypeName = type.AssemblyQualifiedName,
                            MethodName = method.Name,
                            ParameterTypes = method.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName).ToArray(),
                            MethodInfo = method,
                            IsStatic = method.IsStatic,
                            DeclaringType = type
                        };

                        validCommands.Add(entry);

                        string key = entry.CommandName.ToLower();
                        if (!_commands.TryGetValue(key, out var list))
                        {
                            list = new List<CommandEntry>();
                            _commands[key] = list;
                        }
                        list.Add(entry);
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

                    Type[] types;
                    try { types = assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                    foreach (var type in types)
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                        {
                            var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
                            if (attr == null) continue;
                            if (!method.IsStatic && !type.IsSubclassOf(typeof(UnityEngine.Object))) continue;

                            var entry = new CommandEntry
                            {
                                CommandName = attr.Command,
                                Description = attr.Description,
                                Flags = attr.Flags,
                                DeclaringTypeName = type.AssemblyQualifiedName,
                                MethodName = method.Name,
                                ParameterTypes = method.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName).ToArray(),
                                MethodInfo = method,
                                IsStatic = method.IsStatic,
                                DeclaringType = type
                            };

                            onProgress?.Invoke(progress, $"Found command: {entry.CommandName}");
                            valid.Add(entry);
                        }
                    }
                }

                return valid;
            });

            if (overwrite)
                _commands.Clear();

            onProgress?.Invoke(0.95f, "Updating Command Dictionary...");

            foreach (var entry in results)
            {
                string key = entry.CommandName.ToLower();
                if (!_commands.TryGetValue(key, out var list))
                {
                    list = new List<CommandEntry>();
                    _commands[key] = list;
                }
                list.Add(entry);
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
                throw new InvalidOperationException("UniTerminalCommandCache asset not found at 'Resources/UniTerminal/UniTerminalCommandCache'");

            _commands.Clear();

            foreach (var entry in cache.Commands)
            {
                var type = Type.GetType(entry.DeclaringTypeName);
                if (type == null)
                {
                    Debug.LogWarning($"Type '{entry.DeclaringTypeName}' not found.");
                    continue;
                }

                var paramTypes = entry.ParameterTypes.Select(Type.GetType).ToArray();
                if (paramTypes.Any(t => t == null))
                {
                    Debug.LogWarning($"Parameter type missing for '{entry.CommandName}'.");
                    continue;
                }

                var method = type.GetMethod(entry.MethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                    null, paramTypes, null);

                if (method == null)
                {
                    Debug.LogWarning($"Method not found: {type.FullName}.{entry.MethodName}");
                    continue;
                }

                if (!FilterCommand(entry))
                    continue;

                entry.MethodInfo = method;
                entry.IsStatic = method.IsStatic;
                entry.DeclaringType = type;

                if (method.IsStatic)
                    entry.Delegate = Delegate.CreateDelegate(Expression.GetActionType(paramTypes), method);

                string key = entry.CommandName.ToLower();
                if (!_commands.TryGetValue(key, out var list))
                {
                    list = new List<CommandEntry>();
                    _commands[key] = list;
                }
                list.Add(entry);
            }

            if (runtimeAssemblies.Count > 0)
            {
                Debug.Log($"[UniTerminal] Discovering commands in {runtimeAssemblies.Count} runtime assemblies.");
                DiscoverCommands(runtimeAssemblies, false);
            }

            stopwatch.Stop();
            OnCacheLoaded?.Invoke(stopwatch.Elapsed.TotalMilliseconds);
        }

        private static bool FilterCommand(CommandEntry entry)
        {
            if (entry == null)
                return false;

            if (entry.Flags.HasFlag(CommandFlags.DebugOnly) && !Debug.isDebugBuild)
                return false;

            return !entry.Flags.HasFlag(CommandFlags.EditorOnly) || Application.isEditor;
        }
    }
}
