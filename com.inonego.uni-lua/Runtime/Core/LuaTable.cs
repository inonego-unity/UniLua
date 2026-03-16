using System;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// Holds a reference to a Lua table via luaL_ref.
   /// Provides Get, Set, Length, and ForEach operations.
   /// </summary>
   // ============================================================
   public class LuaTable : IDisposable
   {

   #region Fields

      // ------------------------------------------------------------
      /// <summary>
      /// The LuaEnv that owns this table reference.
      /// </summary>
      // ------------------------------------------------------------
      public LuaEnv Env => env;

      private readonly LuaEnv env = null;

      // ------------------------------------------------------------
      /// <summary>
      /// The reference ID in LUA_REGISTRYINDEX.
      /// </summary>
      // ------------------------------------------------------------
      public int Reference => reference;

      private int reference = LuaConst.LUA_NOREF;

   #endregion

   #region Constructors

      // ------------------------------------------------------------
      /// <summary>
      /// Create a LuaTable from the value at the top of the stack.
      /// Pops the value from the stack.
      /// </summary>
      // ------------------------------------------------------------
      public LuaTable(LuaEnv env)
      {
         this.env = env ?? throw new ArgumentNullException(nameof(env));

         if (LuaNative.lua_type(env.RawState, -1) != LuaConst.LUA_TTABLE)
         {
            throw new LuaException("Value at top of stack is not a table.");
         }

         reference = LuaNative.luaL_ref(env.RawState, LuaConst.LUA_REGISTRYINDEX);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Create a LuaTable from an existing reference ID.
      /// </summary>
      // ------------------------------------------------------------
      internal LuaTable(LuaEnv env, int reference)
      {
         this.env = env ?? throw new ArgumentNullException(nameof(env));
         this.reference = reference;
      }

   #endregion

   #region Dispose

      ~LuaTable()
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
         if (reference == LuaConst.LUA_NOREF)
         {
            return;
         }

         if (!env.IsDisposed)
         {
            LuaNative.luaL_unref(env.RawState, LuaConst.LUA_REGISTRYINDEX, reference);
         }

         reference = LuaConst.LUA_NOREF;
      }

   #endregion

   #region Methods

      // ------------------------------------------------------------
      /// <summary>
      /// Push this table onto the Lua stack.
      /// </summary>
      // ------------------------------------------------------------
      public void Push()
      {
         LuaNative.lua_rawgeti(env.RawState, LuaConst.LUA_REGISTRYINDEX, reference);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Get a value from the table by string key.
      /// </summary>
      // ------------------------------------------------------------
      public object Get(string key)
      {
         var L = env.RawState;

         Push();
         LuaNative.lua_getfield(L, -1, key);

         object value = env.ToObject(-1);

         LuaNative.lua_pop(L, 2);

         return value;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Get a value from the table by integer key.
      /// </summary>
      // ------------------------------------------------------------
      public object Get(int key)
      {
         var L = env.RawState;

         Push();
         LuaNative.lua_rawgeti(L, -1, key);

         object value = env.ToObject(-1);

         LuaNative.lua_pop(L, 2);

         return value;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Set a value in the table by string key.
      /// </summary>
      // ------------------------------------------------------------
      public void Set(string key, object value)
      {
         var L = env.RawState;

         Push();
         env.Push(value);
         LuaNative.lua_setfield(L, -2, key);
         LuaNative.lua_pop(L, 1);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Set a value in the table by integer key.
      /// </summary>
      // ------------------------------------------------------------
      public void Set(int key, object value)
      {
         var L = env.RawState;

         Push();
         env.Push(value);
         LuaNative.lua_rawseti(L, -2, key);
         LuaNative.lua_pop(L, 1);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Get the raw length of the table (equivalent to # operator).
      /// </summary>
      // ------------------------------------------------------------
      public int Length
      {
         get
         {
            var L = env.RawState;

            Push();

            int length = LuaNative.lua_rawlen(L, -1);

            LuaNative.lua_pop(L, 1);

            return length;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Iterate over all key-value pairs in the table.
      /// The callback receives (key, value) as C# objects.
      /// </summary>
      // ------------------------------------------------------------
      public void ForEach(Action<object, object> callback)
      {
         var L = env.RawState;

         Push();
         LuaNative.lua_pushnil(L);

         while (LuaNative.lua_next(L, -2) != 0)
         {
            object key   = env.ToObject(-2);
            object value = env.ToObject(-1);

            LuaNative.lua_pop(L, 1);

            callback(key, value);
         }

         LuaNative.lua_pop(L, 1);
      }

   #endregion

   }
}
