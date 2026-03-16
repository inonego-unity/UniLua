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
   /// Wraps C# object instances as Lua full userdata with
   /// per-type metatables. Handles __index, __newindex, __gc,
   /// __tostring, and __eq metamethods.
   /// </summary>
   // ============================================================
   internal static class ObjectWrapper
   {

      private const string METATABLE_PREFIX = "CS:";

   #region Push / Get

      // ------------------------------------------------------------
      /// <summary>
      /// Push a C# object onto the Lua stack as userdata.
      /// Creates per-type metatable on first use.
      /// </summary>
      // ------------------------------------------------------------
      public static void PushObject(IntPtr L, ObjectBridge bridge, object obj)
      {
         // Allocate userdata (size = IntPtr.Size)
         IntPtr ud = LuaNative.lua_newuserdatauv(L, (IntPtr)IntPtr.Size, 0);

         // Store GCHandle in userdata
         var handle = bridge.TrackHandle(obj);
         Marshal.WriteIntPtr(ud, GCHandle.ToIntPtr(handle));

         // Set per-type metatable
         string mtName = METATABLE_PREFIX + obj.GetType().FullName;

         if (LuaNative.luaL_newmetatable(L, mtName) != 0)
         {
            // First time: set up metamethods
            LuaNative.lua_pushstring(L, "__index");
            LuaNative.lua_pushcfunction(L, ObjectIndex);
            LuaNative.lua_settable(L, -3);

            LuaNative.lua_pushstring(L, "__newindex");
            LuaNative.lua_pushcfunction(L, ObjectNewIndex);
            LuaNative.lua_settable(L, -3);

            LuaNative.lua_pushstring(L, "__gc");
            LuaNative.lua_pushcfunction(L, ObjectGC);
            LuaNative.lua_settable(L, -3);

            LuaNative.lua_pushstring(L, "__tostring");
            LuaNative.lua_pushcfunction(L, ObjectToString);
            LuaNative.lua_settable(L, -3);

            LuaNative.lua_pushstring(L, "__eq");
            LuaNative.lua_pushcfunction(L, ObjectEquals);
            LuaNative.lua_settable(L, -3);
         }

         LuaNative.lua_setmetatable(L, -2);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Extract the C# object from a userdata at the given stack
      /// index. Returns null if not a valid wrapped object.
      /// </summary>
      // ------------------------------------------------------------
      public static object GetObject(IntPtr L, int index)
      {
         if (LuaNative.lua_isuserdata(L, index) == 0)
         {
            return null;
         }

         IntPtr ud = LuaNative.lua_touserdata(L, index);

         if (ud == IntPtr.Zero)
         {
            return null;
         }

         IntPtr handlePtr = Marshal.ReadIntPtr(ud);

         if (handlePtr == IntPtr.Zero)
         {
            return null;
         }

         try
         {
            var handle = GCHandle.FromIntPtr(handlePtr);
            return handle.Target;
         }
         catch
         {
            return null;
         }
      }

   #endregion

   #region Metamethods

      // ------------------------------------------------------------
      /// <summary>
      /// __index: instance member read (methods, properties, fields).
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int ObjectIndex(IntPtr L)
      {
         try
         {
            var bridge = ObjectBridge.GetBridge(L);

            if (bridge == null)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            object obj = GetObject(L, 1);

            if (obj == null)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            // Unity fake null check
            if (obj is UnityEngine.Object unityObj && unityObj == null)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            string key = bridge.Env.ToString(2);

            if (key == null)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            var type = obj.GetType();

            if (!bridge.ReflectionCache.TryGetInstanceMember(type, key, out var member))
            {
               // Fallback to static members
               if (!bridge.ReflectionCache.TryGetStaticMember(type, key, out member))
               {
                  LuaNative.lua_pushnil(L);
                  return 1;
               }
            }

            switch (member.Kind)
            {
               case MemberKind.Property:
                  object propVal = member.Property.GetValue(obj);
                  bridge.PushObject(L, propVal);
                  return 1;

               case MemberKind.Field:
                  object fieldVal = member.Field.GetValue(obj);
                  bridge.PushObject(L, fieldVal);
                  return 1;

               case MemberKind.Method:
                  MethodWrapper.PushMethodClosure(L, bridge, obj, member.Methods);
                  return 1;

               default:
                  LuaNative.lua_pushnil(L);
                  return 1;
            }
         }
         catch (Exception e)
         {
            Debug.LogError($"[UniLua] ObjectIndex error: {e.Message}");
            LuaNative.lua_pushnil(L);
            return 1;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// __newindex: instance member write (properties, fields).
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int ObjectNewIndex(IntPtr L)
      {
         try
         {
            var bridge = ObjectBridge.GetBridge(L);

            if (bridge == null)
            {
               return 0;
            }

            object obj = GetObject(L, 1);

            if (obj == null || (obj is UnityEngine.Object u && u == null))
            {
               return 0;
            }

            string key = bridge.Env.ToString(2);

            if (key == null)
            {
               return 0;
            }

            var type = obj.GetType();

            if (!bridge.ReflectionCache.TryGetInstanceMember(type, key, out var member))
            {
               Debug.LogWarning($"[UniLua] '{type.Name}' has no member '{key}'");
               return 0;
            }

            switch (member.Kind)
            {
               case MemberKind.Property:
                  if (member.Property.CanWrite)
                  {
                     var propValue = bridge.ToCSObject(L, 3, member.Property.PropertyType);
                     member.Property.SetValue(obj, propValue);
                  }
                  break;

               case MemberKind.Field:
                  if (!member.Field.IsInitOnly)
                  {
                     var fieldValue = bridge.ToCSObject(L, 3, member.Field.FieldType);
                     member.Field.SetValue(obj, fieldValue);
                  }
                  break;
            }
         }
         catch (Exception e)
         {
            Debug.LogError($"[UniLua] ObjectNewIndex error: {e.Message}");
         }

         return 0;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// __gc: release GCHandle when Lua garbage collects userdata.
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int ObjectGC(IntPtr L)
      {
         try
         {
            IntPtr ud = LuaNative.lua_touserdata(L, 1);

            if (ud == IntPtr.Zero)
            {
               return 0;
            }

            IntPtr handlePtr = Marshal.ReadIntPtr(ud);

            if (handlePtr == IntPtr.Zero)
            {
               return 0;
            }

            var handle = GCHandle.FromIntPtr(handlePtr);

            if (handle.IsAllocated)
            {
               handle.Free();
            }

            Marshal.WriteIntPtr(ud, IntPtr.Zero);
         }
         catch
         {
            // Suppress GC errors
         }

         return 0;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// __tostring: return obj.ToString() for print friendliness.
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int ObjectToString(IntPtr L)
      {
         try
         {
            object obj = GetObject(L, 1);

            if (obj == null || (obj is UnityEngine.Object u && u == null))
            {
               LuaNative.lua_pushstring(L, "null");
               return 1;
            }

            LuaNative.lua_pushstring(L, obj.ToString());
         }
         catch
         {
            LuaNative.lua_pushstring(L, "error");
         }

         return 1;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// __eq: compare two wrapped C# objects.
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int ObjectEquals(IntPtr L)
      {
         try
         {
            object a = GetObject(L, 1);
            object b = GetObject(L, 2);

            bool equal;

            if (a is UnityEngine.Object ua && b is UnityEngine.Object ub)
            {
               equal = ua == ub;
            }
            else
            {
               equal = Equals(a, b);
            }

            LuaNative.lua_pushboolean(L, equal ? 1 : 0);
         }
         catch
         {
            LuaNative.lua_pushboolean(L, 0);
         }

         return 1;
      }

   #endregion

   }
}
