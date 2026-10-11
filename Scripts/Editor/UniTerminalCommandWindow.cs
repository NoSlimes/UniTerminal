using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using static NoSlimes.Util.UniTerminal.ConsoleCommandCache;

namespace NoSlimes.Util.UniTerminal.Editor
{
    internal class UniTerminalCommandWindow : EditorWindow
    {
        private string search = "";
        private string input = "";
        private bool showBrowser;
        private bool showOnlyEditorAllowed = true;
        private bool showHidden;
        private Vector2 browserScroll;
        private Vector2 logScroll;
        private bool scrollToBottom;
        private readonly List<LogLine> logLines = new();
        private readonly List<string> history = new();
        private int historyIndex = -1;
        private string selectedCommandKey = "";
        private const int MaxLogLines = 300;

        private string cachedLogText = "";
        private int cachedLogVersion = -1;
        private int logVersion;

        private Rect logRect;
        private Rect hintRect;
        private Rect searchRect;

        private List<string> tabCandidates = new();
        private int tabIndex = -1;
        private string preTabInput;
        private bool lastKeyWasTab;
        // Deferred caret snap once focus lands.
        private bool snapCaret;
        private bool refocusInput;

        private GUIStyle richLabel;
        private GUIStyle hintStyle;
        private bool stylesInitialized;

        private static readonly Regex TokenizerRegex = new(@"[""].+?[""]|[^ ]+", RegexOptions.Compiled);

        [MenuItem("Tools/UniTerminal/Command Window")]
        public static void ShowWindow()
        {
            UniTerminalCommandWindow window = GetWindow<UniTerminalCommandWindow>("UniTerminal");
            window.minSize = new Vector2(280, 200);
            window.Show();
            window.refocusInput = true;
        }

        private void OnEnable()
        {
            EnsureCommandsLoaded();
            ConsoleCommandInvoker.LogHandler += HandleLog;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            ConsoleCommandInvoker.LogHandler -= HandleLog;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnFocus()
        {
            refocusInput = true;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            EnsureCommandsLoaded();
            Repaint();
        }

        private static void EnsureCommandsLoaded()
        {
            if (ConsoleCommandRegistry.Commands.Count == 0)
                ConsoleCommandRegistry.DiscoverCommandsWithTypeCache(true);
        }

        private void HandleLog(string message, bool success)
        {
            logLines.Add(new LogLine(message, success));
            if (logLines.Count > MaxLogLines)
                logLines.RemoveRange(0, logLines.Count - MaxLogLines);
            logVersion++;
            scrollToBottom = true;
            Repaint();
        }

        private void OnGUI()
        {
            InitStyles();

            if (refocusInput && Event.current.type == EventType.Repaint)
            {
                if (GUI.GetNameOfFocusedControl() == "UniTerminalCommandSearch")
                    refocusInput = false;
                else
                {
                    GUI.FocusControl("UniTerminalCommandInput");
                    refocusInput = false;
                }
            }

            DrawToolbar();
            if (showBrowser)
                DrawBrowser();
            DrawLog();
            DrawHint();
            DrawInput();
            FocusInputOnBackgroundClick();
        }

        private void FocusInputOnBackgroundClick()
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
                return;
            Vector2 pos = Event.current.mousePosition;
            if (logRect.Contains(pos) || hintRect.Contains(pos) || searchRect.Contains(pos))
                return;
            GUI.FocusControl("UniTerminalCommandInput");
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;
            richLabel = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                // Label padding; a padded scrollview style breaks BeginScrollView.
                padding = new RectOffset(8, 8, 1, 1)
            };
            hintStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = false };
            hintStyle.clipping = TextClipping.Clip;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            showBrowser = GUILayout.Toggle(showBrowser, new GUIContent("Browse", "Show/hide the searchable command list"), EditorStyles.toolbarButton, GUILayout.Width(60));
            showOnlyEditorAllowed = GUILayout.Toggle(showOnlyEditorAllowed, new GUIContent("Editor", "In the Browse list, show only commands with AllowInEditor = true"), EditorStyles.toolbarButton, GUILayout.Width(52));

            bool cheats = ConsoleCommandInvoker.CheatsEnabled;
            bool newCheats = GUILayout.Toggle(cheats, "Cheats", EditorStyles.toolbarButton, GUILayout.Width(52));
            if (newCheats != cheats)
                ConsoleCommandInvoker.CheatsEnabled = newCheats;

            GUILayout.FlexibleSpace();

