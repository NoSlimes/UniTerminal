using UnityEditor;
using UnityEngine;

namespace NoSlimes.Util.UniTerminal.Editor
{
    [CustomEditor(typeof(UniTerminalUI))]
    // Add CanEditMultipleObjects if you want to edit several at once
    [CanEditMultipleObjects]
    internal class UniTerminalInspector : UnityEditor.Editor
    {
        #region Serialized Properties
        private SerializedProperty inputSystemProp;
        private SerializedProperty toggleConsoleActionProp;
        private SerializedProperty autoCompleteActionProp;
        private SerializedProperty toggleConsoleKeyProp;
        private SerializedProperty autoCompleteKeyProp;
        private SerializedProperty commandSeparatorProp;

        private SerializedProperty consolePanelProp;
        private SerializedProperty inputFieldProp;
        private SerializedProperty hintTextProp;
        private SerializedProperty scrollRectProp;
        private SerializedProperty consoleLogProp;
        private SerializedProperty maxLogLinesProp;

        private SerializedProperty dontDestroyOnLoadProp;
        private SerializedProperty catchUnityLogsProp;
        private SerializedProperty controlCursorLockModeProp;
        private SerializedProperty loadCacheOnAwakeProp;

        private SerializedProperty bgColorProp;
        private SerializedProperty accentColorProp;
        private SerializedProperty textColorProp;
        private SerializedProperty warningColorProp;
        private SerializedProperty errorColorProp;
        private SerializedProperty assertColorProp;
        private SerializedProperty consoleFontProp;
        private SerializedProperty inputFontSizeProp;
        private SerializedProperty logFontSizeProp;
        #endregion

        private bool showCustomization = false;

        private void OnEnable()
        {
            inputSystemProp = serializedObject.FindProperty("inputSystem");
            toggleConsoleActionProp = serializedObject.FindProperty("toggleConsoleAction");
            autoCompleteActionProp = serializedObject.FindProperty("autoCompleteAction");
            toggleConsoleKeyProp = serializedObject.FindProperty("toggleConsoleKey");
            autoCompleteKeyProp = serializedObject.FindProperty("autoCompleteKey");
            commandSeparatorProp = serializedObject.FindProperty("commandSeparator");

            consolePanelProp = serializedObject.FindProperty("consolePanel");
            inputFieldProp = serializedObject.FindProperty("inputField");
            hintTextProp = serializedObject.FindProperty("hintText");
            scrollRectProp = serializedObject.FindProperty("scrollRect");
            consoleLogProp = serializedObject.FindProperty("consoleLog");
            maxLogLinesProp = serializedObject.FindProperty("maxLogLines");

            dontDestroyOnLoadProp = serializedObject.FindProperty("dontDestroyOnLoad");
            catchUnityLogsProp = serializedObject.FindProperty("catchUnityLogs");
            controlCursorLockModeProp = serializedObject.FindProperty("controlCursorLockMode");
            loadCacheOnAwakeProp = serializedObject.FindProperty("loadCacheOnAwake");

            bgColorProp = serializedObject.FindProperty("backgroundColor");
            accentColorProp = serializedObject.FindProperty("accentColor");
            textColorProp = serializedObject.FindProperty("textColor");
            warningColorProp = serializedObject.FindProperty("warningColor");
            errorColorProp = serializedObject.FindProperty("errorColor");
            assertColorProp = serializedObject.FindProperty("assertColor");
            consoleFontProp = serializedObject.FindProperty("consoleFont");
            inputFontSizeProp = serializedObject.FindProperty("inputFontSize");
            logFontSizeProp = serializedObject.FindProperty("logFontSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawSection("Input Settings", () =>
            {
#if ENABLE_INPUT_SYSTEM
                if (inputSystemProp != null)
                {
                    EditorGUILayout.PropertyField(inputSystemProp);

                    // Check enum value safely
                    if (inputSystemProp.enumValueIndex == (int)UniTerminalUI.InputSystemType.New)
                    {
                        EditorGUILayout.PropertyField(toggleConsoleActionProp);
                        EditorGUILayout.PropertyField(autoCompleteActionProp);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(toggleConsoleKeyProp);
                        EditorGUILayout.PropertyField(autoCompleteKeyProp);
                    }
                }
#else
                EditorGUILayout.PropertyField(toggleConsoleKeyProp);
                EditorGUILayout.PropertyField(autoCompleteKeyProp);
                EditorGUILayout.HelpBox("New Input System package not enabled in Project Settings.", MessageType.Warning);
#endif
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(commandSeparatorProp);
            });

            // --- Section: References ---
            DrawSection("UI References", () =>
            {
                EditorGUILayout.PropertyField(consolePanelProp);
                EditorGUILayout.PropertyField(inputFieldProp);
                EditorGUILayout.PropertyField(hintTextProp);
                EditorGUILayout.PropertyField(scrollRectProp);
                EditorGUILayout.PropertyField(consoleLogProp);
            });

            // --- Section: Configuration ---
            DrawSection("Configuration", () =>
            {
                EditorGUILayout.PropertyField(maxLogLinesProp);
                EditorGUILayout.PropertyField(dontDestroyOnLoadProp);
                EditorGUILayout.PropertyField(catchUnityLogsProp);
                EditorGUILayout.PropertyField(controlCursorLockModeProp);
                EditorGUILayout.PropertyField(loadCacheOnAwakeProp);
            });

            // --- Section: Visuals ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showCustomization = EditorGUILayout.Foldout(showCustomization, "Visual Customization", true);
            if (showCustomization)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(bgColorProp);
                EditorGUILayout.PropertyField(accentColorProp);
                EditorGUILayout.PropertyField(textColorProp);
                EditorGUILayout.PropertyField(warningColorProp);
                EditorGUILayout.PropertyField(errorColorProp);
                EditorGUILayout.PropertyField(assertColorProp);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Typography", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(consoleFontProp);
                EditorGUILayout.PropertyField(inputFontSizeProp);
                EditorGUILayout.PropertyField(logFontSizeProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawSection(string title, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            drawContent?.Invoke();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }
}