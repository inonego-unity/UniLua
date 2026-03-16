<p align="center">
  <h1 align="center">UniLua</h1>
  <p align="center">
    Lua 5.5 Scripting for Unity via Native P/Invoke
  </p>
  <p align="center">
    <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
    <img src="https://img.shields.io/badge/Unity-6-blue?logo=unity" alt="Unity 6">
    <img src="https://img.shields.io/badge/Lua-5.5-blue?logo=lua" alt="Lua 5.5">
  </p>
  <p align="center">
    <b>English</b> | <a href="README.ko.md">한국어</a>
  </p>
</p>

---

UniLua brings Lua 5.5 scripting to Unity through native P/Invoke bindings. Execute Lua scripts, register C# functions, and access any C# type from Lua via the built-in reflection bridge.

> **Note**: The current release includes a pre-built native library for **Windows x64** only. Other platforms require building Lua 5.5 from source.

## Features

- **Lua 5.5.0** native library (built from official source)
- **P/Invoke bindings** for the full Lua C API
- **High-level C# API** — `LuaEnv`, `LuaTable`, `LuaFunction`
- **CS. reflection bridge** — access any C# type from Lua dynamically
  - Namespace chaining: `CS.UnityEngine.GameObject`
  - Constructors, static/instance methods, properties, fields
  - Enum support
  - Reflection caching for performance
- **Unity integration** — `print()` redirects to `Debug.Log`
- **`.lua` ScriptedImporter** — .lua files as TextAsset in the Editor
- **IL2CPP compatible**

## Installation

In Unity: **Window > Package Manager > + > Add package from git URL...**

```
https://github.com/inonego-unity/UniLua.git?path=com.inonego.uni-lua
```

Or add directly to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.inonego.uni-lua": "https://github.com/inonego-unity/UniLua.git?path=com.inonego.uni-lua"
  }
}
```

## Quick Start

### Basic Lua Execution

```csharp
using inonego.UniLua;

var lua = new LuaEnv();

lua.DoString("print('Hello from Lua!')");

lua.SetGlobal("playerName", "Player1");
lua.DoString("print('Name: ' .. playerName)");

lua.Dispose();
```

### Register C# Functions

```csharp
var lua = new LuaEnv();

lua.RegisterFunction("Heal", (env) =>
{
    double amount = env.ToNumber(1);
    Debug.Log($"Healed {amount} HP!");
    return 0;
});

lua.DoString("Heal(50)");
```

### Call Lua Functions from C#

```csharp
var lua = new LuaEnv();

lua.DoString("function Add(a, b) return a + b end");
object[] result = lua.Call("Add", 10, 20);
// result[0] = 30
```

### CS. Reflection Bridge

Access any C# type directly from Lua without manual registration:

```lua
-- Static method
CS.UnityEngine.Debug.Log("Hello from Lua!")

-- Constructor
local go = CS.UnityEngine.GameObject("MyObject")

-- Instance property / method
print(go.name)
go:SetActive(false)

-- Static method
local found = CS.UnityEngine.GameObject.Find("MyObject")

-- Cleanup
CS.UnityEngine.Object.Destroy(go)

-- Enum
local space = CS.UnityEngine.KeyCode.Space

-- Any C# class works
local custom = CS.MyGame.EnemyController()
```

## API Reference

### LuaEnv

| Method | Description |
|--------|-------------|
| `DoString(code)` | Execute a Lua code string |
| `DoFile(path)` | Execute a Lua file |
| `Call(name, args...)` | Call a global Lua function |
| `RegisterFunction(name, callback)` | Register a C# function in Lua |
| `SetGlobal(name, value)` | Set a global variable |
| `GetGlobal(name)` | Get a global variable |
| `Push(value)` | Push a value onto the Lua stack |
| `ToObject(index)` | Read a value from the Lua stack |
| `Dispose()` | Close the Lua state and release resources |

### LuaTable

| Method | Description |
|--------|-------------|
| `Get(key)` | Get a value by string or integer key |
| `Set(key, value)` | Set a value by string or integer key |
| `Length` | Raw length of the table (# operator) |
| `ForEach(callback)` | Iterate all key-value pairs |

### LuaFunction

| Method | Description |
|--------|-------------|
| `Call(args...)` | Invoke the Lua function |

## Project Structure

```
Runtime/
├── Native/
│   ├── LuaConst.cs           # Lua 5.5 constants
│   └── LuaNative.cs          # P/Invoke declarations
├── Core/
│   ├── LuaEnv.cs             # High-level Lua state wrapper
│   ├── LuaException.cs       # Exception with error code
│   ├── LuaTable.cs           # Table reference wrapper
│   └── LuaFunction.cs        # Function reference wrapper
├── Bridge/
│   ├── ReflectionCache.cs    # Type/member/overload/delegate caching
│   ├── ObjectBridge.cs       # Central CS. orchestrator
│   ├── NamespaceProxy.cs     # CS.Namespace.Type chaining
│   ├── TypeProxy.cs          # Static members + constructor
│   ├── ObjectWrapper.cs      # Instance userdata + metamethods
│   ├── MethodWrapper.cs      # Method invocation + overloads
│   └── EnumWrapper.cs        # Enum value access
└── Plugins/Lua/
    └── Windows/x86_64/lua55.dll
Editor/
└── LuaScriptImporter.cs      # .lua ScriptedImporter
```

## Known Limitations

- **IL2CPP stripping**: Types accessed only via reflection may need `[Preserve]` or `link.xml`
- **Value type boxing**: Structs (Vector3, etc.) are boxed when wrapped as userdata
- **Generic methods**: `MakeGenericMethod` has limitations under IL2CPP for value types
- **Windows x64 only** (currently): Other platforms require building lua55 from source

## License

[MIT](LICENSE)
