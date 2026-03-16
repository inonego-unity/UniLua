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
   /// Wraps C# method invocations for Lua, handling overload
   /// resolution, out/ref parameters, and instance vs static calls.
   /// </summary>
   // ============================================================
   internal static class MethodWrapper
   {

      // Storage for method closure data (target + methods)
      // Indexed by registry reference
      private static readonly Dictionary<int, MethodCallData> callDataMap = new Dictionary<int, MethodCallData>();

      private struct MethodCallData
      {
         public object Target;
         public MethodInfo[] Methods;
         public ObjectBridge Bridge;
      }

   #region Push

      // ------------------------------------------------------------
      /// <summary>
      /// Push a method closure onto the Lua stack.
      /// Stores target object and method info as an upvalue via
      /// registry reference.
      /// </summary>
      // ------------------------------------------------------------
      public static void PushMethodClosure(IntPtr L, ObjectBridge bridge, object target, MethodInfo[] methods)
      {
         var data = new MethodCallData
         {
            Target = target,
            Methods = methods,
            Bridge = bridge,
         };

         // Store data and get a unique ID
         LuaNative.lua_pushnil(L);
         int refId = LuaNative.luaL_ref(L, LuaConst.LUA_REGISTRYINDEX);
         callDataMap[refId] = data;

         // Push refId as upvalue
         LuaNative.lua_pushinteger(L, refId);
         LuaNative.lua_pushcclosure(L, CallMethod, 1);
      }

   #endregion

   #region Call

      // ------------------------------------------------------------
      /// <summary>
      /// Static callback for method invocation from Lua.
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int CallMethod(IntPtr L)
      {
         try
         {
            // Get refId from upvalue
            int refId = (int)LuaNative.lua_tointegerx(L, LuaNative.lua_upvalueindex(1), out _);

            if (!callDataMap.TryGetValue(refId, out var data))
            {
               Debug.LogError("[UniLua] Invalid method reference");
               LuaNative.lua_pushnil(L);
               return 1;
            }

            var bridge = data.Bridge;
            var target = data.Target;
            var methods = data.Methods;

            // For instance methods called with ':', arg 1 is self
            bool isInstance = target != null;
            int argStart = isInstance ? 2 : 1;
            int argCount = LuaNative.lua_gettop(L) - argStart + 1;

            if (argCount < 0)
            {
               argCount = 0;
            }

            // If target is from ':' syntax, extract actual target from arg 1
            if (isInstance)
            {
               object self = ObjectWrapper.GetObject(L, 1);

               if (self != null)
               {
                  if (self is UnityEngine.Object unityObj && unityObj == null)
                  {
                     Debug.LogError("[UniLua] Attempt to call method on destroyed Unity object");
                     LuaNative.lua_pushnil(L);
                     return 1;
                  }

                  target = self;
               }
            }

            // Read argument types for overload resolution
            var argTypes = new Type[argCount];
            var args = new object[argCount];

            for (int i = 0; i < argCount; i++)
            {
               int stackIndex = argStart + i;
               args[i] = bridge.ToCSObject(L, stackIndex);
               argTypes[i] = args[i]?.GetType();
            }

            // Resolve overload
            var method = bridge.ReflectionCache.ResolveMethod(methods, argTypes);

            if (method == null)
            {
               string typeName = target?.GetType().Name ?? methods[0].DeclaringType?.Name ?? "unknown";
               Debug.LogError($"[UniLua] No matching overload for '{methods[0].Name}' on '{typeName}' with {argCount} arguments");
               LuaNative.lua_pushnil(L);
               return 1;
            }

            // Convert arguments to match parameter types
            var parameters = method.GetParameters();

            for (int i = 0; i < args.Length; i++)
            {
               var paramType = parameters[i].ParameterType;

               if (paramType.IsByRef)
               {
                  paramType = paramType.GetElementType();
               }

               if (args[i] != null && args[i].GetType() != paramType)
               {
                  args[i] = ConvertArg(bridge, L, argStart + i, args[i], paramType);
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

            // Invoke (uses delegate cache for performance)
            object result = bridge.ReflectionCache.FastInvoke(method, target, args);

            // Push return value
            int returnCount = 0;

            if (method.ReturnType != typeof(void))
            {
               bridge.PushObject(L, result);
               returnCount = 1;
            }

            // Push out/ref parameters as additional return values
            for (int i = 0; i < parameters.Length; i++)
            {
               if (parameters[i].IsOut || parameters[i].ParameterType.IsByRef)
               {
                  bridge.PushObject(L, args[i]);
                  returnCount++;
               }
            }

            return returnCount;
         }
         catch (TargetInvocationException e)
         {
            Debug.LogError($"[UniLua] Method call error: {e.InnerException?.Message ?? e.Message}");
            LuaNative.lua_pushnil(L);
            return 1;
         }
         catch (Exception e)
         {
            Debug.LogError($"[UniLua] Method call error: {e.Message}");
            LuaNative.lua_pushnil(L);
            return 1;
         }
      }

   #endregion

   #region Conversion

      private static object ConvertArg(ObjectBridge bridge, IntPtr L, int stackIndex, object value, Type targetType)
      {
         // Enum conversion from integer
         if (targetType.IsEnum && value is long longVal)
         {
            return Enum.ToObject(targetType, longVal);
         }

         if (targetType.IsEnum && value is int intVal)
         {
            return Enum.ToObject(targetType, intVal);
         }

         // Re-read from stack with expected type
         return bridge.ToCSObject(L, stackIndex, targetType);
      }

   #endregion

   }
}
