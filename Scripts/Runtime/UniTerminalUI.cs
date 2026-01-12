using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NoSlimes.Util.UniTerminal
{
    internal class UniTerminalUI : MonoBehaviour
    {
        private static UniTerminalUI _instance;

        public enum InputSystemType { New, Old }

        [SerializeField] private InputSystemType inputSystem = InputSystemType.Old;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputActionReference toggleConsoleAction;
        [SerializeField] private InputActionReference autoCompleteAction;
        private InputAction historyUpAction;
        private InputAction historyDownAction;
#endif

        [SerializeField] private KeyCode toggleConsoleKey = KeyCode.BackQuote;
        [SerializeField] private KeyCode autoCompleteKey = KeyCode.Tab;
        [SerializeField] private GameObject consolePanel;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TMP_Text consoleLog;
        [SerializeField] private int maxLogLines = 100;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool catchUnityLogs = true;
        [SerializeField] private bool controlCursorLockMode = true;
        [SerializeField] private bool loadCacheOnAwake = true;
        [SerializeField] private char commandSeparator = '|';

        [SerializeField] private Color backgroundColor = new(0f, 0f, 0f, 0.95f);
        [SerializeField] private Color accentColor = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color exceptionColor = Color.red;
        [SerializeField] private Color assertColor = new(1, 0.6f, 0.6f);

        [SerializeField] private TMP_FontAsset consoleFont;
        [SerializeField] private int inputFontSize = 14;
        [SerializeField] private int logFontSize = 20;

        private readonly List<string> logHistory = new();
        private readonly List<string> commandHistory = new();
        private int commandHistoryIndex = -1;
        private CursorLockMode originalCursorLockMode;

        // Autocomplete state
        private string lastTypedPrefix = "";
        private List<string> currentMatches = new();
        private int autoCompleteIndex = -1;
        private string cachedBaseCommand = "";
        private bool ignoreNextValueChange = false;

        // 
        private string hoveredParamName = "";
        private int hoveredParamIndex = -1;
        private int lastCaretPosition = -1;

        private readonly System.Collections.Concurrent.ConcurrentQueue<(string logString, string stackTrace, LogType type)> logQueue = new();

        private static readonly Regex TokenizerRegex = new(@"[\""].+?[\""]|[^ ]+", RegexOptions.Compiled);

        internal static bool IsConsoleVisible => _instance != null && _instance.consolePanel.activeSelf;

        internal static event Action<bool> OnConsoleToggled;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

#if !ENABLE_INPUT_SYSTEM
            if (inputSystem == InputSystemType.New)
            {
                Debug.LogWarning("Developer Console: New Input System is not enabled. Switching to Old Input System.");
                inputSystem = InputSystemType.Old;
            }
#else
            if (inputSystem == InputSystemType.New)
            {
                historyUpAction = new InputAction("HistoryUp", binding: "<Keyboard>/upArrow");
                historyDownAction = new InputAction("HistoryDown", binding: "<Keyboard>/downArrow");
            }
#endif

            GetComponentInChildren<Canvas>().sortingOrder = 1000;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            if (loadCacheOnAwake)
            {
                ConsoleCommandRegistry.OnCacheLoaded += HandleCacheLoaded;
                ConsoleCommandRegistry.LoadCache();
            }

            ConsoleCommandInvoker.LogHandler += LogToConsole;

            ApplyStyles();
        }

        protected virtual void ApplyStyles()
        {
            if (consolePanel.TryGetComponent(out Image consolePanelImage))
            {
                consolePanelImage.color = backgroundColor;
            }

            foreach (Image img in consolePanel.GetComponentsInChildren<Image>())
            {
                if (img.gameObject != consolePanel && img.gameObject != inputField.gameObject)
                {
                    img.color = accentColor;
                }
            }

            if (consoleFont != null)
            {
                inputField.textComponent.font = consoleFont;
                consoleLog.font = consoleFont;
            }

            inputField.textComponent.fontSize = inputFontSize;
            consoleLog.fontSize = logFontSize;

            ConsoleCommandInvoker.Settings.TextColor = textColor;
            ConsoleCommandInvoker.Settings.WarningColor = warningColor;
            ConsoleCommandInvoker.Settings.ErrorColor = errorColor;
            ConsoleCommandInvoker.Settings.SecondaryErrorColor = Color.Lerp(errorColor, Color.white, 0.4f);
        }

        private void HandleCacheLoaded(double ms)
        {
            int totalMethods = ConsoleCommandRegistry.Commands.Sum(kv => kv.Value.Count);
            LogToConsole($"[UniTerminal] Loaded {totalMethods} commands in {ms:F3} ms.");
            ConsoleCommandRegistry.OnCacheLoaded -= HandleCacheLoaded;
        }

        private void OnEnable()
        {
            if (catchUnityLogs)
                Application.logMessageReceived += HandleLogMessage;

            inputField.onSubmit.AddListener((cmd) =>
            {
                if (string.IsNullOrWhiteSpace(cmd)) return;

                string[] commands = cmd.Split(commandSeparator, StringSplitOptions.RemoveEmptyEntries);

                foreach (var singleCommand in commands)
                {
                    string trimmedCommand = singleCommand.Trim();
                    if (string.IsNullOrEmpty(trimmedCommand)) continue;

                    ConsoleCommandInvoker.Execute(trimmedCommand);
                }

                if (commandHistory.Count == 0 || commandHistory[^1] != cmd)
                {
                    commandHistory.Add(cmd);
                }

                inputField.text = "";
                commandHistoryIndex = -1;
                lastTypedPrefix = "";
                currentMatches.Clear();
                FocusInputField();
            });

            inputField.onValueChanged.AddListener((val) =>
            {
                UpdateHoverContext(val);

                if (ignoreNextValueChange)
                {
                    ignoreNextValueChange = false;
                    return;
                }

                autoCompleteIndex = -1;
            });

            consolePanel.SetActive(false);

#if ENABLE_INPUT_SYSTEM
            if (inputSystem == InputSystemType.New)
            {
                if (toggleConsoleAction != null)
                {
                    toggleConsoleAction.action.performed += OnToggleConsoleAction;
                    toggleConsoleAction.action.Enable();
                }
                if (historyUpAction != null)
                {
                    historyUpAction.performed += _ => NavigateCommandHistory(-1);
                    historyUpAction.Enable();
                }
                if (historyDownAction != null)
                {
                    historyDownAction.performed += _ => NavigateCommandHistory(1);
                    historyDownAction.Enable();
                }
                if (autoCompleteAction != null)
                {
                    autoCompleteAction.action.performed += _ => AutoComplete();
                    autoCompleteAction.action.Enable();
                }
            }
#endif
        }

        private void OnDisable()
        {
            if (catchUnityLogs)
                Application.logMessageReceived -= HandleLogMessage;

            inputField.onSubmit.RemoveAllListeners();
            inputField.onValueChanged.RemoveAllListeners();

#if ENABLE_INPUT_SYSTEM
            if (inputSystem == InputSystemType.New)
            {
                if (toggleConsoleAction != null)
                    toggleConsoleAction.action.performed -= OnToggleConsoleAction;
                historyUpAction?.Disable();
                historyDownAction?.Disable();
                autoCompleteAction.action?.Disable();
            }
#endif
        }

        private void OnDestroy()
        {
            ConsoleCommandInvoker.LogHandler -= LogToConsole;
        }

        private void Update()
        {
            while (logQueue.TryDequeue(out (string logString, string stackTrace, LogType type) logEntry))
            {
                ProcessLogMessage(logEntry.logString, logEntry.stackTrace, logEntry.type);
            }

            if (inputField.isFocused && inputField.caretPosition != lastCaretPosition)
            {
                lastCaretPosition = inputField.caretPosition;
                UpdateHoverContext(inputField.text);
            }

            if (inputSystem == InputSystemType.Old)
            {
                if (Input.GetKeyDown(toggleConsoleKey)) ToggleConsole();

                if (consolePanel.activeSelf && inputField.isFocused)
                {
                    if (Input.GetKeyDown(KeyCode.UpArrow)) NavigateCommandHistory(-1);
                    else if (Input.GetKeyDown(KeyCode.DownArrow)) NavigateCommandHistory(1);
                    else if (Input.GetKeyDown(autoCompleteKey)) AutoComplete();
                }
            }
        }

        public static void ShowConsole(bool show)
        {
            if (_instance == null) return;

            if (_instance.consolePanel.activeSelf != show)
                _instance.ToggleConsole();
        }

        public static void ClearLog()
        {
            if (_instance == null) return;

            _instance.logHistory.Clear();
            _instance.consoleLog.text = "";
        }

        private void HandleLogMessage(string logString, string stackTrace, LogType type)
        {
            logQueue.Enqueue((logString, stackTrace, type));
        }

        private void ProcessLogMessage(string logString, string stackTrace, LogType type)
        {
            string color = type switch
            {
                LogType.Log => FormatColorLocal(textColor),
                LogType.Warning => FormatColorLocal(warningColor),
                LogType.Error => FormatColorLocal(errorColor),
                LogType.Exception => FormatColorLocal(exceptionColor),
                LogType.Assert => FormatColorLocal(assertColor),
                _ => "white",
            };

            LogToConsole($"<color={color}>{logString}</color>");
            return;

            static string FormatColorLocal(Color color)
            {
                return ColorUtility.ToHtmlStringRGBA(color);
            }
        }

        private void ToggleConsole()
        {
            bool isActive = !consolePanel.activeSelf;
            consolePanel.SetActive(isActive);

            if (isActive)
            {
                if (controlCursorLockMode)
                {
                    originalCursorLockMode = Cursor.lockState;
                    Cursor.lockState = CursorLockMode.None;
                }
                FocusInputField();
            }
            else
            {
                if (controlCursorLockMode) Cursor.lockState = originalCursorLockMode;
                inputField.DeactivateInputField();
                inputField.text = "";
                commandHistoryIndex = -1;
            }

            OnConsoleToggled?.Invoke(isActive);
        }

        private void FocusInputField()
        {
            inputField.Select();
            inputField.ActivateInputField();
        }

        private void LogToConsole(string message)
        {
            logHistory.Add(message);
            if (logHistory.Count > maxLogLines)
                logHistory.RemoveRange(0, logHistory.Count - maxLogLines);

            consoleLog.text = string.Join("\n", logHistory);
            StartCoroutine(ScrollToBottomCoroutine());
        }

        private void LogToConsole(string message, bool success)
        {
            string color = success ? ColorUtility.ToHtmlStringRGBA(textColor) : ColorUtility.ToHtmlStringRGBA(errorColor);
            LogToConsole($"<color=#{color}>{message}</color>");
        }

        private IEnumerator ScrollToBottomCoroutine()
        {
            yield return new WaitForEndOfFrame();
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0;
        }

        private void NavigateCommandHistory(int direction)
        {
            if (commandHistory.Count == 0) return;

            if (commandHistoryIndex == -1)
                commandHistoryIndex = commandHistory.Count;

            commandHistoryIndex += direction;

            if (commandHistoryIndex < 0)
            {
                commandHistoryIndex = 0;
            }
            else if (commandHistoryIndex >= commandHistory.Count)
            {
                commandHistoryIndex = commandHistory.Count;
                inputField.text = "";
                return;
            }

            inputField.text = commandHistory[commandHistoryIndex];
            StartCoroutine(MoveCaretToEndCoroutine());
        }

        private IEnumerator MoveCaretToEndCoroutine()
        {
            yield return new WaitForEndOfFrame();
            inputField.MoveTextEnd(false);
        }

        private CommandContext ParseCommandContext(string input, int caretPos)
        {
            input ??= "";
            int lastSep = input.LastIndexOf(commandSeparator, Math.Max(0, Math.Min(caretPos - 1, input.Length - 1)));
            string globalPrefix = lastSep != -1 ? input.Substring(0, lastSep + 1) : "";
            string activeSegment = lastSep != -1 ? input.Substring(lastSep + 1) : input;

            int trimStart = 0;
            while (trimStart < activeSegment.Length && char.IsWhiteSpace(activeSegment[trimStart])) trimStart++;
            string whitespace = activeSegment.Substring(0, trimStart);
            string trimmed = activeSegment.TrimStart();

            MatchCollection matches = TokenizerRegex.Matches(trimmed);
            List<string> partsList = matches.Cast<Match>().Select(m => m.Value).ToList();
            if (trimmed.EndsWith(" ")) partsList.Add("");
            if (partsList.Count == 0) partsList.Add("");

            int partIndex = partsList.Count - 1;

            return new CommandContext
            {
                GlobalPrefix = globalPrefix,
                ActiveSegment = trimmed,
                Whitespace = whitespace,
                Parts = partsList.ToArray(),
                CurrentPartIndex = partIndex,
                CurrentPrefix = partsList[partIndex].Replace("\"", ""),
                IsHelp = partsList.Count > 1 && partsList[0].Equals("help", StringComparison.OrdinalIgnoreCase)
            };
        }

        private void UpdateHoverContext(string input)
        {
            var ctx = ParseCommandContext(input, inputField.caretPosition);
            hoveredParamIndex = ctx.CurrentPartIndex - 1;

            if (ctx.CurrentPartIndex > 0 && !ctx.IsHelp)
            {
                string cmdName = ctx.Parts[0].ToLower();
                if (ConsoleCommandRegistry.Commands.TryGetValue(cmdName, out var methods))
                {
                    // Collect unique parameter names for the current index
                    var names = methods
                        .Select(m => m.GetParameters())
                        .Where(p => p.Length > hoveredParamIndex + 1)
                        .Select(p => p[hoveredParamIndex + 1].Name)
                        .Distinct();

                    hoveredParamName = string.Join(" | ", names);
                }
                else hoveredParamName = "";
            }
            else hoveredParamName = "";

            UpdateHintUI(input);
        }

        private void UpdateHintUI(string input)
        {
            if (hintText == null) return;

            if (string.IsNullOrEmpty(hoveredParamName) || string.IsNullOrEmpty(input))
            {
                hintText.text = "";
                return;
            }

            // We use a completely transparent version of the typed text to push 
            // the hint to the correct horizontal position.
            // The "  " adds that extra spacing you wanted.
            string invisibleTyped = $"<color=#00000000>{input}</color>";
            string paramHint = $"  <color=#ffffff66>({hoveredParamName})</color>";

            hintText.text = invisibleTyped + paramHint;
        }

        private void AutoComplete()
        {
            var ctx = ParseCommandContext(inputField.text, inputField.caretPosition);

            string cleanLastPrefix = lastTypedPrefix.Replace("\"", "");

            if (autoCompleteIndex == -1 || (ctx.CurrentPrefix != cleanLastPrefix))
            {
                autoCompleteIndex = -1;
                currentMatches.Clear();

                if (ctx.CurrentPartIndex == 0 || ctx.IsHelp)
                {
                    currentMatches = ConsoleCommandRegistry.Commands.Keys
                        .Where(k => k.IndexOf(ctx.CurrentPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderByDescending(k => k.StartsWith(ctx.CurrentPrefix, StringComparison.OrdinalIgnoreCase))
                        .ThenBy(k => k).ToList();
                }
                else
                {
                    if (ConsoleCommandRegistry.Commands.TryGetValue(ctx.Parts[0].ToLower(), out var methods))
                    {
                        HashSet<string> suggestions = new();
                        foreach (var m in methods)
                        {
                            foreach (var s in ConsoleCommandInvoker.GetAutoCompleteSuggestions(m, hoveredParamIndex, ctx.CurrentPrefix))
                                suggestions.Add(s);
                        }

                        currentMatches = suggestions
                            .OrderByDescending(s => s.StartsWith(ctx.CurrentPrefix, StringComparison.OrdinalIgnoreCase))
                            .ThenBy(s => s).ToList();
                    }
                }

                string preSegments = ctx.Parts.Length > 1 ? string.Join(" ", ctx.Parts, 0, ctx.Parts.Length - 1) + " " : "";
                cachedBaseCommand = ctx.GlobalPrefix + ctx.Whitespace + preSegments;
            }

            if (currentMatches.Count == 0) return;

            autoCompleteIndex = (autoCompleteIndex + 1) % currentMatches.Count;
            string selectedMatch = currentMatches[autoCompleteIndex];

            lastTypedPrefix = selectedMatch;

            if (selectedMatch.Contains(" ") && !selectedMatch.StartsWith("\""))
                selectedMatch = $"\"{selectedMatch}\"";

            ignoreNextValueChange = true;
            inputField.text = cachedBaseCommand + selectedMatch;
            inputField.caretPosition = inputField.text.Length;

            UpdateHoverContext(inputField.text);
        }

#if ENABLE_INPUT_SYSTEM
        private void OnToggleConsoleAction(InputAction.CallbackContext context) => ToggleConsole();
#endif

        #region Built-in basic commands
        [ConsoleCommand("help", "Shows a list of commands or details for one command.")]
        private static void HelpCommand(Action<string> response, string commandName = "")
        {
            string output = ConsoleCommandInvoker.GetHelp(commandName);
            response(output);
        }

        [ConsoleCommand("clear", "Clears the console log.")]
        private static void ClearCommand() => ClearLog();

        [ConsoleCommand("toggleUnityLogs", "Toggles display of unity debug logs")]
        private static void ToggleUnityLogsCommand(Action<string> response)
        {
            if (_instance == null) return;
            _instance.catchUnityLogs = !_instance.catchUnityLogs;

            if (_instance.catchUnityLogs)
                Application.logMessageReceived += _instance.HandleLogMessage;
            else
                Application.logMessageReceived -= _instance.HandleLogMessage;

            var color = _instance.catchUnityLogs ? "green" : "red";
            response($"Unity logs are now <color={color}>{(_instance.catchUnityLogs ? "enabled" : "disabled")}</color>");
        }
        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyStyles();
        }
#endif

        private struct CommandContext
        {
            public string GlobalPrefix;
            public string ActiveSegment;
            public string Whitespace;
            public string[] Parts;
            public int CurrentPartIndex;
            public string CurrentPrefix;
            public bool IsHelp;
        }
    }
}