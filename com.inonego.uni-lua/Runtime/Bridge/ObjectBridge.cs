using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

using AOT;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// Central orchestrator for the CS. reflection bridge.
   /// Manages C# object lifecycle in Lua, GCHandle tracking,
   /// and connects all bridge components.
   /// One instance per LuaEnv.
   /// </summary>
   // ============================================================
   internal class ObjectBridge : IDisposable
   {

   #region Fields

      public LuaEnv Env => env;
      private readonly LuaEnv env = null;

      public ReflectionCache ReflectionCache => reflectionCache;
      private readonly ReflectionCache reflectionCache = new ReflectionCache();

      private readonly List<GCHandle> activeHandles = new List<GCHandle>();

      private static readonly Dictionary<IntPtr, ObjectBridge> instances = new Dictionary<IntPtr, ObjectBridge>();

   #endregion

   #region Constructors

      public ObjectBridge(LuaEnv env)
      {
         this.env = env ?? throw new ArgumentNullException(nameof(env));
      }

   #endregion

   #region Initialize

      // ------------------------------------------------------------
      /// <summary>
      /// Set up the CS global table with namespace resolution.
      /// </summary>
      // ------------------------------------------------------------
      public void Initialize(IntPtr L)
      {
         instances[L] = this;
         NamespaceProxy.SetupCSTable(L, this);
      }

   #endregion

   #region Dispose

      public void Dispose()
      {
         if (env.RawState != IntPtr.Zero)
         {
            instances.Remove(env.RawState);
         }

         for (int i = 0; i < activeHandles.Count; i++)
         {
            if (activeHandles[i].IsAllocated)
            {
               activeHandles[i].Free();
            }
         }

         activeHandles.Clear();
      }

   #endregion

   #region Instance Lookup

      // ------------------------------------------------------------
      /// <summary>
      /// Get the ObjectBridge for a lua_State pointer.
      /// Used by static [MonoPInvokeCallback] methods.
      /// </summary>
      // ------------------------------------------------------------
      public static ObjectBridge GetBridge(IntPtr L)
      {
         instances.TryGetValue(L, out var bridge);
         return bridge;
      }

   #endregion

   #region Push Object

      // ------------------------------------------------------------
      /// <summary>
      /// Push any C# object onto the Lua stack.
      /// Handles null, primitives, enums, Type, and arbitrary
      /// objects via userdata wrapping.
      /// </summary>
      // ------------------------------------------------------------
      public void PushObject(IntPtr L, object obj)
      {
         // Null
         if (obj == null)
         {
            LuaNative.lua_pushnil(L);
            return;
         }

         // Unity fake null
         if (obj is UnityEngine.Object unityObj && unityObj == null)
         {
            LuaNative.lua_pushnil(L);
            return;
         }

         // Primitives
         switch (obj)
         {
            case bool b:
               LuaNative.lua_pushboolean(L, b ? 1 : 0);
               return;

            case int i:
               LuaNative.lua_pushinteger(L, i);
               return;

            case long l:
               LuaNative.lua_pushinteger(L, l);
               return;

            case float f:
               LuaNative.lua_pushnumber(L, f);
               return;

            case double d:
               LuaNative.lua_pushnumber(L, d);
               return;

            case string s:
               LuaNative.lua_pushstring(L, s);
               return;
         }

         // Enum → integer
         if (obj.GetType().IsEnum)
         {
            LuaNative.lua_pushinteger(L, Convert.ToInt64(obj));
            return;
         }

         // Type → TypeProxy table
         if (obj is Type type)
         {
            TypeProxy.PushTypeTable(L, this, type);
            return;
         }

         // Any other object → userdata
         ObjectWrapper.PushObject(L, this, obj);
      }

   #endregion

   #region Read Object

      // ------------------------------------------------------------
      /// <summary>
      /// Read a C# object from the Lua stack at the given index.
      /// Handles all Lua types including userdata.
      /// </summary>
      // ------------------------------------------------------------
      public object ToCSObject(IntPtr L, int index, Type expectedType = null)
      {
         int luaType = LuaNative.lua_type(L, index);

         switch (luaType)
         {
            case LuaConst.LUA_TNIL:
            case LuaConst.LUA_TNONE:
               return null;

            case LuaConst.LUA_TBOOLEAN:
               return LuaNative.lua_toboolean(L, index) != 0;

            case LuaConst.LUA_TNUMBER:
               return ReadNumber(L, index, expectedType);

            case LuaConst.LUA_TSTRING:
               return env.ToString(index);

            case LuaConst.LUA_TUSERDATA:
               return ObjectWrapper.GetObject(L, index);

            case LuaConst.LUA_TTABLE:
               return ReadTable(L, index);

            default:
               return null;
         }
      }

      private object ReadNumber(IntPtr L, int index, Type expectedType)
      {
         if (LuaNative.lua_isinteger(L, index) != 0)
         {
            long value = LuaNative.lua_tointegerx(L, index, out _);

            // Convert to expected enum type
            if (expectedType != null && expectedType.IsEnum)
            {
               return Enum.ToObject(expectedType, value);
            }

            // Convert to expected numeric type
            if (expectedType == typeof(int))   return (int)value;
            if (expectedType == typeof(float)) return (float)value;
            if (expectedType == typeof(byte))  return (byte)value;
            if (expectedType == typeof(short)) return (short)value;

            return value;
         }

         double dval = LuaNative.lua_tonumberx(L, index, out _);

         if (expectedType == typeof(float)) return (float)dval;
         if (expectedType == typeof(int))   return (int)dval;

         return dval;
      }

      private object ReadTable(IntPtr L, int index)
      {
         // Check if this table is a TypeProxy (has __cstype)
         LuaNative.lua_getfield(L, index, "__cstype");

         if (LuaNative.lua_type(L, -1) == LuaConst.LUA_TLIGHTUSERDATA)
         {
            IntPtr ptr = LuaNative.lua_touserdata(L, -1);
            LuaNative.lua_pop(L, 1);

            if (ptr != IntPtr.Zero)
            {
               var handle = GCHandle.FromIntPtr(ptr);
               return handle.Target as Type;
            }
         }

         LuaNative.lua_pop(L, 1);
         return null;
      }

   #endregion

   #region GCHandle Management

      // ------------------------------------------------------------
      /// <summary>
      /// Allocate a tracked GCHandle for an object.
      /// </summary>
      // ------------------------------------------------------------
      public GCHandle TrackHandle(object obj)
      {
         var handle = GCHandle.Alloc(obj);
         activeHandles.Add(handle);
         return handle;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Free a GCHandle and remove from tracking.
      /// </summary>
      // ------------------------------------------------------------
      public void ReleaseHandle(GCHandle handle)
      {
         if (handle.IsAllocated)
         {
            handle.Free();
         }

         activeHandles.Remove(handle);
      }

   #endregion

   }
}