            bool isEditMode = !Application.isPlaying;
            var modeContent = new GUIContent(
                isEditMode ? "edit mode" : "playmode",
                isEditMode
                    ? "Unity is not playing: only commands with AllowInEditor = true can run."
                    : "Unity is playing: all commands can run.");
            GUILayout.Label(modeContent, EditorStyles.miniLabel, GUILayout.Width(62));

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48)))
            {
                logLines.Clear();
                logVersion++;
            }
            if (GUILayout.Button("Rebuild", EditorStyles.toolbarButton, GUILayout.Width(58)))
                ConsoleCommandRegistry.DiscoverCommandsEditor();
            EditorGUILayout.EndHorizontal();
        }

        #region Browser (collapsible)

        private void DrawBrowser()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName("UniTerminalCommandSearch");
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            showHidden = GUILayout.Toggle(showHidden, "hidden", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            searchRect = GUILayoutUtility.GetLastRect();

            List<KeyValuePair<string, List<CommandEntry>>> filtered = FilteredCommands();

            browserScroll = EditorGUILayout.BeginScrollView(browserScroll, EditorStyles.helpBox, GUILayout.MaxHeight(130));
            foreach (var kv in filtered)
            {
                bool isSelected = selectedCommandKey == kv.Key;
                bool anyEditor = kv.Value.Any(e => e.AllowInEditor);
                string badge = anyEditor ? "• " : "";
                string label = $"{badge}{kv.Key} — {kv.Value.FirstOrDefault()?.Description}";
                GUIStyle style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true), GUILayout.Height(17)))
                {
                    selectedCommandKey = kv.Key;
                    GUIUtility.keyboardControl = 0;
                    input = kv.Key + " ";
                    ResetTabState();
                    snapCaret = true;
                    refocusInput = true;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private List<KeyValuePair<string, List<CommandEntry>>> FilteredCommands()
        {
            EnsureCommandsLoaded();
            bool isEditMode = !Application.isPlaying;
            string q = (search ?? "").Trim().ToLowerInvariant();

            var result = new List<KeyValuePair<string, List<CommandEntry>>>();
            foreach (var kv in ConsoleCommandRegistry.Commands.OrderBy(c => c.Key))
            {
                if (ConsoleCommandRegistry.IsAlias(kv.Key, out _))
                    continue;

                var entries = kv.Value;
                if (entries == null || entries.Count == 0)
                    continue;

                bool anyEditor = entries.Any(e => e.AllowInEditor);
                if (isEditMode && showOnlyEditorAllowed && !anyEditor)
                    continue;

                if (!showHidden && entries.All(e => e.Flags.HasFlag(CommandFlags.Hidden)))
                    continue;

                if (!string.IsNullOrEmpty(q))
                {
                    string hay = kv.Key.ToLowerInvariant() + " " + string.Join(" ", entries.Select(e => e.Description ?? "")).ToLowerInvariant();
                    if (!hay.Contains(q))
                        continue;
                }

                result.Add(kv);
            }
            return result;
        }

        #endregion

        private void DrawLog()
        {
            if (cachedLogVersion != logVersion)
            {
                cachedLogVersion = logVersion;
                cachedLogText = string.Join("\n", logLines.Select(l => l.Text ?? ""));
            }

            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.ExpandHeight(true));
            float contentWidth = Mathf.Max(100f, position.width - 62f);
            float h = Mathf.Max(16f, richLabel.CalcHeight(new GUIContent(cachedLogText), contentWidth) + 2f);
            EditorGUILayout.SelectableLabel(cachedLogText, richLabel, GUILayout.Height(h));
            EditorGUILayout.EndScrollView();
            logRect = GUILayoutUtility.GetLastRect();

            if (scrollToBottom)
            {
                scrollToBottom = false;
                logScroll.y = float.MaxValue;
            }
        }

        #region Hint + Tab completion

        private void DrawHint()
        {
            string hint = BuildHint();
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(hint) ? " " : hint, hintStyle, GUILayout.Height(14));
            hintRect = GUILayoutUtility.GetLastRect();
        }

        private string BuildHint()
        {
            string[] tokens = Tokenize(input);
            if (tokens.Length == 0)
                return "";

            string cmdName = tokens[0].Trim('"').ToLowerInvariant();
            if (!ConsoleCommandRegistry.Commands.TryGetValue(cmdName, out List<CommandEntry> entries))
            {
                if (ConsoleCommandRegistry.IsAlias(cmdName, out CommandEntry aliased))
                    cmdName = string.IsNullOrWhiteSpace(aliased.Group)
                        ? aliased.CommandName.ToLowerInvariant()
                        : $"{aliased.Group.ToLowerInvariant()}.{aliased.CommandName.ToLowerInvariant()}";
                if (!ConsoleCommandRegistry.Commands.TryGetValue(cmdName, out entries))
                    return "";
            }

            CommandEntry first = !Application.isPlaying
                ? entries.FirstOrDefault(e => e.AllowInEditor) ?? entries.FirstOrDefault()
                : entries.FirstOrDefault();
            if (first == null)
                return "";
            return $"{first.CommandName} {GetSignature(first)}";
        }

        private static List<string> ComputeCandidates(string currentInput)
        {
            string[] tokens = Tokenize(currentInput);
            bool endsWithSpace = currentInput.Length > 0 && char.IsWhiteSpace(currentInput[^1]);
            bool editMode = !Application.isPlaying;

            if (tokens.Length == 0 || (tokens.Length == 1 && !endsWithSpace))
            {
                string prefix = tokens.Length == 0 ? "" : tokens[0].Trim('"');
                if (string.IsNullOrEmpty(prefix))
                    return new List<string>();
                return ConsoleCommandRegistry.Commands
                    .Where(kv => !ConsoleCommandRegistry.IsAlias(kv.Key, out _)
                        && (!editMode || kv.Value.Any(e => e.AllowInEditor))
                        && kv.Key.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(kv => kv.Key)
                    .OrderByDescending(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ThenBy(k => k)
                    .Take(20)
                    .ToList();
            }

            string cmdName = tokens[0].Trim('"').ToLowerInvariant();
            if (!ConsoleCommandRegistry.Commands.TryGetValue(cmdName, out List<CommandEntry> entries))
                return new List<string>();

            string currentPrefix = endsWithSpace ? "" : tokens[^1].Trim('"');
            int userArgIndex = tokens.Length - 1 - (endsWithSpace ? 0 : 1);
            string[] typedArgs = tokens.Skip(1).Select(t => t.Trim('"')).ToArray();

            var seen = new HashSet<string>();
            var ordered = new List<string>();
            foreach (CommandEntry entry in entries)
            {
                if (editMode && !entry.AllowInEditor)
                    continue;
                IEnumerable<string> results;
                try
                {
                    results = ConsoleCommandInvoker.GetAutoCompleteSuggestions(entry, userArgIndex, currentPrefix, typedArgs);
                }
                catch
                {
                    continue;
                }
                foreach (string s in results)
                {
                    if (seen.Add(s)) ordered.Add(s);
                }
            }
            return ordered.Take(20).ToList();
        }

        private void ApplyCandidate(string match)
        {
            string[] tokens = Tokenize(input);
            bool endsWithSpace = input.Length > 0 && char.IsWhiteSpace(input[^1]);

            if (tokens.Length <= 1 && !endsWithSpace)
            {
                input = match + " ";
            }
            else
            {
                string display = match.Contains(" ") && !match.StartsWith("\"") ? $"\"{match}\"" : match;
                if (endsWithSpace)
                    input += display + " ";
                else
                {
                    int lastSpace = input.TrimEnd().LastIndexOf(' ');
                    string head = lastSpace < 0 ? "" : input.Substring(0, lastSpace + 1);
                    input = head + display;
                }
            }
        }

        private void HandleTab()
        {
            if (!lastKeyWasTab || tabCandidates.Count == 0)
            {
                preTabInput = input;
                tabCandidates = ComputeCandidates(input);
                if (tabCandidates.Count == 0)
                    return;
                if (tabCandidates.Count > 1)
                    HandleLog(string.Join("  ", tabCandidates), true);
                tabIndex = 0;
            }
            else
            {
                // Rewind the previous insertion, then apply the next candidate fresh.
                // (Recomputing from the post-insert text would reset to index 0 forever.)
                input = preTabInput;
                tabIndex = (tabIndex + 1) % tabCandidates.Count;
            }

            ApplyCandidate(tabCandidates[tabIndex]);
            lastKeyWasTab = true;
            SnapCaretToEnd();
        }

        private void ResetTabState() { tabCandidates.Clear(); tabIndex = -1; preTabInput = null; lastKeyWasTab = false; }

        // Puts the caret at end of line so recalled/completed text is ready to edit or send.
        // Only valid while the input holds keyboard focus.
        private void SnapCaretToEnd()
        {
            if (GUI.GetNameOfFocusedControl() != "UniTerminalCommandInput") return;
            var te = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (te == null) return;
            te.text = input;
            te.MoveTextEnd();
        }

        private static string[] Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();
            return TokenizerRegex.Matches(text).Cast<Match>().Select(m => m.Value).ToArray();
        }

        private static string GetSignature(CommandEntry entry)
        {
            if (entry?.MethodInfo == null)
                return string.Join(" ", (entry?.ParameterTypes ?? Array.Empty<string>()));
            var pars = entry.MethodInfo.GetParameters()
                .Where(p => p.ParameterType != typeof(Action<string>)
                    && p.ParameterType != typeof(Action<string, bool>)
                    && p.ParameterType != typeof(CommandResponseDelegate))
                .Select(p => p.HasDefaultValue ? $"[{p.Name}:{p.ParameterType.Name}={p.DefaultValue}]" : $"<{p.Name}:{p.ParameterType.Name}>");
            return string.Join(" ", pars);
        }

        #endregion

        private void DrawInput()
        {
            // Keys are handled before the text field draws; it consumes Return and Tab.
            bool submit = false;
            bool tab = false;
            bool otherKey = false;

            bool inputFocused = GUI.GetNameOfFocusedControl() == "UniTerminalCommandInput";
            bool searchFocused = GUI.GetNameOfFocusedControl() == "UniTerminalCommandSearch";

            // History recall from anywhere except search. Unfocus first: a focused
            // field can paint its stale content over the assigned string.
            if (Event.current.type == EventType.KeyDown && !searchFocused && history.Count > 0 &&
                (Event.current.keyCode == KeyCode.UpArrow || Event.current.keyCode == KeyCode.DownArrow))
            {
                GUIUtility.keyboardControl = 0;
                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    historyIndex = historyIndex <= 0 ? 0 : historyIndex - 1;
                    input = history[historyIndex];
                }
                else
                {
                    historyIndex++;
                    input = historyIndex >= history.Count ? "" : history[historyIndex];
                    historyIndex = Mathf.Clamp(historyIndex, 0, history.Count);
                }
                ResetTabState();
                snapCaret = true;
                refocusInput = true;
                Event.current.Use();
                Repaint();
            }
            // Tab completes the input from anywhere except browser search, where it
            // keeps its native focus-navigation. Focus is pulled back to the box.
            else if (Event.current.keyCode == KeyCode.Tab &&
                (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp) &&
                !searchFocused)
            {
                if (Event.current.type == EventType.KeyDown)
                    tab = true;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.KeyDown && inputFocused)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        submit = true;
                        Event.current.Use();
                        break;
                    default:
                        otherKey = true;
                        break;
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(">", GUILayout.Width(14));
            GUI.SetNextControlName("UniTerminalCommandInput");
            if (snapCaret && GUI.GetNameOfFocusedControl() == "UniTerminalCommandInput")
            {
                snapCaret = false;
                SnapCaretToEnd();
            }
            input = EditorGUILayout.TextField(input);

            bool runPressed = GUILayout.Button("Run", GUILayout.Width(48));
            EditorGUILayout.EndHorizontal();

            if (tab)
            {
                HandleTab();
                GUIUtility.keyboardControl = 0;
                snapCaret = true;
                refocusInput = true;
                Repaint();
            }
            else if (otherKey)
            {
                lastKeyWasTab = false;
                snapCaret = false;
            }

            if (submit || runPressed)
                RunInput();
        }

        private void RunInput()
        {
            string cmd = (input ?? "").Trim();
            if (string.IsNullOrEmpty(cmd))
                return;

            if (history.Count == 0 || history[^1] != cmd)
                history.Add(cmd);
            historyIndex = history.Count;
            ResetTabState();

            // Local clear; the runtime command targets the in-game UI.
            if (cmd.Equals("clear", StringComparison.OrdinalIgnoreCase) || cmd.Equals("c", StringComparison.OrdinalIgnoreCase))
            {
                logLines.Clear();
                logVersion++;
                input = "";
                SnapCaretToEnd();
                Repaint();
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"UniTerminal: {cmd}");
            try
            {
                foreach (string part in cmd.Split('|'))
                {
                    string single = part.Trim();
                    if (string.IsNullOrEmpty(single))
                        continue;
                    ConsoleCommandInvoker.Execute(single);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            input = "";
            scrollToBottom = true;
            refocusInput = true;
            SnapCaretToEnd();
            GUI.FocusControl("UniTerminalCommandInput");
        }

        private readonly struct LogLine
        {
            public string Text { get; }
            public bool Success { get; }
            public LogLine(string text, bool success)
            {
                Text = text;
                Success = success;
            }
        }
    }
}
