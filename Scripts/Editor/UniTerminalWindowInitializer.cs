using UnityEngine;
using UnityEditor;

namespace NoSlimes.Util.UniTerminal.Editor
{
	internal class UniTerminalWindowInitializer: ScriptableObject
	{
        private static string FirstTimeKey => $"{PlayerSettings.companyName}_{PlayerSettings.productName}_{PlayerSettings.productGUID}_UniTerminal_FirstTime_Shown";

        static UniTerminalWindowInitializer()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool(FirstTimeKey, false))
                {
                    UniTerminalEditorWindow.ShowWindow();
                    EditorPrefs.SetBool(FirstTimeKey, true);
                }
            };
        }
    }
}