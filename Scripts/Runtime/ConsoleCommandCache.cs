using System;
using System.Reflection;
using UnityEngine;

namespace NoSlimes.Util.UniTerminal
{
    public class ConsoleCommandCache : ScriptableObject
    {
        public CommandEntry[] Commands;

        [Serializable]
        public class CommandEntry
        {
            public string CommandName;
            public string Description;
            public CommandFlags Flags;

            // Binding info
            public string DeclaringTypeName;
            public string MethodName;
            public string[] ParameterTypes;

            // Runtime only
            [NonSerialized] public MethodInfo MethodInfo; // For reflection invocation - this is slower than Delegate
            [NonSerialized] public Delegate Delegate; // For faster invocation 
            [NonSerialized] public bool IsStatic;
            [NonSerialized] public Type DeclaringType;
        }
    }
}