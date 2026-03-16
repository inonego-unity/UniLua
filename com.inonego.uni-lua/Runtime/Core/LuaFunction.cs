using System;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// Holds a reference to a Lua function via luaL_ref.
   /// Provides Call to invoke the function with arguments.
   /// </summary>
   // ============================================================
   public class LuaFunction : IDisposable
   {

   #region Fields

      // ------------------------------------------------------------
      /// <summary>
      /// The LuaEnv that owns this function reference.
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
      /// Create a LuaFunction from the value at the top of the stack.
      /// Pops the value from the stack.
      /// </summary>
      // ------------------------------------------------------------
      public LuaFunction(LuaEnv env)
      {
         this.env = env ?? throw new ArgumentNullException(nameof(env));

         if (!LuaNative.lua_isfunction(env.RawState, -1))
         {
            throw new LuaException("Value at top of stack is not a function.");
         }

         reference = LuaNative.luaL_ref(env.RawState, LuaConst.LUA_REGISTRYINDEX);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Create a LuaFunction from an existing reference ID.
      /// </summary>
      // ------------------------------------------------------------
      internal LuaFunction(LuaEnv env, int reference)
      {
         this.env = env ?? throw new ArgumentNullException(nameof(env));
         this.reference = reference;
      }

   #endregion

   #region Dispose

      ~LuaFunction()
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
      /// Push this function onto the Lua stack.
      /// </summary>
      // ------------------------------------------------------------
      public void Push()
      {
         LuaNative.lua_rawgeti(env.RawState, LuaConst.LUA_REGISTRYINDEX, reference);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Call this Lua function with the given arguments.
      /// Returns all results from the function.
      /// </summary>
      // ------------------------------------------------------------
      public object[] Call(params object[] args)
      {
         var L = env.RawState;
         int oldTop = env.Top;

         Push();

         if (args != null)
         {
            foreach (var arg in args)
            {
               env.Push(arg);
            }
         }

         int argCount = args?.Length ?? 0;
         int result = LuaNative.lua_pcall(L, argCount, LuaConst.LUA_MULTRET, 0);

         if (result != LuaConst.LUA_OK)
         {
            IntPtr ptr = LuaNative.lua_tolstring(L, -1, out _);
            string error = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(ptr);
            LuaNative.lua_pop(L, 1);

            throw new LuaException(result, error);
         }

         int newTop = env.Top;
         int count = newTop - oldTop;

         if (count <= 0)
         {
            return Array.Empty<object>();
         }

         var results = new object[count];

         for (int i = 0; i < count; i++)
         {
            results[i] = env.ToObject(oldTop + 1 + i);
         }

         LuaNative.lua_pop(L, count);

         return results;
      }

   #endregion

   }
}
