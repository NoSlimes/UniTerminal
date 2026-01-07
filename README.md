## Setup

Use the menu to create and add the Developer Console prefab to your scene:  
- `Assets → Create → UniTerminal → UniTerminal`  
- `Tools → UniTerminal → Create UniTerminal Prefab`  

This will create a prefab variant in the folder currently open in the Project window.

## Command Parameters

When defining commands, methods can have the following parameters:

### 1. Response callback (optional)

```csharp
Action<string> response        // Receives console messages
Action<string, bool> response  // Receives message + success/failure
CommandResponseDelegate response // Receives message + optional success/failure
````

* `string` = message to log
* `bool` = whether the command succeeded

**Example:**

```csharp
[ConsoleCommand("heal", "Heals the player.")]
public static void HealCommand(Action<string, bool> response, int amount)
{
    if (amount < 0) {
        response("Amount cannot be negative!", false); // Command did not succeed - logs as an error (red)
        return;
    }
    Player.Instance.Heal(amount);
    response($"Healed player for {amount} HP", true); // Command succeeded - logs normally
}
```

---

### 2. Arguments

UniTerminal supports a wide range of argument types, with automatic type conversion from strings:

| Type | Description | Example Input |
|------|-------------|---------------|
| `string` | Any text. Use quotes if it contains spaces. | `"Hello world"` |
| `int` | Integer numbers. | `42` |
| `float` | Decimal numbers. | `3.14` |
| `bool` | True or false. | `true` / `false` |
| `enum` | Any enum type. Matches enum names (case-insensitive). | `MoveMode.Walk` |
| `Vector2` | 2D vector `(x,y)` format. | `(1.0,2.5)` |
| `Vector3` | 3D vector `(x,y,z)` format. | `(0,1,0)` |
| `Color` | RGBA color `(r,g,b,a)` format. | `(1,0,0,1)` for opaque red |
| `Quaternion` | Rotation `(x,y,z,w)` format. | `(0,0,0,1)` |

#### Notes

* **Default values:** Arguments can have defaults if omitted:

```csharp
[ConsoleCommand("screenshot", "Takes a screenshot.")]
public static void ScreenshotCommand(Action<string> response, string filename = "screenshot.png")
{
    ScreenCapture.CaptureScreenshot(filename);
    response($"Saved screenshot as {filename}", true);
}
````

* **Multiple arguments:** Separate with spaces. Strings containing spaces must be quoted:

```
spawnEnemy "Big Slime" (0,1,0) true
```

* **Type conversion:** UniTerminal automatically converts argument strings to the required type. Errors are logged if conversion fails.

* **Nullable types:** Nullable parameters are supported, e.g., `int?`, `float?`.

* **Custom argument types:** You can register your own converters for custom types. For example, the built-in `Vector3` converter:

```csharp
UniTerminal.RegisterArgConverter<Vector3>(arg =>
{
    var parts = arg.Trim('(', ')').Split(',');
    if (parts.Length != 3)
        throw new ArgumentException($"Could not convert '{arg}' to Vector3");

    return new Vector3(
        float.Parse(parts[0]),
        float.Parse(parts[1]),
        float.Parse(parts[2])
    );
});
```

Once registered, UniTerminal will automatically convert arguments of that type when invoking commands.

---

### 3. Command Execution

Commands are invoked by typing in the console:

```
<command> [arg1] [arg2] ...
```

* The first parameter can optionally be a response callback (`Action<string>` or `Action<string, bool>`).
* Remaining parameters are parsed and converted automatically.
* Quoted strings are supported: `"Hello World"`.
* Errors such as missing or invalid arguments are automatically logged to the console.

---

### 4. Chained Commands

Multiple commands can be executed in a single line using the `|`(configurable) separator:

```
command1 arg1 | command2 arg2 arg3
```

Each command runs sequentially, and errors in one command do not prevent subsequent commands from executing.

---

### 5. Command Flags

Commands can include **flags** to control when and how they are available. Flags are optional and can be combined using the bitwise OR operator (`|`).

| Flag | Description |
|------|-------------|
| `None` | No special behavior. Command is always available. |
| `DebugOnly` | Command is only available in debug builds. Cannot be combined with `EditorOnly`. |
| `EditorOnly` | Command is only available in the Unity editor. Cannot be combined with `DebugOnly`. |
| `Cheat` | Marks the command as a cheat. Only runs if `CheatsEnabled` is `true`. |
| `Mod` | Command is added by a mod or external plugin. |
| `Hidden` | Command is hidden from help listings, but can still be invoked. |

**Cheat Commands Note:**

