using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

using UnityEngine;

using AOT;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// Represents a resolved C# Type as a Lua table.
   /// Provides __call (constructor), __index (static members),
   /// and __newindex (static property/field setters).
   /// </summary>
   // ============================================================
   internal static class TypeProxy
   {

   #region Push

      // ------------------------------------------------------------
      /// <summary>
      /// Push a type proxy table onto the Lua stack.
      /// </summary>
      // ------------------------------------------------------------
      public static void PushTypeTable(IntPtr L, ObjectBridge bridge, Type type)
      {
         // Check if already an enum
         if (type.IsEnum)
         {
            EnumWrapper.PushEnumTable(L, bridge, type);
            return;
         }

         LuaNative.lua_newtable(L);

         // Store Type reference as lightuserdata
         var handle = bridge.TrackHandle(type);
         LuaNative.lua_pushstring(L, "__cstype");
         LuaNative.lua_pushlightuserdata(L, GCHandle.ToIntPtr(handle));
         LuaNative.lua_rawset(L, -3);

         // Create metatable
         LuaNative.lua_newtable(L);

         LuaNative.lua_pushstring(L, "__call");
         LuaNative.lua_pushcfunction(L, TypeCall);
         LuaNative.lua_rawset(L, -3);

         LuaNative.lua_pushstring(L, "__index");
         LuaNative.lua_pushcfunction(L, TypeIndex);
         LuaNative.lua_rawset(L, -3);

         LuaNative.lua_pushstring(L, "__newindex");
         LuaNative.lua_pushcfunction(L, TypeNewIndex);
         LuaNative.lua_rawset(L, -3);

         LuaNative.lua_setmetatable(L, -2);
      }

   #endregion

   #region Helpers

      private static Type GetTypeFromTable(IntPtr L, int index)
      {
         LuaNative.lua_getfield(L, index, "__cstype");

         if (LuaNative.lua_type(L, -1) != LuaConst.LUA_TLIGHTUSERDATA)
         {
            LuaNative.lua_pop(L, 1);
            return null;
         }

         IntPtr ptr = LuaNative.lua_touserdata(L, -1);
         LuaNative.lua_pop(L, 1);

         if (ptr == IntPtr.Zero)
         {
            return null;
         }

         try
         {
            var handle = GCHandle.FromIntPtr(ptr);
            return handle.Target as Type;
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
      /// __call: constructor invocation.
      /// CS.UnityEngine.GameObject("name") calls this.
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int TypeCall(IntPtr L)
      {
         try
         {
            var bridge = ObjectBridge.GetBridge(L);

            if (bridge == null)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            // arg 1 is the type table itself (from __call)
            Type type = GetTypeFromTable(L, 1);

            if (type == null)
            {
               Debug.LogError("[UniLua] Invalid type proxy in __call");
               LuaNative.lua_pushnil(L);
               return 1;
            }

            // Read constructor arguments (starting at arg 2)
            int argCount = LuaNative.lua_gettop(L) - 1;
            var argTypes = new Type[argCount];
            var args = new object[argCount];

            for (int i = 0; i < argCount; i++)
            {
               args[i] = bridge.ToCSObject(L, i + 2);
               argTypes[i] = args[i]?.GetType();
            }

            // Resolve constructor
            var ctor = bridge.ReflectionCache.ResolveConstructor(type, argTypes);

            if (ctor == null)
            {
               Debug.LogError($"[UniLua] No matching constructor for '{type.Name}' with {argCount} arguments");
               LuaNative.lua_pushnil(L);
               return 1;
            }

            // Convert arguments
            var parameters = ctor.GetParameters();

            for (int i = 0; i < args.Length; i++)
            {
               var paramType = parameters[i].ParameterType;

               if (args[i] != null && args[i].GetType() != paramType)
               {
                  args[i] = bridge.ToCSObject(L, i + 2, paramType);
               }
            }

            // Handle optional parameters
            if (args.Length < parameters.Length)
            {
               var fullArgs = new object[parameters.Length];
               Array.Copy(args, fullArgs, args.Length);

               for (int i = args.Length; i < parameters.Length; i++)
               {
                  fullArgs[i] = parameters[i].DefaultValue;
               }

               args = fullArgs;
            }

            // Invoke constructor
            object instance = ctor.Invoke(args);

            bridge.PushObject(L, instance);
            return 1;
         }
         catch (TargetInvocationException e)
         {
            Debug.LogError($"[UniLua] Constructor error: {e.InnerException?.Message ?? e.Message}");
            LuaNative.lua_pushnil(L);
            return 1;
         }
         catch (Exception e)
         {
            Debug.LogError($"[UniLua] Constructor error: {e.Message}");
            LuaNative.lua_pushnil(L);
            return 1;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// __index: static member access.
      /// CS.UnityEngine.GameObject.Find calls this.
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int TypeIndex(IntPtr L)
      {
         try
         {
            var bridge = ObjectBridge.GetBridge(L);

            if (bridge == null)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            Type type = GetTypeFromTable(L, 1);

            if (type == null)
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

            if (!bridge.ReflectionCache.TryGetStaticMember(type, key, out var member))
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            switch (member.Kind)
            {
               case MemberKind.Property:
                  object propVal = member.Property.GetValue(null);
                  bridge.PushObject(L, propVal);

                  // Cache constant-like properties
                  LuaNative.lua_pushvalue(L, 2);
                  LuaNative.lua_pushvalue(L, -2);
                  LuaNative.lua_rawset(L, 1);
                  return 1;

               case MemberKind.Field:
                  object fieldVal = member.Field.GetValue(null);
                  bridge.PushObject(L, fieldVal);

                  // Cache static readonly/const fields
                  if (member.Field.IsInitOnly || member.Field.IsLiteral)
                  {
                     LuaNative.lua_pushvalue(L, 2);
                     LuaNative.lua_pushvalue(L, -2);
                     LuaNative.lua_rawset(L, 1);
                  }
                  return 1;

               case MemberKind.Method:
                  MethodWrapper.PushMethodClosure(L, bridge, null, member.Methods);

                  // Cache method closure
                  LuaNative.lua_pushvalue(L, 2);
                  LuaNative.lua_pushvalue(L, -2);
                  LuaNative.lua_rawset(L, 1);
                  return 1;

               case MemberKind.NestedType:
                  TypeProxy.PushTypeTable(L, bridge, member.NestedType);

                  // Cache nested type
                  LuaNative.lua_pushvalue(L, 2);
                  LuaNative.lua_pushvalue(L, -2);
                  LuaNative.lua_rawset(L, 1);
                  return 1;

               default:
                  LuaNative.lua_pushnil(L);
                  return 1;
            }
         }
         catch (Exception e)
         {
            Debug.LogError($"[UniLua] TypeIndex error: {e.Message}");
            LuaNative.lua_pushnil(L);
            return 1;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// __newindex: static property/field setter.
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int TypeNewIndex(IntPtr L)
      {
         try
         {
            var bridge = ObjectBridge.GetBridge(L);

            if (bridge == null)
            {
               return 0;
            }

            Type type = GetTypeFromTable(L, 1);

            if (type == null)
            {
               return 0;
            }

            string key = bridge.Env.ToString(2);

            if (!bridge.ReflectionCache.TryGetStaticMember(type, key, out var member))
            {
               return 0;
            }

            switch (member.Kind)
            {
               case MemberKind.Property:
                  if (member.Property.CanWrite)
                  {
                     var propValue = bridge.ToCSObject(L, 3, member.Property.PropertyType);
                     member.Property.SetValue(null, propValue);
                  }
                  break;

               case MemberKind.Field:
                  if (!member.Field.IsInitOnly && !member.Field.IsLiteral)
                  {
                     var fieldValue = bridge.ToCSObject(L, 3, member.Field.FieldType);
                     member.Field.SetValue(null, fieldValue);
                  }
                  break;
            }
         }
         catch (Exception e)
         {
            Debug.LogError($"[UniLua] TypeNewIndex error: {e.Message}");
         }

         return 0;
      }

   #endregion

   }
}
