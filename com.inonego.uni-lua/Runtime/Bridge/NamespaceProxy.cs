using System;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;

using AOT;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// Handles the CS global table and namespace chaining.
   /// CS.UnityEngine.GameObject resolves through:
   /// CS.__index("UnityEngine") → namespace table
   /// UnityEngine.__index("GameObject") → TypeProxy
   /// Results are cached with rawset for subsequent access.
   /// </summary>
   // ============================================================
   internal static class NamespaceProxy
   {

   #region Setup

      // ------------------------------------------------------------
      /// <summary>
      /// Create the global CS table and set its __index metamethod.
      /// </summary>
      // ------------------------------------------------------------
      public static void SetupCSTable(IntPtr L, ObjectBridge bridge)
      {
         // CS = {}
         LuaNative.lua_newtable(L);

         // metatable = { __index = CSIndex }
         LuaNative.lua_newtable(L);

         // Store empty namespace path as upvalue
         LuaNative.lua_pushstring(L, "");
         LuaNative.lua_pushcclosure(L, NSIndex, 1);

         LuaNative.lua_setfield(L, -2, "__index");

         LuaNative.lua_setmetatable(L, -2);

         // _G.CS = table
         LuaNative.lua_setglobal(L, "CS");
      }

   #endregion

   #region Index

      // ------------------------------------------------------------
      /// <summary>
      /// __index metamethod for namespace tables.
      /// Tries to resolve the key as a type first, then creates
      /// a child namespace table.
      /// </summary>
      // ------------------------------------------------------------
      [MonoPInvokeCallback(typeof(LuaNative.lua_CFunction))]
      private static int NSIndex(IntPtr L)
      {
         try
         {
            var bridge = ObjectBridge.GetBridge(L);

            if (bridge == null)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            // Get namespace path from upvalue
            string nsPath = ReadUpvalueString(L);

            // Get the key being accessed
            string key = bridge.Env.ToString(2);

            if (key == null)
            {
               LuaNative.lua_pushnil(L);
               return 1;
            }

            // Build full candidate name
            string candidate = string.IsNullOrEmpty(nsPath) ? key : nsPath + "." + key;

            // Try to find as a type first
            Type type = bridge.ReflectionCache.FindType(candidate);

            if (type != null)
            {
               // Found a type — push TypeProxy and cache
               TypeProxy.PushTypeTable(L, bridge, type);

               // Cache: table[key] = typeProxy
               LuaNative.lua_pushvalue(L, 2);
               LuaNative.lua_pushvalue(L, -2);
               LuaNative.lua_rawset(L, 1);

               return 1;
            }

            // Not a type — create child namespace table
            PushNamespaceTable(L, candidate);

            // Cache: table[key] = nsTable
            LuaNative.lua_pushvalue(L, 2);
            LuaNative.lua_pushvalue(L, -2);
            LuaNative.lua_rawset(L, 1);

            return 1;
         }
         catch (Exception e)
         {
            Debug.LogError($"[UniLua] NSIndex error: {e.Message}");
            LuaNative.lua_pushnil(L);
            return 1;
         }
      }

   #endregion

   #region Helpers

      private static string ReadUpvalueString(IntPtr L)
      {
         IntPtr ptr = LuaNative.lua_tolstring(L, LuaNative.lua_upvalueindex(1), out IntPtr len);

         if (ptr == IntPtr.Zero)
         {
            return "";
         }

         int length = (int)(long)len;

         if (length == 0)
         {
            return "";
         }

         byte[] buffer = new byte[length];
         Marshal.Copy(ptr, buffer, 0, length);
         return Encoding.UTF8.GetString(buffer);
      }

      private static void PushNamespaceTable(IntPtr L, string nsPath)
      {
         // Create table
         LuaNative.lua_newtable(L);

         // Create metatable with __index
         LuaNative.lua_newtable(L);

         // Push namespace path as upvalue for __index closure
         LuaNative.lua_pushstring(L, nsPath);
         LuaNative.lua_pushcclosure(L, NSIndex, 1);

         LuaNative.lua_setfield(L, -2, "__index");

         LuaNative.lua_setmetatable(L, -2);
      }

   #endregion

   }
}
