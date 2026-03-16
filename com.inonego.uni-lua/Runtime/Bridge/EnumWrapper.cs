using System;
using System.Runtime.InteropServices;

using AOT;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// Handles enum types in Lua. Enum values are exposed as
   /// integer fields on the type table.
   /// CS.UnityEngine.KeyCode.Space → pushes integer 32.
   /// </summary>
   // ============================================================
   internal static class EnumWrapper
   {

   #region Push

      // ------------------------------------------------------------
      /// <summary>
      /// Push an enum type table onto the Lua stack with all
      /// enum values pre-populated as integer fields.
      /// </summary>
      // ------------------------------------------------------------
      public static void PushEnumTable(IntPtr L, ObjectBridge bridge, Type enumType)
      {
         var names = Enum.GetNames(enumType);
         var values = Enum.GetValues(enumType);

         LuaNative.lua_createtable(L, 0, names.Length + 1);

         // Store Type reference
         var handle = bridge.TrackHandle(enumType);
         LuaNative.lua_pushstring(L, "__cstype");
         LuaNative.lua_pushlightuserdata(L, GCHandle.ToIntPtr(handle));
         LuaNative.lua_rawset(L, -3);

         // Populate all enum values
         for (int i = 0; i < names.Length; i++)
         {
            LuaNative.lua_pushstring(L, names[i]);
            LuaNative.lua_pushinteger(L, Convert.ToInt64(values.GetValue(i)));
            LuaNative.lua_rawset(L, -3);
         }

         // Metatable with __call for int → enum conversion
         LuaNative.lua_newtable(L);

         LuaNative.lua_pushstring(L, "__call");
         LuaNative.lua_pushcfunction(L, EnumCall);
         LuaNative.lua_rawset(L, -3);

         LuaNative.lua_setmetatable(L, -2);
      }

   #endregion

   #region Metamethods

      // ------------------------------------------------------------
      /// <summary>
      /// __call: convert an integer to an enum value.
      /// local code = CS.UnityEngine.KeyCode(32) → KeyCode.Space
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int EnumCall(IntPtr L)
      {
         try
         {
            var bridge = ObjectBridge.GetBridge(L);

            if (bridge == null)
            {
               return 0;
            }

            // arg 1 = enum table, arg 2 = integer value
            LuaNative.lua_getfield(L, 1, "__cstype");

            if (LuaNative.lua_type(L, -1) != LuaConst.LUA_TLIGHTUSERDATA)
            {
               LuaNative.lua_pop(L, 1);
               LuaNative.lua_pushnil(L);
               return 1;
            }

            IntPtr ptr = LuaNative.lua_touserdata(L, -1);
            LuaNative.lua_pop(L, 1);

            if (ptr == IntPtr.Zero)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            var handle = GCHandle.FromIntPtr(ptr);
            var enumType = handle.Target as Type;

            if (enumType == null)
            {
               LuaNative.lua_pushnil(L);
            return 1;
         }

            long value = LuaNative.lua_tointegerx(L, 2, out _);
            var enumValue = Enum.ToObject(enumType, value);

            // Push enum name as string
            LuaNative.lua_pushstring(L, enumValue.ToString());
            return 1;
         }
         catch (System.Exception e)
         {
            UnityEngine.Debug.LogError($"[UniLua] EnumCall error: {e.Message}");
            LuaNative.lua_pushnil(L);
            return 1;
         }
      }

   #endregion

   }
}
