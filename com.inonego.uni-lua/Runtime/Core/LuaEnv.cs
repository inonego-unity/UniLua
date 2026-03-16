using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;

using AOT;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// High-level wrapper for a Lua 5.5 state.
   /// Provides DoString, DoFile, RegisterFunction, Call, and
   /// automatic type conversion between C# and Lua.
   /// </summary>
   // ============================================================
   public class LuaEnv : IDisposable
   {

   #region Fields

      // ------------------------------------------------------------
      /// <summary>
      /// Raw pointer to the underlying lua_State.
      /// </summary>
      // ------------------------------------------------------------
      public IntPtr RawState => rawState;

      private IntPtr rawState = IntPtr.Zero;

      // ------------------------------------------------------------
      /// <summary>
      /// Current number of elements on the Lua stack.
      /// </summary>
      // ------------------------------------------------------------
      public int Top => LuaNative.lua_gettop(rawState);

      // ------------------------------------------------------------
      /// <summary>
      /// Whether this instance has been disposed.
      /// </summary>
      // ------------------------------------------------------------
      public bool IsDisposed => rawState == IntPtr.Zero;

      // Prevent GC from collecting delegates passed to Lua
      private readonly List<LuaNative.lua_CFunction> registeredDelegates = new List<LuaNative.lua_CFunction>();

      // Map from lua_State pointer to LuaEnv instance for static callbacks
      private static readonly Dictionary<IntPtr, LuaEnv> instances = new Dictionary<IntPtr, LuaEnv>();

      // Reflection bridge for CS. access
      private ObjectBridge bridge = null;

   #endregion

   #region Constructors

      public LuaEnv()
      {
         rawState = LuaNative.luaL_newstate();

         if (rawState == IntPtr.Zero)
         {
            throw new LuaException("Failed to create Lua state.");
         }

         instances[rawState] = this;

         LuaNative.luaL_openlibs(rawState);

         // Override print to redirect to Debug.Log
         RegisterStaticFunction("print", PrintCallback);

         // Initialize reflection bridge (CS. table)
         bridge = new ObjectBridge(this);
         bridge.Initialize(rawState);
      }

   #endregion

   #region Dispose

      ~LuaEnv()
      {
         Dispose(false);
      }

      public void Dispose()
      {
         Dispose(true);
         GC.SuppressFinalize(this);
      }

      protected virtual void Dispose(bool disposing)
      {
         if (rawState == IntPtr.Zero)
         {
            return;
         }

         if (disposing)
         {
            bridge?.Dispose();
            bridge = null;
            registeredDelegates.Clear();
         }

         instances.Remove(rawState);
         LuaNative.lua_close(rawState);
         rawState = IntPtr.Zero;
      }

   #endregion

   #region Execute

      // ------------------------------------------------------------
      /// <summary>
      /// Execute a Lua code string.
      /// </summary>
      // ------------------------------------------------------------
      public object[] DoString(string code)
      {
         ThrowIfDisposed();

         int oldTop = Top;
         int result = LuaNative.luaL_dostring(rawState, code);

         if (result != LuaConst.LUA_OK)
         {
            string error = PopString();
            throw new LuaException(result, error);
         }

         return PopResults(oldTop);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Execute a Lua file.
      /// </summary>
      // ------------------------------------------------------------
      public object[] DoFile(string path)
      {
         ThrowIfDisposed();

         int oldTop = Top;
         int result = LuaNative.luaL_dofile(rawState, path);

         if (result != LuaConst.LUA_OK)
         {
            string error = PopString();
            throw new LuaException(result, error);
         }

         return PopResults(oldTop);
      }

   #endregion

   #region Function Registration

      // ------------------------------------------------------------
      /// <summary>
      /// Register a C# function as a global Lua function.
      /// The callback receives this LuaEnv and returns the number
      /// of return values pushed onto the stack.
      /// </summary>
      // ------------------------------------------------------------
      public void RegisterFunction(string name, Func<LuaEnv, int> callback)
      {
         ThrowIfDisposed();

         var env = this;

         LuaNative.lua_CFunction wrapper = (IntPtr L) =>
         {
            try
            {
               return callback(env);
            }
            catch (Exception e)
            {
               Debug.LogError($"[Lua] Error in '{name}': {e.Message}");
               return 0;
            }
         };

         registeredDelegates.Add(wrapper);

         LuaNative.lua_register(rawState, name, wrapper);
      }

   #endregion

   #region Function Call

      // ------------------------------------------------------------
      /// <summary>
      /// Call a global Lua function by name with arguments.
      /// Returns all results from the function.
      /// </summary>
      // ------------------------------------------------------------
      public object[] Call(string functionName, params object[] args)
      {
         ThrowIfDisposed();

         int oldTop = Top;

         int type = LuaNative.lua_getglobal(rawState, functionName);

         if (!LuaNative.lua_isfunction(rawState, -1))
         {
            LuaNative.lua_pop(rawState, 1);
            throw new LuaException($"'{functionName}' is not a function.");
         }

         if (args != null)
         {
            foreach (var arg in args)
            {
               Push(arg);
            }
         }

         int argCount = args?.Length ?? 0;
         int result = LuaNative.lua_pcall(rawState, argCount, LuaConst.LUA_MULTRET, 0);

         if (result != LuaConst.LUA_OK)
         {
            string error = PopString();
            throw new LuaException(result, error);
         }

         return PopResults(oldTop);
      }

   #endregion

   #region Global Variables

      // ------------------------------------------------------------
      /// <summary>
      /// Set a global variable in Lua.
      /// </summary>
      // ------------------------------------------------------------
      public void SetGlobal(string name, object value)
      {
         ThrowIfDisposed();

         Push(value);
         LuaNative.lua_setglobal(rawState, name);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Get a global variable from Lua.
      /// </summary>
      // ------------------------------------------------------------
      public object GetGlobal(string name)
      {
         ThrowIfDisposed();

         LuaNative.lua_getglobal(rawState, name);

         return PopValue();
      }

   #endregion

   #region Stack Push

      // ------------------------------------------------------------
      /// <summary>
      /// Push a C# object onto the Lua stack with automatic type
      /// conversion.
      /// </summary>
      // ------------------------------------------------------------
      public void Push(object value)
      {
         ThrowIfDisposed();

         if (value == null)
         {
            LuaNative.lua_pushnil(rawState);
            return;
         }

         switch (value)
         {
            case bool b:
               LuaNative.lua_pushboolean(rawState, b ? 1 : 0);
               break;

            case int i:
               LuaNative.lua_pushinteger(rawState, i);
               break;

            case long l:
               LuaNative.lua_pushinteger(rawState, l);
               break;

            case float f:
               LuaNative.lua_pushnumber(rawState, f);
               break;

            case double d:
               LuaNative.lua_pushnumber(rawState, d);
               break;

            case string s:
               LuaNative.lua_pushstring(rawState, s);
               break;

            default:
               bridge.PushObject(rawState, value);
               break;
         }
      }

   #endregion

   #region Stack Read

      // ------------------------------------------------------------
      /// <summary>
      /// Read a boolean from the Lua stack at the given index.
      /// </summary>
      // ------------------------------------------------------------
      public bool ToBoolean(int index)
      {
         return LuaNative.lua_toboolean(rawState, index) != 0;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Read an integer from the Lua stack at the given index.
      /// </summary>
      // ------------------------------------------------------------
      public long ToInteger(int index)
      {
         return LuaNative.lua_tointegerx(rawState, index, out _);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Read a number from the Lua stack at the given index.
      /// </summary>
      // ------------------------------------------------------------
      public double ToNumber(int index)
      {
         return LuaNative.lua_tonumberx(rawState, index, out _);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Read a string from the Lua stack at the given index.
      /// </summary>
      // ------------------------------------------------------------
      public string ToString(int index)
      {
         IntPtr ptr = LuaNative.lua_tolstring(rawState, index, out IntPtr len);

         if (ptr == IntPtr.Zero)
         {
            return null;
         }

         int length = (int)(long)len;
         byte[] buffer = new byte[length];
         Marshal.Copy(ptr, buffer, 0, length);

         return Encoding.UTF8.GetString(buffer);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Read a value from the Lua stack at the given index,
      /// converting to the appropriate C# type.
      /// </summary>
      // ------------------------------------------------------------
      public object ToObject(int index)
      {
         int type = LuaNative.lua_type(rawState, index);

         switch (type)
         {
            case LuaConst.LUA_TNIL:
            case LuaConst.LUA_TNONE:
               return null;

            case LuaConst.LUA_TBOOLEAN:
               return ToBoolean(index);

            case LuaConst.LUA_TNUMBER:
               if (LuaNative.lua_isinteger(rawState, index) != 0)
               {
                  return ToInteger(index);
               }
               return ToNumber(index);

            case LuaConst.LUA_TSTRING:
               return ToString(index);

            case LuaConst.LUA_TUSERDATA:
               return bridge.ToCSObject(rawState, index);

            default:
               string typeName = Marshal.PtrToStringAnsi(LuaNative.lua_typename(rawState, type));
               return $"[{typeName}]";
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Get the Lua type of the value at the given stack index.
      /// </summary>
      // ------------------------------------------------------------
      public int TypeAt(int index)
      {
         return LuaNative.lua_type(rawState, index);
      }

   #endregion

   #region Internal

      private void ThrowIfDisposed()
      {
         if (rawState == IntPtr.Zero)
         {
            throw new ObjectDisposedException(nameof(LuaEnv));
         }
      }

      private string PopString()
      {
         string value = ToString(-1);
         LuaNative.lua_pop(rawState, 1);
         return value;
      }

      private object PopValue()
      {
         object value = ToObject(-1);
         LuaNative.lua_pop(rawState, 1);
         return value;
      }

      private object[] PopResults(int oldTop)
      {
         int newTop = Top;
         int count = newTop - oldTop;

         if (count <= 0)
         {
            return Array.Empty<object>();
         }

         var results = new object[count];

         for (int i = 0; i < count; i++)
         {
            results[i] = ToObject(oldTop + 1 + i);
         }

         LuaNative.lua_pop(rawState, count);

         return results;
      }

      private void RegisterStaticFunction(string name, LuaNative.lua_CFunction callback)
      {
         registeredDelegates.Add(callback);
         LuaNative.lua_register(rawState, name, callback);
      }

   #endregion

   #region Print Callback

      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int PrintCallback(IntPtr L)
      {
         if (!instances.TryGetValue(L, out var env))
         {
            return 0;
         }

         int argCount = LuaNative.lua_gettop(L);
         var sb = new StringBuilder();

         for (int i = 1; i <= argCount; i++)
         {
            if (i > 1)
            {
               sb.Append('\t');
            }

            int type = LuaNative.lua_type(L, i);

            switch (type)
            {
               case LuaConst.LUA_TNIL:
                  sb.Append("nil");
                  break;

               case LuaConst.LUA_TBOOLEAN:
                  sb.Append(LuaNative.lua_toboolean(L, i) != 0 ? "true" : "false");
                  break;

               case LuaConst.LUA_TNUMBER:
                  if (LuaNative.lua_isinteger(L, i) != 0)
                  {
                     sb.Append(LuaNative.lua_tointegerx(L, i, out _));
                  }
                  else
                  {
                     sb.Append(LuaNative.lua_tonumberx(L, i, out _));
                  }
                  break;

               case LuaConst.LUA_TUSERDATA:
                  var obj = ObjectWrapper.GetObject(L, i);
                  sb.Append(obj?.ToString() ?? "nil");
                  break;

               default:
                  sb.Append(env.ToString(i) ?? "nil");
                  break;
            }
         }

         Debug.Log(sb.ToString());

         return 0;
      }

   #endregion

   }
}
