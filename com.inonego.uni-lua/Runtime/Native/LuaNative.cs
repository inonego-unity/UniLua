using System;
using System.Runtime.InteropServices;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// P/Invoke declarations for Lua 5.5 C API.
   /// All names follow original C snake_case convention.
   /// </summary>
   // ============================================================
   internal static class LuaNative
   {

      private const string DLL = "lua55";

   #region Delegate

      [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
      public delegate int lua_CFunction(IntPtr L);

   #endregion

   #region State

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern IntPtr luaL_newstate();

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_close(IntPtr L);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void luaL_openselectedlibs(IntPtr L, int load, int preload);

      // luaL_openlibs(L) => luaL_openselectedlibs(L, ~0, 0)
      public static void luaL_openlibs(IntPtr L)
      {
         luaL_openselectedlibs(L, ~0, 0);
      }

   #endregion

   #region Load / Execute

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int luaL_loadstring(IntPtr L, [MarshalAs(UnmanagedType.LPUTF8Str)] string s);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int luaL_loadfilex(IntPtr L, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, IntPtr mode);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_pcallk(IntPtr L, int nargs, int nresults, int msgh, IntPtr ctx, IntPtr k);

   #endregion

   #region Stack Push

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_pushnil(IntPtr L);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_pushboolean(IntPtr L, int b);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_pushinteger(IntPtr L, long n);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_pushnumber(IntPtr L, double n);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern IntPtr lua_pushstring(IntPtr L, [MarshalAs(UnmanagedType.LPUTF8Str)] string s);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_pushcclosure(IntPtr L, lua_CFunction fn, int n);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_pushlightuserdata(IntPtr L, IntPtr p);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_pushvalue(IntPtr L, int index);

   #endregion

   #region Stack Read

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_toboolean(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern long lua_tointegerx(IntPtr L, int index, out int isnum);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern double lua_tonumberx(IntPtr L, int index, out int isnum);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern IntPtr lua_tolstring(IntPtr L, int index, out IntPtr len);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern IntPtr lua_topointer(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern IntPtr lua_touserdata(IntPtr L, int index);

   #endregion

   #region Stack Query

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_type(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern IntPtr lua_typename(IntPtr L, int tp);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_gettop(IntPtr L);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_settop(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_checkstack(IntPtr L, int n);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_isinteger(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_isnumber(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_isstring(IntPtr L, int index);

   #endregion

   #region Table

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_createtable(IntPtr L, int narr, int nrec);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_gettable(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_settable(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_getfield(IntPtr L, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string k);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_setfield(IntPtr L, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string k);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_rawget(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_rawset(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_rawgeti(IntPtr L, int index, long n);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_rawseti(IntPtr L, int index, long n);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_next(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_rawlen(IntPtr L, int index);

   #endregion

   #region Global

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_getglobal(IntPtr L, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void lua_setglobal(IntPtr L, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

   #endregion

   #region Metatable

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_getmetatable(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_setmetatable(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int luaL_newmetatable(IntPtr L, [MarshalAs(UnmanagedType.LPUTF8Str)] string tname);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void luaL_setmetatable(IntPtr L, [MarshalAs(UnmanagedType.LPUTF8Str)] string tname);

   #endregion

   #region Userdata

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern IntPtr lua_newuserdatauv(IntPtr L, IntPtr size, int nuvalue);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_isuserdata(IntPtr L, int index);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern IntPtr luaL_testudata(IntPtr L, int ud, [MarshalAs(UnmanagedType.LPUTF8Str)] string tname);

   #endregion

   #region Reference

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int luaL_ref(IntPtr L, int t);

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern void luaL_unref(IntPtr L, int t, int r);

   #endregion

   #region Garbage Collection

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int lua_gc(IntPtr L, int what, params int[] args);

   #endregion

   #region Error

      [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
      public static extern int luaL_error(IntPtr L, [MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

   #endregion

   #region Macro Equivalents

      // lua_pcall(L, n, r, f) => lua_pcallk(L, n, r, f, 0, NULL)
      public static int lua_pcall(IntPtr L, int nargs, int nresults, int msgh)
      {
         return lua_pcallk(L, nargs, nresults, msgh, IntPtr.Zero, IntPtr.Zero);
      }

      // lua_pop(L, n) => lua_settop(L, -(n)-1)
      public static void lua_pop(IntPtr L, int n)
      {
         lua_settop(L, -(n) - 1);
      }

      // lua_newtable(L) => lua_createtable(L, 0, 0)
      public static void lua_newtable(IntPtr L)
      {
         lua_createtable(L, 0, 0);
      }

      // lua_register(L, n, f) => lua_pushcfunction + lua_setglobal
      public static void lua_register(IntPtr L, string name, lua_CFunction f)
      {
         lua_pushcfunction(L, f);
         lua_setglobal(L, name);
      }

      // lua_pushcfunction(L, f) => lua_pushcclosure(L, f, 0)
      public static void lua_pushcfunction(IntPtr L, lua_CFunction f)
      {
         lua_pushcclosure(L, f, 0);
      }

      // lua_isfunction(L, n) => lua_type(L, n) == LUA_TFUNCTION
      public static bool lua_isfunction(IntPtr L, int index)
      {
         return lua_type(L, index) == LuaConst.LUA_TFUNCTION;
      }

      // lua_istable(L, n) => lua_type(L, n) == LUA_TTABLE
      public static bool lua_istable(IntPtr L, int index)
      {
         return lua_type(L, index) == LuaConst.LUA_TTABLE;
      }

      // lua_isnil(L, n) => lua_type(L, n) == LUA_TNIL
      public static bool lua_isnil(IntPtr L, int index)
      {
         return lua_type(L, index) == LuaConst.LUA_TNIL;
      }

      // lua_isnoneornil(L, n) => lua_type(L, n) <= 0
      public static bool lua_isnoneornil(IntPtr L, int index)
      {
         return lua_type(L, index) <= 0;
      }

      // luaL_dostring(L, s) => luaL_loadstring + lua_pcall
      public static int luaL_dostring(IntPtr L, string s)
      {
         int result = luaL_loadstring(L, s);

         if (result != LuaConst.LUA_OK)
         {
            return result;
         }

         return lua_pcall(L, 0, LuaConst.LUA_MULTRET, 0);
      }

      // luaL_dofile(L, fn) => luaL_loadfilex + lua_pcall
      public static int luaL_dofile(IntPtr L, string filename)
      {
         int result = luaL_loadfilex(L, filename, IntPtr.Zero);

         if (result != LuaConst.LUA_OK)
         {
            return result;
         }

         return lua_pcall(L, 0, LuaConst.LUA_MULTRET, 0);
      }

      // lua_upvalueindex(i) => LUA_REGISTRYINDEX - (i)
      public static int lua_upvalueindex(int i)
      {
         return LuaConst.LUA_REGISTRYINDEX - i;
      }

   #endregion

   }
}