* UniTerminal includes a built-in `enablecheats` command to toggle cheat commands globally.  
* Alternatively, you can disable the built-in command and provide your own mechanism for enabling cheats by setting `UniTerminal.CheatsEnabled` manually.  
* Commands with the `Cheat` flag will only run if `CheatsEnabled` is `true`.

**Example:**

```csharp
[ConsoleCommand("godMode", "Enables invincibility.", CommandFlags.Cheat | CommandFlags.DebugOnly)]
public static void GodModeCommand(Action<string> response)
{
    Player.Instance.Invincible = true;
    response("God mode enabled!", true);
}
```
---

Here is the updated section 6, expanded to include the new functionality for registering auto-complete methods.

---

### 6. Auto-completion & Suggestions

UniTerminal supports tab-based auto-completion for both command names and argument values.

*   **Command Names:** Unmatched text is automatically completed against registered commands (case-insensitive).
*   **Built-in Types:** Arguments of type `bool` (`true`/`false`) and `enum` are automatically auto-completed.

#### Custom Argument Suggestions

You can provide dynamic suggestions for your string arguments (e.g., Item IDs, Entity Names) by referencing a static method in the `[ConsoleCommand]` attribute.

**How to register:**
1.  Create a `static` method in the same class that returns `IEnumerable<string>` or `string[]`.
2.  Pass the method's name to the `AutoCompleteProvider` property in the attribute.

**Supported Method Signatures:**
UniTerminal automatically detects the parameters of your provider method. You can choose the signature that best fits your complexity needs:

| Signature | Who Filters? | Description |
| :--- | :--- | :--- |
| `()` | **System** | Returns the same list for *every* argument. Best used for commands with **only one** parameter. |
| `(int index)` | **System** | Returns options specific to the argument index being typed. Best for multi-parameter commands. |
| `(string prefix)` | **You** | You receive the current input. You must filter and return only matches. |
| `(string prefix, int index)` | **You** | You receive input and argument index. You must filter and return matches. |

> **Important: Index & Callbacks**
> The `index` parameter represents the argument index **as typed by the user in the console**.
> If your command method requests an `Action<string>` or `Action<string, bool>` for responses, **this parameter is ignored** for indexing. The first argument typed by the user is always `index 0`.

---

#### Examples

**1. Simple List `()`**
*Best for: Commands with a **single parameter**.*
Since this signature doesn't receive the argument index, it will return the same suggestions for every argument.

```csharp
[ConsoleCommand("spawn", "Spawns an entity.", AutoCompleteProvider = nameof(GetEntityNames))]
public static void SpawnCommand(string entityName) { ... }

// System handles filtering (StartsWith)
private static IEnumerable<string> GetEntityNames()
{
    return new[] { "Slime", "Goblin", "Dragon", "Skeleton" };
}
```

**2. Index Aware `(int index)`**
*Best for: Commands with **multiple arguments** where you want the system to handle filtering.*
*Note how `statName` is index 0, even though the C# method has an `Action` as the first parameter.*

```csharp
[ConsoleCommand("set_stat", "Sets a stat.", AutoCompleteProvider = nameof(StatSuggestions))]
public static void SetStatCommand(Action<string> reply, string statName, string mode) { ... }

// 'index' is 0 for 'statName', 1 for 'mode' (The Action parameter is skipped)
private static IEnumerable<string> StatSuggestions(int index)
{
    return index switch
    {
        0 => new[] { "Health", "Mana", "Stamina" },
        1 => new[] { "Set", "Add", "Subtract" },
        _ => Array.Empty<string>()
    };
}
```

**3. Manual Filtering `(string prefix)`**
*Best for: Custom matching logic (e.g., 'Contains' instead of 'StartsWith') or specific optimization.*

```csharp
[ConsoleCommand("search_part", "Find part.", AutoCompleteProvider = nameof(SearchParts))]
public static void SearchCommand(string query) { ... }

// You must filter the results yourself using 'prefix'
private static IEnumerable<string> SearchParts(string prefix)
{
    // Example: Using 'Contains' allows finding "Engine_Piston" by typing "Piston"
    return PartDatabase.AllParts
        .Where(p => p.Contains(prefix, StringComparison.OrdinalIgnoreCase)); 
}
```

**4. Full Control `(string prefix, int index)`**
*Best for: Complex commands with multiple arguments requiring custom logic per argument.*

```csharp
[ConsoleCommand("give", "Give item.", AutoCompleteProvider = nameof(GiveSuggestions))]
public static void GiveCommand(Action<string> reply, string target, string itemId) { ... }

private static IEnumerable<string> GiveSuggestions(string prefix, int index)
{
    // Index 0 = 'target', Index 1 = 'itemId' (Action is skipped)
    if (index == 0) 
    {
        // Custom logic for Target
        return new[] { "Player", "Enemy" }.Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
    else if (index == 1) 
    {
        // Custom logic for ItemId (e.g. database lookup)
        return ItemDatabase.FindMatches(prefix);
    }
    return Array.Empty<string>();
}
```

