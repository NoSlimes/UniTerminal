using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NoSlimes.Util.UniTerminal.Editor
{

    internal class UniTerminalEditorWindow : EditorWindow
    {
        private UniTerminalSettings settings;

        [MenuItem("Tools/UniTerminal/UniTerminal Window")]
        public static void ShowWindow()
        {
            UniTerminalEditorWindow window = GetWindow<UniTerminalEditorWindow>("UniTerminal");
            window.minSize = new Vector2(350, 400);
            window.maxSize = new Vector2(350, 600);
        }

        private void OnEnable()
        {
            settings = UniTerminalSettings.instance;

            #region Migrate Old Settings

            if (EditorPrefs.HasKey(UniTerminalDefines.AutoRebuildCacheKey))
                settings.IsAutoRebuildEnabled = EditorPrefs.GetBool(UniTerminalDefines.AutoRebuildCacheKey, true);

            if (EditorPrefs.HasKey(UniTerminalDefines.IncludeBuiltInCommandsKey))
                settings.IncludeBuiltInCommands = EditorPrefs.GetBool(UniTerminalDefines.IncludeBuiltInCommandsKey, true);

            if (EditorPrefs.HasKey(UniTerminalDefines.IncludeCheatCommandKey))
                settings.IncludeCheatCommand = EditorPrefs.GetBool(UniTerminalDefines.IncludeCheatCommandKey, true);

            if (EditorPrefs.HasKey(UniTerminalDefines.DetailedLoggingKey))
                settings.IsDetailedLoggingEnabled = EditorPrefs.GetBool(UniTerminalDefines.DetailedLoggingKey, false);

            DeleteIfExists(UniTerminalDefines.AutoRebuildCacheKey);
            DeleteIfExists(UniTerminalDefines.IncludeBuiltInCommandsKey);
            DeleteIfExists(UniTerminalDefines.IncludeCheatCommandKey);
            DeleteIfExists(UniTerminalDefines.DetailedLoggingKey);

            static void DeleteIfExists(string editorPrefKey)
            {
                if (EditorPrefs.HasKey(editorPrefKey))
                {
                    EditorPrefs.DeleteKey(editorPrefKey);
                }
            }
            #endregion

            rootVisualElement.Clear();
        }

        private void OnDisable()
        {
            SaveSettings();
        }

        private void CreateGUI()
        {
            if (settings == null)
                return;

            // Root container
            VisualElement root = new()
            {
                style =
                {
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 12,
                    paddingBottom = 12,
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.Stretch,
                    justifyContent = Justify.FlexStart
                }
            };

            root.Add(new Label("UniTerminal Settings")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    marginBottom = 10
                }
            });

            // Runtime & Editor section
            VisualElement runtimeSection = CreateSection("Runtime and Editor");

            ToggleButton includeBuiltInToggle = new("Include Built-In Commands in Cache", settings.IncludeBuiltInCommands);
            includeBuiltInToggle.OnValueChanged += SetIncludeBuiltInCommands;
            runtimeSection.Add(includeBuiltInToggle);

            ToggleButton includeCheatToggle = new("Include Built-In Cheat Command in Cache", settings.IncludeCheatCommand);
            includeCheatToggle.OnValueChanged += SetIncludeCheatCommand;
            runtimeSection.Add(includeCheatToggle);

            root.Add(runtimeSection);

            // Editor section
            VisualElement editorSection = CreateSection("Editor");

            ToggleButton autoRebuildToggle = new("Auto Rebuild Cache", settings.IsAutoRebuildEnabled);
            autoRebuildToggle.OnValueChanged += val =>
            {
                settings.IsAutoRebuildEnabled = val;
                SaveSettings();
            };
            editorSection.Add(autoRebuildToggle);

            ToggleButton detailedLoggingToggle = new("Detailed Logging", settings.IsDetailedLoggingEnabled);
            detailedLoggingToggle.OnValueChanged += val =>
            {
                settings.IsDetailedLoggingEnabled = val;
                SaveSettings();
            };
            editorSection.Add(detailedLoggingToggle);

            Button rebuildButton = new(() => ConsoleCommandRegistry.DiscoverCommandsEditor())
            {
                text = "Manual Rebuild Command Cache",
                style =
                {
                    marginTop = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = new Color(0.2f, 0.6f, 0.9f),
                    color = Color.white,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4
                }
            };
            editorSection.Add(rebuildButton);

            root.Add(editorSection);

            root.Add(new Label("All changes are saved automatically.")
            {
                style =
                {
                    fontSize = 10,
                    color = new Color(0.7f, 0.7f, 0.7f),
                    marginTop = 8
                }
            });

            rootVisualElement.Add(root);
        }

        private void SetIncludeBuiltInCommands(bool value)
        {
            settings.IncludeBuiltInCommands = value;

            if (settings.IncludeBuiltInCommands)
                UniTerminalDefines.EnableBuiltinCommands();
            else
                UniTerminalDefines.DisableBuiltinCommands();

            SaveSettings();
        }

        private void SetIncludeCheatCommand(bool value)
        {
            settings.IncludeCheatCommand = value;

            if (settings.IncludeCheatCommand)
                UniTerminalDefines.EnableBuiltinCheatCommand();
            else
                UniTerminalDefines.DisableBuiltinCheatCommand();

            SaveSettings();
        }

        private void SaveSettings()
        {
            if (settings == null) return;
            settings.Save();
        }

        private VisualElement CreateSection(string titleText)
        {
            VisualElement container = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.Stretch,
                    marginTop = 12
                }
            };

            Label titleLabel = new(titleText)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    marginBottom = 6
                }
            };

            VisualElement separator = new()
            {
                style =
                {
                    height = 1,
                    backgroundColor = new Color(0.5f, 0.5f, 0.5f),
                    marginBottom = 6
                }
            };

            container.Add(titleLabel);
            container.Add(separator);
            return container;
        }
    }

    internal class ToggleButton : VisualElement
    {
        public bool Value { get; private set; }

        private readonly VisualElement labelContainer;
        private readonly VisualElement indicator;
        private readonly Label label;

        public event System.Action<bool> OnValueChanged;

        private readonly Color activeColor = new(0.5f, 0.9f, 0.5f); // gentle green
        private readonly Color inactiveColor = new(0.9f, 0.5f, 0.5f); // gentle red
        private readonly Color backgroundColor = new(0.3f, 0.3f, 0.3f); // gray

        public ToggleButton(string text, bool initialValue = false)
        {
            Value = initialValue;

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Stretch;
            style.justifyContent = Justify.FlexStart;
            style.height = 30;
            style.backgroundColor = backgroundColor;
            style.borderBottomLeftRadius = 6;
            style.borderTopLeftRadius = 6;
            style.borderBottomRightRadius = 6;
            style.borderTopRightRadius = 6;
            style.marginBottom = 6;
            style.overflow = Overflow.Hidden;

            labelContainer = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 6,
                    paddingRight = 6,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart,
                    alignItems = Align.Center
                }
            };
            Add(labelContainer);

            label = new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    flexShrink = 0
                }
            };
            labelContainer.Add(label);

            indicator = new VisualElement
            {
                style =
                {
                    width = Length.Percent(10),
                    backgroundColor = Value ? activeColor : inactiveColor,
                    alignSelf = Align.Stretch,
                    borderBottomRightRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 0,
                    borderTopLeftRadius = 0
                }
            };
            Add(indicator);

            RegisterCallback<MouseDownEvent>(evt => Toggle());
        }

        public void Toggle()
        {
            Value = !Value;
            indicator.style.backgroundColor = Value ? activeColor : inactiveColor;
            OnValueChanged?.Invoke(Value);
        }
    }
}
