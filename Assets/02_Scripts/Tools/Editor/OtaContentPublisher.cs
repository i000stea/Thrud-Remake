using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TeaGame.Tools.Editor
{
   /// <summary>
   /// 把当前构建产物发布成本地 OTA 内容（23 文档 §4 清单 + §7 内容目录）。
   ///
   /// **职责边界**：这是**构建侧**工具，不是服务端。它只做三件事：
   /// 1. 把 `StreamingAssets/AssetBundles/&lt;Platform&gt;/` 与 `StreamingAssets/HotUpdate/` 的产物镜像到
   ///    `web/LanOtaServer/content/&lt;channel&gt;/&lt;platform&gt;/`（目标目录先清空，保证与服务端内容一致）；
   /// 2. 计算每个文件的 size / sha256；
   /// 3. 写 `manifest.json`（`schemaVersion=1`，`contentVersion` 每次发布唯一）。
   ///
   /// 服务端（`web/LanOtaServer`）只按路径下发、不生成也不改写清单——清单由这里产出。
   ///
   /// 菜单：`Thrud/HotUpdate/3. 发布 OTA 内容到局域网服务目录（含 manifest.json）`
   /// </summary>
   public static class OtaContentPublisher
   {
      private const string MenuRoot = "Thrud/HotUpdate/";

      /// <summary>内容根目录（相对工程根）。</summary>
      private const string ContentRootRelative = "web/LanOtaServer/content";

      /// <summary>渠道名，与服务端路由的第一段一致。</summary>
      private const string Channel = "lan";

      /// <summary>清单文件名。</summary>
      private const string ManifestName = "manifest.json";

      /// <summary>热更目录名（与 `HotUpdateLoader.DirName` 一致）。</summary>
      private const string HotUpdateDirName = "HotUpdate";

      /// <summary>
      /// 条目 kind 取值，必须与客户端 `HotUpdateFileEntry` 的常量一致。
      /// 这里本地重复一份：`TeaGame.Tools.Editor` 在依赖方向的上游，不能引用 `TeaGame.App`。
      /// </summary>
      private const string KindAssetBundle = "assetBundle";
      private const string KindHotUpdateDll = "hotUpdateDll";
      private const string KindHotUpdateManifest = "hotUpdateManifest";
      private const string KindAotMetadata = "aotMetadata";

      /// <summary>不做 OTA 下发的文件：Unity 的 AB 依赖清单、meta、以及平台级 manifest bundle。</summary>
      private static bool IsIgnoredBundleFile(string path, string platform)
      {
         var name = Path.GetFileName(path);
         if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return true;
         if (name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)) return true;
         // ResKit 的构建产物里有一个与平台同名的 bundle（Android），它只在出包/调试时有用
         return string.Equals(name, platform, StringComparison.OrdinalIgnoreCase);
      }

      [MenuItem(MenuRoot + "3. 发布 OTA 内容到局域网服务目录（含 manifest.json）", false, 30)]
      public static void PublishLanOtaContent()
      {
         var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
         if (string.IsNullOrEmpty(projectRoot))
         {
            Debug.LogError("[OTA-Publish] 无法定位工程根目录。");
            return;
         }

         // 平台名与 ResKit 的 AssetBundlePathHelper.GetPlatformName() 在 Editor 下取值一致
         // （都是当前激活的构建目标），但本程序集在依赖上游、拿不到 ResKit 运行时类型，故直接取构建目标。
         var platform = EditorUserBuildSettings.activeBuildTarget.ToString();
         var platformFolder = platform.ToLowerInvariant();   // 服务端目录用小写（路由大小写不敏感）

         var abSource = Path.Combine(Application.streamingAssetsPath, "AssetBundles", platform);
         var hotSource = Path.Combine(Application.streamingAssetsPath, HotUpdateDirName);
         var targetRoot = Path.Combine(projectRoot, ContentRootRelative, Channel, platformFolder);
         var targetAb = Path.Combine(targetRoot, "AssetBundles");
         var targetHot = Path.Combine(targetRoot, HotUpdateDirName);

         // ---- 1. 前置校验：源必须齐 ----
         if (!Directory.Exists(abSource))
         {
            Debug.LogError($"[OTA-Publish] 找不到 AB 产物目录：{abSource}。" +
                           "先执行「Thrud/Demo/Android/2. Build AssetBundles (Android)」。");
            return;
         }

         var abFiles = Directory.GetFiles(abSource)
            .Where(p => !IsIgnoredBundleFile(p, platform))
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToArray();

         var configPath = Path.Combine(abSource, "asset_bundle_config.bin");
         if (!File.Exists(configPath))
         {
            Debug.LogError($"[OTA-Publish] AB 产物缺少 asset_bundle_config.bin：{abSource}。" +
                           "完整集合发布必须带上它（否则客户端只能走逐文件覆盖模式）。");
            return;
         }

         if (!Directory.Exists(hotSource))
         {
            Debug.LogError($"[OTA-Publish] 找不到热更目录：{hotSource}。" +
                           "先执行「Thrud/HotUpdate/1. 导出热更 DLL 与清单到 StreamingAssets」。");
            return;
         }

         var hotFiles = Directory.GetFiles(hotSource)
            .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToArray();

         var hotfixPath = Path.Combine(hotSource, "hotfix.json");
         if (!File.Exists(hotfixPath))
         {
            Debug.LogError($"[OTA-Publish] 热更目录缺少 hotfix.json：{hotSource}。");
            return;
         }

         // ---- 2. 目标目录先清空，避免上一版残留被服务端照发 ----
         ResetDirectory(targetAb);
         ResetDirectory(targetHot);

         // ---- 3. 镜像 + 收集清单条目 ----
         var entries = new List<ManifestEntry>();
         long totalBytes = 0;

         foreach (var source in abFiles)
         {
            var name = Path.GetFileName(source);
            var destination = Path.Combine(targetAb, name);
            File.Copy(source, destination, true);

            var size = new FileInfo(destination).Length;
            totalBytes += size;
            entries.Add(new ManifestEntry
            {
               id = KindAssetBundle + "/" + name,
               kind = KindAssetBundle,
               path = "AssetBundles/" + name,
               size = size,
               sha256 = ComputeSha256(destination),
               required = true,
            });
         }

         // hotfix.json 里的 hotUpdate 列表决定哪些 DLL 是热更程序集；其余 DLL 是 AOT 补充元数据
         var hotUpdateDllNames = ReadHotUpdateDllNames(hotfixPath);

         foreach (var source in hotFiles)
         {
            var name = Path.GetFileName(source);
            var destination = Path.Combine(targetHot, name);
            File.Copy(source, destination, true);

            var size = new FileInfo(destination).Length;
            totalBytes += size;

            string kind;
            if (string.Equals(name, "hotfix.json", StringComparison.OrdinalIgnoreCase))
            {
               kind = KindHotUpdateManifest;
            }
            else if (hotUpdateDllNames.Contains(Path.GetFileNameWithoutExtension(name)))
            {
               kind = KindHotUpdateDll;
            }
            else
            {
               kind = KindAotMetadata;
            }

            entries.Add(new ManifestEntry
            {
               id = kind + "/" + name,
               kind = kind,
               path = HotUpdateDirName + "/" + name,
               size = size,
               sha256 = ComputeSha256(destination),
               required = true,
            });
         }

         // ---- 4. 写清单 ----
         var contentVersion = BuildContentVersion();
         WriteManifest(Path.Combine(targetRoot, ManifestName), contentVersion, platform, entries);

         var abBytes = entries.Where(e => e.kind == KindAssetBundle).Sum(e => e.size);
         Debug.Log($"[OTA-Publish] 发布完成：contentVersion={contentVersion}，共 {entries.Count} 个文件、" +
                   $"{totalBytes / 1024f / 1024f:F1} MB（其中 AB {abBytes / 1024f / 1024f:F1} MB）。\n" +
                   $"[OTA-Publish] 内容目录：{targetRoot}\n" +
                   $"[OTA-Publish] 清单路由：/{Channel}/{platform}/manifest.json（真机用服务端控制台打印的 IP）");
      }

      /// <summary>清空并重建目录（服务端内容必须与本次发布完全一致）。</summary>
      private static void ResetDirectory(string path)
      {
         if (Directory.Exists(path))
         {
            Directory.Delete(path, true);
         }

         Directory.CreateDirectory(path);
      }

      /// <summary>读 hotfix.json 的 hotUpdate 数组，用于区分热更程序集与 AOT 补充元数据。</summary>
      private static HashSet<string> ReadHotUpdateDllNames(string hotfixPath)
      {
         var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         try
         {
            var text = File.ReadAllText(hotfixPath, Encoding.UTF8).TrimStart('\uFEFF');
            var manifest = JsonUtility.FromJson<HotfixManifestShape>(text);
            if (manifest?.hotUpdate != null)
            {
               foreach (var name in manifest.hotUpdate)
               {
                  if (!string.IsNullOrEmpty(name))
                  {
                     result.Add(Path.GetFileNameWithoutExtension(name));
                  }
               }
            }
         }
         catch (Exception e)
         {
            Debug.LogWarning($"[OTA-Publish] 解析 hotfix.json 失败，DLL 将统一按 AOT 元数据归类：{e.Message}");
         }

         return result;
      }

      /// <summary>内容版本：包版本 + 秒级时间戳，保证每次发布唯一且可追溯。</summary>
      private static string BuildContentVersion()
      {
         return PlayerSettings.bundleVersion + "+" + DateTime.Now.ToString("yyyyMMddHHmmss");
      }

      private static void WriteManifest(string path, string contentVersion, string platform, List<ManifestEntry> entries)
      {
         var builder = new StringBuilder();
         builder.Append("{\n");
         builder.Append("  \"schemaVersion\": 1,\n");
         builder.Append($"  \"contentVersion\": \"{contentVersion}\",\n");
         builder.Append($"  \"channel\": \"{Channel}\",\n");
         builder.Append($"  \"platform\": \"{platform}\",\n");
         builder.Append("  \"abSetComplete\": true,\n");
         builder.Append("  \"files\": [\n");

         for (var i = 0; i < entries.Count; i++)
         {
            var entry = entries[i];
            builder.Append("    {\n");
            builder.Append($"      \"id\": \"{entry.id}\",\n");
            builder.Append($"      \"kind\": \"{entry.kind}\",\n");
            builder.Append($"      \"path\": \"{entry.path}\",\n");
            builder.Append($"      \"size\": {entry.size},\n");
            builder.Append($"      \"sha256\": \"{entry.sha256}\",\n");
            builder.Append($"      \"required\": {(entry.required ? "true" : "false")}\n");
            builder.Append("    }");
            builder.Append(i == entries.Count - 1 ? "\n" : ",\n");
         }

         builder.Append("  ]\n");
         builder.Append("}\n");

         // 不带 BOM：JsonUtility 不接受以 U+FEFF 开头的文本（HotUpdateLoader 踩过这个坑）
         File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
      }

      private static string ComputeSha256(string path)
      {
         using (var sha = SHA256.Create())
         using (var stream = File.OpenRead(path))
         {
            var hash = sha.ComputeHash(stream);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
               builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
         }
      }

      /// <summary>hotfix.json 的最小解析形状（只关心 hotUpdate 数组）。</summary>
      [Serializable]
      private sealed class HotfixManifestShape
      {
         public string[] hotUpdate = new string[0];
      }

      /// <summary>清单条目（写 JSON 时逐字段输出，避免依赖 JsonUtility 的缩进与字段名策略）。</summary>
      private sealed class ManifestEntry
      {
         public string id;
         public string kind;
         public string path;
         public long size;
         public string sha256;
         public bool required;
      }
   }
}
