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
            AutoCompleteProvider = autoCompleteMethod;
        }

        [Obsolete("Use [ConsoleCommand(name, Description = ..., Flags = ..., AutoCompleteProvider = ...)] instead.")]
        public ConsoleCommandAttribute(string command, string description, CommandFlags flags, string autoCompleteMethod) : this(command, description, flags)
        {
            AutoCompleteProvider = autoCompleteMethod;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class CommandAliasAttribute : Attribute
    {
        public string Alias { get; }
        public CommandAliasAttribute(string alias) => Alias = alias;
    }
}