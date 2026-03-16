namespace inonego.UniLua
{
   // ============================================================
   /// <summary>
   /// Lua C API constants matching lua.h / luaconf.h definitions.
   /// </summary>
   // ============================================================
   internal static class LuaConst
   {

   #region Version

      public const string LUA_VERSION = "Lua 5.5";

      public const int LUA_VERSION_NUM = 505;

   #endregion

   #region Thread Status

      public const int LUA_OK        = 0;
      public const int LUA_YIELD     = 1;
      public const int LUA_ERRRUN    = 2;
      public const int LUA_ERRSYNTAX = 3;
      public const int LUA_ERRMEM    = 4;
      public const int LUA_ERRERR    = 5;

   #endregion

   #region Type Tags

      public const int LUA_TNONE          = -1;
      public const int LUA_TNIL           = 0;
      public const int LUA_TBOOLEAN       = 1;
      public const int LUA_TLIGHTUSERDATA = 2;
      public const int LUA_TNUMBER        = 3;
      public const int LUA_TSTRING        = 4;
      public const int LUA_TTABLE         = 5;
      public const int LUA_TFUNCTION      = 6;
      public const int LUA_TUSERDATA      = 7;
      public const int LUA_TTHREAD        = 8;

   #endregion

   #region Stack / Registry

      public const int LUA_MINSTACK       = 20;
      public const int LUA_REGISTRYINDEX  = -(int.MaxValue / 2 + 1000);
      public const int LUA_RIDX_GLOBALS    = 2;
      public const int LUA_RIDX_MAINTHREAD = 3;
      public const int LUA_RIDX_LAST       = LUA_RIDX_MAINTHREAD;

   #endregion

   #region Garbage Collection

      public const int LUA_GCSTOP         = 0;
      public const int LUA_GCRESTART      = 1;
      public const int LUA_GCCOLLECT      = 2;
      public const int LUA_GCCOUNT        = 3;
      public const int LUA_GCCOUNTB       = 4;
      public const int LUA_GCSTEP         = 5;
      public const int LUA_GCISRUNNING    = 6;
      public const int LUA_GCGEN          = 7;
      public const int LUA_GCINC          = 8;
      public const int LUA_GCPARAM        = 9;

      // GC parameter names for LUA_GCPARAM
      public const int LUA_GCPMINORMUL    = 0;
      public const int LUA_GCPMAJORMINOR  = 1;
      public const int LUA_GCPMINORMAJOR  = 2;
      public const int LUA_GCPPAUSE       = 3;
      public const int LUA_GCPSTEPMUL     = 4;
      public const int LUA_GCPSTEPSIZE    = 5;

   #endregion

   #region Reference

      public const int LUA_NOREF  = -2;
      public const int LUA_REFNIL = -1;

   #endregion

   #region Comparison

      public const int LUA_OPEQ = 0;
      public const int LUA_OPLT = 1;
      public const int LUA_OPLE = 2;

   #endregion

   #region Arithmetic

      public const int LUA_OPADD  = 0;
      public const int LUA_OPSUB  = 1;
      public const int LUA_OPMUL  = 2;
      public const int LUA_OPMOD  = 3;
      public const int LUA_OPPOW  = 4;
      public const int LUA_OPDIV  = 5;
      public const int LUA_OPIDIV = 6;
      public const int LUA_OPBAND = 7;
      public const int LUA_OPBOR  = 8;
      public const int LUA_OPBXOR = 9;
      public const int LUA_OPSHL  = 10;
      public const int LUA_OPSHR  = 11;
      public const int LUA_OPUNM  = 12;
      public const int LUA_OPBNOT = 13;

   #endregion

   #region Multret

      public const int LUA_MULTRET = -1;

   #endregion

   }
}
