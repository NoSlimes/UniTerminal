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
            public string Group;

            public string[] Aliases;
            public string Description;
            public CommandFlags Flags;
            
            public string AutoCompleteProvider;

            // Binding info
            public string DeclaringTypeName;
            public string MethodName;
            public string[] ParameterTypes;

            // Per-parameter suggest metadata, parallel to the method's
            // parameters (including the response-callback slot, which is null).
            public ParamSuggest[] ParamSuggests;

            // Runtime only
            [NonSerialized] public MethodInfo MethodInfo; // For reflection invocation - this is slower than Delegate
            [NonSerialized] public Delegate Delegate; // For faster invocation 
            [NonSerialized] public bool IsStatic;
            [NonSerialized] public Type DeclaringType;
            [NonSerialized] public Func<AutoCompleteContext, System.Collections.Generic.IEnumerable<string>>[] SuggestResolvers;
        }
        [Serializable]
        public class ParamSuggest
        {
            public string[] Values;
            public string ProviderTypeName;
            public string ProviderMethod;
        }
    }
}