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
   /// **只发布业务热更程序集**：`StreamingAssets/HotUpdate/` 里除了 6 个 `TeaGame.*` 热更 DLL，
   /// 还有一批 AOT 补充元数据（`mscorlib.dll` / `System.Core.dll` / `QFramework.dll` / `DOTween.dll`
   /// / `UnityEngine.CoreModule.dll` / `UIKit.dll` / `ResKit.dll` / `QFramework.CoreKit.dll` / `TeaGame.App.dll`）。
   /// 那些 DLL **从来不作为程序集加载**，只被 `RuntimeApi.LoadMetadataForAOTAssembly` 读来补泛型元数据；
   /// 而且它们与**已安装的 APK** 是版本强耦合的，通过 OTA 换掉会让元数据与 AOT 代码对不上。
   /// 所以它们随 APK 走（StreamingAssets），**不进 OTA 清单**：
   /// 客户端读 `hotfix.json` 里的 aotMetadata 时会从 StreamingAssets 逐文件回退命中。
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

      /// <summary>允许热更的程序集名前缀（只热更自己写的业务程序集）。</summary>
      private const string HotUpdateAssemblyPrefix = "TeaGame.";

      /// <summary>AOT 程序集：永远不能出现在热更列表里。</summary>
      private const string AotAssemblyName = "TeaGame.App";

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
      public static void PublishLanOtaContentMenu()
      {
         Publish();
      }

      /// <summary>
      /// 发布实现（菜单与命令行共用）。返回是否成功——命令行据此给退出码。
      /// </summary>
      public static bool Publish()
      {
         var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
         if (string.IsNullOrEmpty(projectRoot))
         {
            Debug.LogError("[OTA-Publish] 无法定位工程根目录。");
            return false;
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
            return false;
         }

         var abFiles = Directory.GetFiles(abSource)
            .Where(p => !IsIgnoredBundleFile(p, platform))
            .Where(p => IsLiveBundleFile(p, platform))
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToArray();

         var configPath = Path.Combine(abSource, "asset_bundle_config.bin");
         if (!File.Exists(configPath))
         {
            Debug.LogError($"[OTA-Publish] AB 产物缺少 asset_bundle_config.bin：{abSource}。" +
                           "完整集合发布必须带上它（否则客户端只能走逐文件覆盖模式）。");
            return false;
         }

         if (!Directory.Exists(hotSource))
         {
            Debug.LogError($"[OTA-Publish] 找不到热更目录：{hotSource}。" +
                           "先执行「Thrud/HotUpdate/1. 导出热更 DLL 与清单到 StreamingAssets」。");
            return false;
         }

         var hotFiles = Directory.GetFiles(hotSource)
            .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToArray();

         var hotfixPath = Path.Combine(hotSource, "hotfix.json");
         if (!File.Exists(hotfixPath))
         {
            Debug.LogError($"[OTA-Publish] 热更目录缺少 hotfix.json：{hotSource}。");
            return false;
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
         if (!ValidateHotUpdateAssemblies(hotUpdateDllNames, out var assemblyError))
         {
            Debug.LogError("[OTA-Publish] " + assemblyError);
            return false;
         }

         var skippedAotMetadata = new System.Collections.Generic.List<string>();

         foreach (var source in hotFiles)
         {
            var name = Path.GetFileName(source);
            var isHotfixJson = string.Equals(name, "hotfix.json", StringComparison.OrdinalIgnoreCase);
            var isHotUpdateDll = !isHotfixJson &&
                                 hotUpdateDllNames.Contains(Path.GetFileNameWithoutExtension(name));

            if (!isHotfixJson && !isHotUpdateDll)
            {
               // AOT 补充元数据：与 APK 强耦合，随包走，不做 OTA 下发
               skippedAotMetadata.Add(name);
               continue;
            }

            var destination = Path.Combine(targetHot, name);
            File.Copy(source, destination, true);

            var size = new FileInfo(destination).Length;
            totalBytes += size;

            var kind = isHotfixJson ? KindHotUpdateManifest : KindHotUpdateDll;

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

         if (skippedAotMetadata.Count > 0)
         {
            Debug.Log($"[OTA-Publish] 跳过 {skippedAotMetadata.Count} 个 AOT 补充元数据 DLL（随 APK 走，不参与 OTA）：" +
                      string.Join(", ", skippedAotMetadata.ToArray()));
         }

         // ---- 4. 写清单 ----
         var contentVersion = BuildContentVersion();
         WriteManifest(Path.Combine(targetRoot, ManifestName), contentVersion, platform, entries);

         var abBytes = entries.Where(e => e.kind == KindAssetBundle).Sum(e => e.size);
         Debug.Log($"[OTA-Publish] 发布完成：contentVersion={contentVersion}，共 {entries.Count} 个文件、" +
                   $"{totalBytes / 1024f / 1024f:F1} MB（其中 AB {abBytes / 1024f / 1024f:F1} MB）。\n" +
                   $"[OTA-Publish] 内容目录：{targetRoot}\n" +
                   $"[OTA-Publish] 清单路由：/{Channel}/{platform}/manifest.json（真机用服务端控制台打印的 IP）");
         return true;
      }

      /// <summary>
      /// 文件是否仍是「活着的」bundle：AB 产物目录里会残留已取消标记的历史 bundle
      /// （本次实测遇到过一个 42.7 MB 的孤儿，重打 AB 后又出现），
      /// 它们不在 `AssetDatabase.GetAllAssetBundleNames()` 里，发到 OTA 既浪费带宽也会误导排障。
      /// `asset_bundle_config.bin` 与平台同名 bundle 是构建产物的一部分，单独放行。
      /// </summary>
      private static bool IsLiveBundleFile(string path, string platform)
      {
         var name = Path.GetFileName(path);
         if (string.Equals(name, "asset_bundle_config.bin", StringComparison.OrdinalIgnoreCase)) return true;
         if (string.Equals(name, platform, StringComparison.OrdinalIgnoreCase)) return true;

         if (sLiveBundleNames == null)
         {
            sLiveBundleNames = new HashSet<string>(AssetDatabase.GetAllAssetBundleNames(),
               StringComparer.OrdinalIgnoreCase);
         }

         if (sLiveBundleNames.Contains(name)) return true;

         Debug.LogWarning($"[OTA-Publish] 跳过非活 bundle（不在 AB 清单里，疑似历史残留）：{name}");
         return false;
      }

      /// <summary>当前工程登记过的 bundle 名（发布内缓存一次）。</summary>
      private static HashSet<string> sLiveBundleNames;

      /// <summary>清空 bundle 名缓存（重打 AB 后调用；同一次发布内无需重复取）。</summary>
      public static void InvalidateBundleNameCache()
      {
         sLiveBundleNames = null;
      }

      /// <summary>
      /// 校验 hotfix.json 声明的热更程序集集合：**只允许 `TeaGame.*`，且不能是 AOT 的 `TeaGame.App`**。
      ///
      /// 这是把「只热更自己写的业务程序集」变成硬约束，而不是靠人记得：
      /// 一旦 `HybridCLR/Settings` 或导出配置把 `QFramework` / `DOTween` / `mscorlib` 之类
      /// 插件或引擎程序集列进 hotUpdate，插件就会被当作热更程序集加载，出现双份类型、
      /// 帧回调重复触发等极难排查的问题——宁可在发布这一步直接失败。
      /// </summary>
      private static bool ValidateHotUpdateAssemblies(HashSet<string> hotUpdateDllNames, out string error)
      {
         error = null;

         if (hotUpdateDllNames.Count == 0)
         {
            error = "hotfix.json 的 hotUpdate 为空：没有任何热更程序集。先执行「Thrud/HotUpdate/1. 导出热更 DLL 与清单」。";
            return false;
         }

         foreach (var name in hotUpdateDllNames)
         {
            if (string.Equals(name, AotAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
               error = $"hotfix.json 把 AOT 程序集 {name} 当成热更程序集——AOT 程序集无法热更，" +
                       "请检查 HybridCLR 的 hotUpdateAssemblyDefinitions。";
               return false;
            }

            if (!name.StartsWith(HotUpdateAssemblyPrefix, StringComparison.Ordinal))
            {
               error = $"hotfix.json 的 hotUpdate 里出现非业务程序集 {name}。" +
                       $"本工程只允许 {HotUpdateAssemblyPrefix}*（{AotAssemblyName} 除外）热更；" +
                       "插件/框架/引擎程序集必须留在 AOT。请检查 HybridCLR 的 hotUpdateAssemblyDefinitions。";
               return false;
            }
         }

         return true;
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
