# Changelog

## [0.1.0] - 2026-03-16

### Added
- Lua 5.5.0 native library build and placement
- P/Invoke binding layer for Lua C API
  - `LuaNative`: State, execution, stack, table, global, reference API
  - `LuaConst`: Lua constants
- High-level C# API
  - `LuaEnv`: IDisposable wrapper (DoString, DoFile, RegisterFunction, Call)
  - `LuaException`: Exception with error code
  - `LuaTable`: Table reference and manipulation
  - `LuaFunction`: Function reference and invocation
