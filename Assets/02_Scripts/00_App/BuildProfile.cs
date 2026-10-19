using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeaGame.App
{
   /// <summary>
   /// 构建画像（`Assets/Resources/BuildProfile.txt`，由出包菜单写入）。
   ///
   /// **为什么需要它**：薄包（APK 里不带 AssetBundle）与胖包（带基线）在运行时的启动路径不同——
   /// 薄包首启必须先把资源下下来，才能初始化 ResKit。而运行期无法可靠判断「这个包里有没有资源」：
   /// Android 上 StreamingAssets 在 APK 内，`File.Exists` 恒为 false，只能用异步请求去探（多一次 IO）。
   /// 所以改由构建侧写一个标记文件，运行时同步读一行就知道。
   ///
   /// 标记缺失时按**胖包**处理（保守：宁可多一次白跑的定位，也不要让带资源的包走薄包路径）。
   /// </summary>
   public static class BuildProfile
   {
      /// <summary>标记文件在 Resources 下的路径（不含扩展名）。</summary>
      public const string ResourcePath = "BuildProfile";

      /// <summary>键：包内是否携带 AssetBundle 基线（`true`/`false`）。</summary>
      public const string KeyPackageBundles = "package.bundles";

      /// <summary>键：写入时间（排障用）。</summary>
      public const string KeyBuiltAtUtc = "builtAtUtc";

      private static Dictionary<string, string> sValues;

      /// <summary>包内是否带了 AssetBundle 基线（胖包）。缺失标记时按 true 处理。</summary>
      public static bool PackageHasAssetBundles
      {
         get
         {
            var raw = Get(KeyPackageBundles);
            if (string.IsNullOrEmpty(raw)) return true;
            return !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase);
         }
      }

      /// <summary>构建时间（没有标记时返回空）。</summary>
      public static string BuiltAtUtc => Get(KeyBuiltAtUtc) ?? string.Empty;

      /// <summary>读一个键（懒加载 + 缓存）。</summary>
      public static string Get(string key)
      {
         EnsureLoaded();
         return sValues.TryGetValue(key, out var value) ? value : null;
      }

      private static void EnsureLoaded()
      {
         if (sValues != null) return;

         sValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

         var asset = Resources.Load<TextAsset>(ResourcePath);
         if (asset == null) return;

         foreach (var line in asset.text.Split('\n'))
         {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)) continue;

            var index = trimmed.IndexOf('=');
            if (index <= 0) continue;

            sValues[trimmed.Substring(0, index).Trim()] = trimmed.Substring(index + 1).Trim();
         }
      }
   }
}