*Note: If a suggestion contains spaces (e.g., `"Big Slime"`), UniTerminal will automatically wrap it in quotes when selected.*

---

### 7. Performance & Caching

UniTerminal is designed for **zero startup overhead**. Unlike traditional consoles that scan your assemblies using Reflection every time the game starts (causing lag), UniTerminal pre-calculates and caches command metadata in the Unity Editor.

**When does the cache update?**

1.  **Automatically:** The cache is rebuilt automatically whenever you modify your scripts and trigger a **Recompile / Domain Reload**.
2.  **Manually:** If for any reason the cache seems out of sync, you can force a rebuild via the menu:
    *   `Tools → UniTerminal → Manual Build Command Cache`

**What this means for you:**
*   **Editor:** You simply write code. Commands appear in the console immediately after compilation.
*   **Builds:** The cached data is serialized into the build. The game launches with **0ms** reflection cost for command discovery.

> **Troubleshooting:** If you just added a `[ConsoleCommand]` but it's not showing up in autocomplete, ensure your scripts have finished compiling. If it still persists, click **Manual Build Command Cache**.

---

### 8. Configuration & Settings

You can configure UniTerminal's behavior via the dedicated settings window. These settings allow you to strip out built-in features to keep your builds lightweight or adjust the editor workflow.

**Open the settings window:**
*   `Tools → UniTerminal → UniTerminal Window`

#### Runtime & Editor Settings
These settings modify the **Scripting Define Symbols** of your project. Changing them will trigger a script recompilation.

| Setting | Description |
| :--- | :--- |
| **Include Built-In Commands** | If enabled, includes additional utility commands (e.g. time scale or system info). <br><br>Note: The core commands help, clear, and toggleUnityLogs are essential to the system and are always included, regardless of this setting. |
| **Include Built-In Cheat Command** | If enabled, the enablecheats command is available. Disable this for release builds if you want to prevent users from easily toggling cheat mode (or if you want to implement your own cheat logic). |

#### Editor Workflow
These settings only affect the Unity Editor environment and are saved in `EditorPrefs`.

| Setting | Description |
| :--- | :--- |
| **Auto Rebuild Cache** | **(Recommended: On)** Automatically updates the command cache whenever scripts are compiled. If you have a very large project with slow iteration times, you can turn this off and manually rebuild the cache only when needed. |
| **Detailed Logging** | If enabled, UniTerminal will log a **change report** to the Unity Console after a cache rebuild, detailing exactly which commands were **Added**, **Removed**, or **Modified**. Useful for verifying that your code changes were detected correctly. |

#### Manual Actions
*   **Manual Rebuild Command Cache:** Forces a full reflection scan of your assemblies and rebuilds the command database. Use this if the console isn't picking up a new `[ConsoleCommand]` attribute immediately.

---

### 9. Runtime Assembly Discovery (Mods & DLC)

Since UniTerminal caches commands at build time, any code loaded **dynamically** after the game starts (such as Mods, DLCs, or Addressables) will not be in the initial cache. You must manually tell UniTerminal to scan these new assemblies.

#### How to register an assembly

Use `UniTerminal.DiscoverCommandsInAssembly()` to scan a specific assembly for `[ConsoleCommand]` attributes.

**Example 1: Self-Registering Mod**
If you are building a mod (e.g., for BepInEx), you can have the mod register itself when it loads.

```csharp
using System.Reflection;
using UnityEngine;
using NoSlimes.Util.UniTerminal;

public class MyMod : MonoBehaviour
{
    private void Awake()
    {
        // Tells UniTerminal to scan THIS assembly for commands immediately
        UniTerminal.DiscoverCommandsInAssembly(Assembly.GetExecutingAssembly());
        
        Debug.Log("MyMod commands registered!");
    }

    [ConsoleCommand("mod_hello", "Says hello from the mod.")]
    public static void HelloCommand()
    {
        Debug.Log("Hello from MyMod!");
    }
}
```

**Example 2: External Mod Loader**
If you have a dedicated ModLoader script that loads DLLs from a folder:

```csharp
public void LoadMod(string path)
{
    // Load the DLL
    Assembly modAssembly = Assembly.LoadFrom(path);

    // Register its commands into the console
    UniTerminal.DiscoverCommandsInAssembly(modAssembly);
}
```

> **Note:** Do not call this for your main game assembly (`Assembly-CSharp`) if you are using the default setup, as those commands are already optimized and cached. This method is specifically for code loaded **after** the game has started.
