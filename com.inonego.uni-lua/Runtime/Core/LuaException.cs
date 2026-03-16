using System;

namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// Exception thrown when a Lua operation fails.
   /// </summary>
   // ============================================================
   public class LuaException : Exception
   {

   #region Fields

      // ------------------------------------------------------------
      /// <summary>
      /// Lua error code (LUA_ERRRUN, LUA_ERRSYNTAX, etc.)
      /// </summary>
      // ------------------------------------------------------------
      public int ErrorCode { get; }

   #endregion

   #region Constructors

      public LuaException(string message) : base(message)
      {
         ErrorCode = LuaConst.LUA_ERRRUN;
      }

      public LuaException(int errorCode, string message) : base(message)
      {
         ErrorCode = errorCode;
      }

      public LuaException(string message, Exception innerException) : base(message, innerException)
      {
         ErrorCode = LuaConst.LUA_ERRRUN;
      }

   #endregion

   }
}
