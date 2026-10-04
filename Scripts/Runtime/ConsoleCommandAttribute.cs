using System;

namespace NoSlimes.Util.UniTerminal
{
    [Flags]
    public enum CommandFlags
    {
        None = 0,
        DebugOnly = 1 << 0,
        EditorOnly = 1 << 1,
        Cheat = 1 << 2,
        Mod = 1 << 3,
        Hidden = 1 << 4
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ConsoleCommandAttribute : Attribute
    {
        public string Name { get; }
        public string Group { get; set; } = "";
        public string Description { get; set; } = "";
        public CommandFlags Flags { get; set; } = CommandFlags.None;
        [Obsolete("Use [Suggest]/[SuggestValues] on parameters instead.")]
        public string AutoCompleteProvider { get; set; } = "";

        public ConsoleCommandAttribute(string name)
        {
            Name = name;
        }


        [Obsolete("Use [ConsoleCommand(name, Description = ...)] instead.")]
        public ConsoleCommandAttribute(string command, string description) : this(command)
        {
            Description = description;
        }


        [Obsolete("Use [ConsoleCommand(name, Description = ..., Flags = ...)] instead.")]
        public ConsoleCommandAttribute(string command, string description, CommandFlags flags) : this(command, description)
        {
            Flags = flags;
        }

        [Obsolete("Use [ConsoleCommand(name, Description = ..., AutoCompleteProvider = ...)] instead.")]
        public ConsoleCommandAttribute(string command, string description, string autoCompleteMethod) : this(command, description)
        {
#pragma warning disable 618
            AutoCompleteProvider = autoCompleteMethod;
#pragma warning restore 618
        }

        [Obsolete("Use [ConsoleCommand(name, Description = ..., Flags = ..., AutoCompleteProvider = ...)] instead.")]
        public ConsoleCommandAttribute(string command, string description, CommandFlags flags, string autoCompleteMethod) : this(command, description, flags)
        {
#pragma warning disable 618
            AutoCompleteProvider = autoCompleteMethod;
#pragma warning restore 618
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class CommandAliasAttribute : Attribute
    {
        public string Alias { get; }
        public CommandAliasAttribute(string alias) => Alias = alias;
    }

    public readonly struct AutoCompleteContext
    {
        public string Prefix { get; }
        public int ArgIndex { get; }
        public string ParamName { get; }
        public System.Collections.Generic.IReadOnlyList<string> TypedArgs { get; }

        public AutoCompleteContext(string prefix, int argIndex, string paramName, System.Collections.Generic.IReadOnlyList<string> typedArgs)
        {
            Prefix = prefix ?? "";
            ArgIndex = argIndex;
            ParamName = paramName ?? "";
            TypedArgs = typedArgs;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class SuggestAttribute : Attribute
    {
        public Type ProviderType { get; }
        public string ProviderMethod { get; }

        public SuggestAttribute(string methodName)
        {
            ProviderMethod = methodName;
        }

        public SuggestAttribute(Type providerType, string methodName = "Suggest")
        {
            ProviderType = providerType;
            ProviderMethod = methodName;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class SuggestValuesAttribute : Attribute
    {
        public string[] Values { get; }
        public SuggestValuesAttribute(params string[] values) => Values = values ?? System.Array.Empty<string>();
    }
}