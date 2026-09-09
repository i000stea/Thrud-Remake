using TMPro;
using UnityEditor;
using UnityEngine;

namespace TeaGame.Tools.Editor
{
   /// <summary>
   /// TMP 字体资产修复工具。
   ///
   /// 背景：TextMeshPro 的「下划线」表现使用特殊字符 <c>0x5F</c>（`_`）；
   /// 若字体资产中缺少该字符，编辑器内每个 TMP_Text 的 OnValidate 都会打印
   /// 「The character used for Underline is not available in font asset [...]」警告（成片刷屏）。
   ///
   /// 本工具遍历全部 TMP_FontAsset：Dynamic 模式的字体直接补入 `_` 并保存资产
   /// （Static 模式无法动态补字符，跳过并提示需重建图集）。
   /// </summary>
   public static class TmpFontAssetFixer
   {
      /// <summary>TMP 下划线特殊字符（下划线 `_`）。</summary>
      private const uint UnderlineUnicode = 0x5F;

      [MenuItem("Thrud/Tools/TMP/Fix Missing Underline Character (0x5F)")]
      public static void FixMissingUnderline()
      {
         var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
         var fixedCount = 0;
         var skipped = 0;

         foreach (var guid in guids)
         {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) continue;

            font.ReadFontAssetDefinition();
            if (font.characterLookupTable != null && font.characterLookupTable.ContainsKey(UnderlineUnicode))
            {
               continue; // 已含下划线，跳过
            }

            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
               skipped++;
               Debug.LogWarning($"[TMP] {font.name} 为 Static 图集且缺少下划线字符，需重新生成字体资产（跳过）。");
               continue;
            }

            var ok = font.TryAddCharacters("_", out var missing);
            if (ok && string.IsNullOrEmpty(missing))
            {
               EditorUtility.SetDirty(font);
               fixedCount++;
               Debug.Log($"[TMP] 已补下划线字符：{font.name}");
            }
            else
            {
               skipped++;
               Debug.LogWarning($"[TMP] 无法为 {font.name} 补下划线字符（缺字符：{missing}）。");
            }
         }

         AssetDatabase.SaveAssets();
         AssetDatabase.Refresh();
         Debug.Log($"[TMP] 下划线字符修复完成：补齐 {fixedCount} 个字体，跳过 {skipped} 个。");
      }
   }
}
