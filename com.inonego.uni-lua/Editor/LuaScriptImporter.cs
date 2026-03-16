using System.IO;

using UnityEngine;

using UnityEditor.AssetImporters;

namespace inonego.UniLua.Editor
{
   // ============================================================
   /// <summary>
   /// ScriptedImporter for .lua files.
   /// Imports .lua files as TextAsset so they appear in the
   /// Project window and can be referenced in the Inspector.
   /// </summary>
   // ============================================================
   [ScriptedImporter(1, "lua")]
   public class LuaScriptImporter : ScriptedImporter
   {

      public override void OnImportAsset(AssetImportContext ctx)
      {
         var text = File.ReadAllText(ctx.assetPath);
         var asset = new TextAsset(text);

         ctx.AddObjectToAsset("main", asset);
         ctx.SetMainObject(asset);
      }

   }
}
