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
            static void callback()
            {
                DiscoverCommandsEditor();
                EditorApplication.delayCall -= callback;
            }

            if (UniTerminalSettings.instance.IsAutoRebuildEnabled && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += callback;
        }

        [MenuItem("Tools/UniTerminal/Manual Build Command Cache")]
        internal static void DiscoverCommandsEditor()
        {
            int taskId = Progress.Start("UniTerminal", "Building Command Cache...");
            try
            {
                // Editor: TypeCache is indexed at compile time, so this is
                // O(commands) instead of O(all methods in all assemblies).
                DiscoverCommandsWithTypeCache(
                    true,
                    (progress, message) => Progress.Report(taskId, progress, message)
                );
            }
            finally
            {
                Progress.Finish(taskId);
            }
        }

        /// Editor-only TypeCache fast path. Runtime mods must use reflection.
        /// <see cref="DiscoverCommands"/> covers builds and dynamic assemblies.
        internal static void DiscoverCommandsWithTypeCache(bool overwrite = true, Action<float, string> onProgress = null)
        {
            if (overwrite)
            {
                _commands.Clear();
                _commandsByGroup.Clear();
                _aliasLookup.Clear();
            }

            onProgress?.Invoke(0f, "Querying TypeCache...");
            var methods = TypeCache.GetMethodsWithAttribute<ConsoleCommandAttribute>();

            var validCommands = new List<CommandEntry>(methods.Count);
            for (int i = 0; i < methods.Count; i++)
            {
                var entry = CreateCommandEntry(methods[i]);
                if (entry == null) continue;
                validCommands.Add(entry);
                RegisterEntryInDictionary(entry);
            }

            onProgress?.Invoke(0.99f, "Saving to Disk...");
            UpdateCacheEditor(validCommands);
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

        private static CommandEntry CreateCommandEntry(MethodInfo method)
        {
            if (method == null) return null;

            var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
            if (attr == null) return null;

            var type = method.DeclaringType;
            if (type == null) return null;
            if (!method.IsStatic && !type.IsSubclassOf(typeof(UnityEngine.Object))) return null;

            var paramInfos = method.GetParameters();
            var suggests = new ParamSuggest[paramInfos.Length];
            for (int i = 0; i < paramInfos.Length; i++)
            {
                var valuesAttr = paramInfos[i].GetCustomAttribute<SuggestValuesAttribute>();
                if (valuesAttr != null)
                {
                    suggests[i] = new ParamSuggest { Values = (string[])valuesAttr.Values.Clone() };
                    continue;
                }

                var suggestAttr = paramInfos[i].GetCustomAttribute<SuggestAttribute>();
                if (suggestAttr != null)
                {
                    suggests[i] = new ParamSuggest
                    {
                        ProviderTypeName = suggestAttr.ProviderType != null ? suggestAttr.ProviderType.AssemblyQualifiedName : null,
                        ProviderMethod = suggestAttr.ProviderMethod
                    };
                }
            }

            var entry = new CommandEntry
            {
                CommandName = attr.Name,
                Group = attr.Group,
                Description = attr.Description,
                Flags = attr.Flags,
#pragma warning disable 618
                AutoCompleteProvider = attr.AutoCompleteProvider,
#pragma warning restore 618
                Aliases = method.GetCustomAttributes<CommandAliasAttribute>().Select(a => a.Alias).ToArray(),
                DeclaringTypeName = type.AssemblyQualifiedName,
                MethodName = method.Name,
                ParameterTypes = paramInfos.Select(p => p.ParameterType.AssemblyQualifiedName).ToArray(),
                ParamSuggests = suggests,
                MethodInfo = method,
                IsStatic = method.IsStatic,
                DeclaringType = type
            };
            EnsureSuggestResolvers(entry, null);
            return entry;
        }

        private static readonly Dictionary<string, MethodInfo> providerMethodCache = new(StringComparer.Ordinal);

        internal static void EnsureSuggestResolvers(CommandEntry entry, Dictionary<string, Type> typeCache)
        {
            if (entry?.ParamSuggests == null)
                return;
            if (entry.SuggestResolvers != null && entry.SuggestResolvers.Length == entry.ParamSuggests.Length)
                return;

            var resolvers = new Func<AutoCompleteContext, IEnumerable<string>>[entry.ParamSuggests.Length];
            for (int i = 0; i < entry.ParamSuggests.Length; i++)
            {
                var suggest = entry.ParamSuggests[i];
                if (IsEmptySuggest(suggest))
                    continue;

                if (suggest.Values != null && suggest.Values.Length > 0)
                {
                    string[] copy = (string[])suggest.Values.Clone();
                    resolvers[i] = _ => copy;
                    continue;
                }

                if (string.IsNullOrEmpty(suggest.ProviderMethod))
                    continue;

                Type providerType = string.IsNullOrEmpty(suggest.ProviderTypeName)
                    ? entry.DeclaringType
                    : ResolveType(suggest.ProviderTypeName, typeCache);
                if (providerType == null)
                    continue;

                string cacheKey = providerType.AssemblyQualifiedName + "::" + suggest.ProviderMethod;
                if (!providerMethodCache.TryGetValue(cacheKey, out var provider))
                {
                    provider = providerType.GetMethod(suggest.ProviderMethod,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (provider == null || !typeof(System.Collections.IEnumerable).IsAssignableFrom(provider.ReturnType))
                    {
                        providerMethodCache[cacheKey] = null;
                        continue;
                    }
                    var providerParams = provider.GetParameters();
                    bool valid = providerParams.Length == 0
                        || (providerParams.Length == 1 && providerParams[0].ParameterType == typeof(AutoCompleteContext));
                    if (!valid)
                    {
                        providerMethodCache[cacheKey] = null;
                        continue;
                    }
                    providerMethodCache[cacheKey] = provider;
                }

                if (provider == null)
                    continue;

                var captured = provider;
                var capturedParams = captured.GetParameters();
                if (capturedParams.Length == 0)
                    resolvers[i] = _ => (IEnumerable<string>)captured.Invoke(null, null);
                else
                    resolvers[i] = ctx => (IEnumerable<string>)captured.Invoke(null, new object[] { ctx });
            }
            entry.SuggestResolvers = resolvers;
        }

        internal static void DiscoverCommands(IEnumerable<Assembly> assemblies = null, bool overwrite = true, bool applyFilter = true)
        {
            if (overwrite)
            {
                _commands.Clear();
                _commandsByGroup.Clear();
                _aliasLookup.Clear();
            }

            assemblies ??= GetLoadedAssemblies();
            if (applyFilter)
                assemblies = GetScannableAssemblies(assemblies);
            var validCommands = new List<CommandEntry>();

            foreach (var assembly in assemblies)
            {
                foreach (var type in GetSafeTypes(assembly))
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        var entry = CreateCommandEntry(method);
                        if (entry == null) continue;

                        validCommands.Add(entry);
                        RegisterEntryInDictionary(entry);
                    }
                }
            }

#if UNITY_EDITOR
            UpdateCacheEditor(validCommands);
#endif
        }

        // Runtime/reflection fallback for explicit assembly scans and mods.
        // Editor full rebuilds should use DiscoverCommandsWithTypeCache instead.

        internal static async Task DiscoverCommandsAsync(IEnumerable<Assembly> assemblies = null, bool overwrite = true, Action<float, string> onProgress = null)
        {
            var assemblyList = GetScannableAssemblies(assemblies ?? GetLoadedAssemblies()).ToArray();
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
                            var entry = CreateCommandEntry(method);
                            if (entry == null) continue;
                            valid.Add(entry);
                        }
                    }
                }

                return valid;
            });

            if (overwrite)
            {
                _commands.Clear();
                _commandsByGroup.Clear();
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
        private const string CacheAssetPath = "Assets/Resources/UniTerminal/UniTerminalCommandCache.asset";

        private static void UpdateCacheEditor(List<CommandEntry> entries)
        {
            entries.Sort((a, b) => string.CompareOrdinal(EntryIdentity(a), EntryIdentity(b)));
            ValidateAutoCompleteProviders(entries);

            cache = AssetDatabase.LoadAssetAtPath<ConsoleCommandCache>(CacheAssetPath);
            if (cache == null)
            {
                const string folderPath = "Assets/Resources/UniTerminal";
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder(folderPath))
                    AssetDatabase.CreateFolder("Assets/Resources", "UniTerminal");

                cache = ScriptableObject.CreateInstance<ConsoleCommandCache>();
                AssetDatabase.CreateAsset(cache, CacheAssetPath);
            }

            var oldEntries = cache.Commands ?? Array.Empty<CommandEntry>();
            if (CommandsEqual(oldEntries, entries))
                return;

            bool detailed = UniTerminalSettings.instance != null && UniTerminalSettings.instance.IsDetailedLoggingEnabled;
            if (detailed)
                LogCommandDiff(oldEntries, entries);
            else
                Debug.Log($"[UniTerminal] Built command cache with {entries.Count} entries.");

            cache.Commands = entries.ToArray();
            EditorUtility.SetDirty(cache);
            AssetDatabase.SaveAssets();
        }

        private static string EntryIdentity(CommandEntry e)
        {
            string paramKey = e.ParameterTypes != null ? string.Join(",", e.ParameterTypes) : string.Empty;
            return $"{e.DeclaringTypeName}::{e.MethodName}({paramKey})";
        }

        private static bool EntryPayloadEqual(CommandEntry a, CommandEntry b)
        {
            if (a.CommandName != b.CommandName
                || a.Group != b.Group
                || a.Description != b.Description
                || a.Flags != b.Flags
#pragma warning disable 618
                || a.AutoCompleteProvider != b.AutoCompleteProvider
#pragma warning restore 618
                || a.DeclaringTypeName != b.DeclaringTypeName
                || a.MethodName != b.MethodName
                || !(a.Aliases ?? Array.Empty<string>()).SequenceEqual(b.Aliases ?? Array.Empty<string>())
                || !(a.ParameterTypes ?? Array.Empty<string>()).SequenceEqual(b.ParameterTypes ?? Array.Empty<string>()))
                return false;

            var sa = a.ParamSuggests ?? Array.Empty<ParamSuggest>();
            var sb = b.ParamSuggests ?? Array.Empty<ParamSuggest>();
            if (sa.Length != sb.Length)
                return false;
            for (int i = 0; i < sa.Length; i++)
            {
                if (!SuggestEqual(sa[i], sb[i]))
                    return false;
            }
            return true;
        }

        private static bool CommandsEqual(IReadOnlyList<CommandEntry> a, IReadOnlyList<CommandEntry> b)
        {
            if (a.Count != b.Count)
                return false;
            var sortedA = a.OrderBy(EntryIdentity).ToArray();
            var sortedB = b.OrderBy(EntryIdentity).ToArray();
            for (int i = 0; i < sortedA.Length; i++)
            {
                if (EntryIdentity(sortedA[i]) != EntryIdentity(sortedB[i]) || !EntryPayloadEqual(sortedA[i], sortedB[i]))
                    return false;
            }
            return true;
        }

        private static void LogCommandDiff(IReadOnlyList<CommandEntry> oldEntries, IReadOnlyList<CommandEntry> newEntries)
        {
            var oldById = oldEntries.ToDictionary(EntryIdentity, e => e);
            var newById = newEntries.ToDictionary(EntryIdentity, e => e);
            var added = newById.Keys.Except(oldById.Keys).ToArray();
            var removed = oldById.Keys.Except(newById.Keys).ToArray();
            var modified = newById.Keys.Intersect(oldById.Keys).Where(id => !EntryPayloadEqual(oldById[id], newById[id])).ToArray();
            Debug.Log($"[UniTerminal] Cache: +{added.Length} -{removed.Length} ~{modified.Length} ({newEntries.Count} total).");
            foreach (var id in added) Debug.Log($"[UniTerminal] Added: {newById[id].Group}.{newById[id].CommandName} ({id})");
            foreach (var id in removed) Debug.Log($"[UniTerminal] Removed: {oldById[id].Group}.{oldById[id].CommandName} ({id})");
            foreach (var id in modified) Debug.Log($"[UniTerminal] Modified: {newById[id].Group}.{newById[id].CommandName} ({id})");
        }

        private static void ValidateAutoCompleteProviders(List<CommandEntry> entries)
        {
            foreach (var entry in entries)
            {
#pragma warning disable 618
                string obsolete = entry.AutoCompleteProvider;
#pragma warning restore 618
                if (!string.IsNullOrEmpty(obsolete))
                {
                    var type = entry.DeclaringType;
                    var provider = type?.GetMethod(obsolete,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (provider == null || !typeof(System.Collections.IEnumerable).IsAssignableFrom(provider.ReturnType))
                        Debug.LogWarning($"[UniTerminal] '{entry.CommandName}' obsolete provider '{obsolete}' not found on {type?.Name}. Use [Suggest]/[SuggestValues].");
                }

                if (entry.ParamSuggests == null)
                    continue;
                for (int i = 0; i < entry.ParamSuggests.Length; i++)
                {
                    var suggest = entry.ParamSuggests[i];
                    if (suggest == null)
                        continue;
                    if (suggest.Values != null)
                    {
                        if (suggest.Values.Length == 0)
                            Debug.LogError($"[UniTerminal] '{entry.CommandName}' param {i}: [SuggestValues] is empty.");
                        continue;
                    }
                    Type providerType = string.IsNullOrEmpty(suggest.ProviderTypeName)
                        ? entry.DeclaringType
                        : Type.GetType(suggest.ProviderTypeName, false);
                    if (providerType == null)
                    {
                        Debug.LogError($"[UniTerminal] '{entry.CommandName}' param {i}: provider type '{suggest.ProviderTypeName}' not found.");
                        continue;
                    }
                    var method = providerType.GetMethod(suggest.ProviderMethod,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null || !typeof(System.Collections.IEnumerable).IsAssignableFrom(method.ReturnType))
                    {
                        Debug.LogError($"[UniTerminal] '{entry.CommandName}' param {i}: provider '{suggest.ProviderMethod}' not found on {providerType.Name}.");
                        continue;
                    }
                    var ps = method.GetParameters();
                    bool valid = ps.Length == 0
                        || (ps.Length == 1 && ps[0].ParameterType == typeof(AutoCompleteContext));
                    if (!valid)
                        Debug.LogError($"[UniTerminal] '{entry.CommandName}' param {i}: provider '{suggest.ProviderMethod}' must be () or (AutoCompleteContext).");
                }
            }
        }
#endif

        internal static void DiscoverCommandsInAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (!runtimeAssemblies.Contains(assembly)) runtimeAssemblies.Add(assembly);
            DiscoverCommands(new[] { assembly }, false, false);
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

            var typeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var entry in cache.Commands)
            {
                if (entry == null)
                    continue;

                var type = ResolveType(entry.DeclaringTypeName, typeCache);
                if (type == null) continue;

                var paramNames = entry.ParameterTypes ?? Array.Empty<string>();
                var paramTypes = new Type[paramNames.Length];
                bool failed = false;
                for (int i = 0; i < paramNames.Length; i++)
                {
                    paramTypes[i] = ResolveType(paramNames[i], typeCache);
                    if (paramTypes[i] == null) { failed = true; break; }
                }
                if (failed) continue;

                var method = type.GetMethod(entry.MethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                    null, paramTypes, null);

                if (method == null) continue;
                if (!FilterCommand(entry)) continue;

                entry.MethodInfo = method;
                entry.IsStatic = method.IsStatic;
                entry.DeclaringType = type;
                EnsureSuggestResolvers(entry, typeCache);

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
                DiscoverCommands(runtimeAssemblies, false, false);
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

        private static readonly string UniTerminalAssemblyName =
            typeof(ConsoleCommandAttribute).Assembly.GetName().Name;

        private static bool ShouldScanAssembly(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic)
                return false;

            // The assembly that defines [ConsoleCommand] always gets scanned.
            if (assembly == typeof(ConsoleCommandAttribute).Assembly)
                return true;

            string name = assembly.GetName().Name;

            // Fast path: these can never contain commands, don't even
            // pay for GetReferencedAssemblies() on them.
            if (name.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                name.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                name.StartsWith("Unity.", StringComparison.Ordinal) ||
                name.StartsWith("System.", StringComparison.Ordinal) ||
                name.StartsWith("Mono.", StringComparison.Ordinal) ||
                name.StartsWith("nunit.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("NUnit", StringComparison.Ordinal) ||
                string.Equals(name, "mscorlib", StringComparison.Ordinal) ||
                string.Equals(name, "netstandard", StringComparison.Ordinal) ||
                string.Equals(name, "System", StringComparison.Ordinal))
                return false;

            // Only scan assemblies that could actually see [ConsoleCommand]:
            // i.e. they reference NoSlimes.UniTerminal.Runtime.
            try
            {
                return assembly.GetReferencedAssemblies()
                    .Any(r => string.Equals(r.Name, UniTerminalAssemblyName, StringComparison.Ordinal));
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<Assembly> GetScannableAssemblies(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                if (ShouldScanAssembly(assembly))
                    yield return assembly;
            }
        }

        private static Type ResolveType(string typeName, Dictionary<string, Type> typeCache)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;
            if (typeCache != null && typeCache.TryGetValue(typeName, out var cached))
                return cached;

            var resolved = Type.GetType(typeName, false);
            if (resolved == null)
            {
                int comma = typeName.IndexOf(',');
                if (comma > 0)
                {
                    string fullName = typeName.Substring(0, comma).Trim();
                    string assemblyShort = typeName.Substring(comma + 1).Split(',')[0].Trim();
                    var loaded = GetLoadedAssemblies();
                    foreach (var asm in loaded)
                    {
                        try
                        {
                            if (!string.Equals(asm.GetName().Name, assemblyShort, StringComparison.Ordinal))
                                continue;
                            resolved = asm.GetType(fullName, false);
                            if (resolved != null) break;
                        }
                        catch { }
                    }
                    if (resolved == null)
                    {
                        foreach (var asm in loaded)
                        {
                            try
                            {
                                resolved = asm.GetType(fullName, false);
                                if (resolved != null) break;
                            }
                            catch { }
                        }
                    }
                }
            }

            if (typeCache != null)
                typeCache[typeName] = resolved;
            return resolved;
        }

        private static Assembly[] GetLoadedAssemblies()
        {
            // No Unity runtime equivalent for name-based type fallback; load-time
            // only, per-assembly guarded, results cached in the caller's dict.
#pragma warning disable UAC0005
            return AppDomain.CurrentDomain.GetAssemblies();
#pragma warning restore UAC0005
        }

        private static bool IsEmptySuggest(ParamSuggest s)
        {
            return s == null
                || ((s.Values == null || s.Values.Length == 0)
                    && string.IsNullOrEmpty(s.ProviderMethod)
                    && string.IsNullOrEmpty(s.ProviderTypeName));
        }

        private static bool SuggestEqual(ParamSuggest a, ParamSuggest b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (IsEmptySuggest(a) && IsEmptySuggest(b)) return true;
            if (a == null || b == null) return false;
            return a.ProviderMethod == b.ProviderMethod
                && a.ProviderTypeName == b.ProviderTypeName
                && ((a.Values ?? Array.Empty<string>()).SequenceEqual(b.Values ?? Array.Empty<string>()));
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