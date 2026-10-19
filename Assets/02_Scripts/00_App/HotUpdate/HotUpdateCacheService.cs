using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using QFramework;
using UnityEngine;

namespace TeaGame.App
{
   /// <summary>
   /// 本地 OTA 缓存状态（写在 `persistentDataPath/HotUpdate/ota-state.json`）。
   /// 只有「整次更新全部校验通过」才会写这份状态——它是「当前可信内容」的唯一凭据，
   /// 因此运行时只信任这里登记过的文件，`.part` 永远不会被当成可用内容。
   /// </summary>
   [Serializable]
   public sealed class HotUpdateCacheState
   {
      /// <summary>状态结构版本。</summary>
      public int schemaVersion = 1;

      /// <summary>当前生效的内容版本。</summary>
      public string contentVersion;

      /// <summary>上一份可用内容版本（回滚目标）。空表示没有可回滚版本（首包基线）。</summary>
      public string previousContentVersion;

      /// <summary>渠道名。</summary>
      public string channel;

      /// <summary>平台名。</summary>
      public string platform;

      /// <summary>本次内容是否覆盖了完整 AB 集合（决定 ResKit 是否切到 persistent 全量）。</summary>
      public bool abSetComplete;

      /// <summary>落地时间（UTC ISO8601，排障用）。</summary>
      public string updatedAtUtc;

      /// <summary>已落地的文件清单。</summary>
      public HotUpdateCachedFile[] files = new HotUpdateCachedFile[0];
   }

   /// <summary>已落地的单个文件记录。</summary>
   [Serializable]
   public sealed class HotUpdateCachedFile
   {
      public string id;
      public string kind;
      public string fileName;
      public long size;
      public string sha256;
   }

   /// <summary>
   /// OTA 缓存层（23 文档 §3 / §4 规则 2、5）。
   ///
   /// **落点选择**：全部放在 `persistentDataPath` 下，因为 ResKit 与 HotUpdateLoader
   /// 都已经是「persistent 优先、逐文件回退 StreamingAssets」：
   /// - `HotUpdateLoader.ReadBytes` → `persistentDataPath/HotUpdate/<file>` 优先；
   /// - `AssetBundleSettings.AssetBundleName2Url` → `persistentDataPath/AssetBundles/<Platform>/<file>` 优先。
   /// 所以「只替换一个 bundle / 一个 DLL」不需要改 ResKit，也不需要整包搬移。
   ///
   /// **原子性**：先写 `<name>.part`，校验 size + sha256 通过后再改名覆盖正式文件；
   /// 任何一步失败都只丢临时文件，当前可用版本原样保留。
   ///
   /// **回滚**：覆盖正式文件之前先把旧文件备份到 `HotUpdate/_backup/<上一版本>/`，
   /// 保留上一份完整内容，`RollbackToPrevious` 可整体还原。
   /// </summary>
   public sealed class HotUpdateCacheService
   {
      /// <summary>临时文件后缀。</summary>
      public const string PartSuffix = ".part";

      /// <summary>状态文件名。</summary>
      public const string StateFileName = "ota-state.json";

      /// <summary>回滚备份目录名（放在 HotUpdate 目录下）。</summary>
      private const string BackupDirName = "_backup";

      /// <summary>持久化根目录。</summary>
      public static string PersistentRoot => Application.persistentDataPath;

      /// <summary>热更目录（与 HotUpdateLoader.DirName 保持一致）。</summary>
      public static string HotUpdateDir => Path.Combine(PersistentRoot, HotUpdateLoader.DirName);

      /// <summary>OTA 覆盖用的 AB 目录（与 ResKit 的 persistent 落点保持一致）。</summary>
      public static string AssetBundleDir =>
         Path.Combine(PersistentRoot, "AssetBundles", AssetBundlePathHelper.GetPlatformName());

      /// <summary>状态文件完整路径。</summary>
      public static string StatePath => Path.Combine(HotUpdateDir, StateFileName);

      /// <summary>本次运行解析出的平台名（写进日志与状态，便于核对「这台机器找的是哪个目录」）。</summary>
      public static string PlatformName => AssetBundlePathHelper.GetPlatformName();

      /// <summary>
      /// 条目的本地落点：AB 进 AB 目录，DLL 与热更清单进 HotUpdate 目录。
      /// 由 kind 决定而不是由服务端 path 决定——服务端换目录结构不影响客户端。
      /// </summary>
      public static string DestinationFor(HotUpdateFileEntry entry)
      {
         var fileName = entry.FileName;
         return entry.IsAssetBundle
            ? Path.Combine(AssetBundleDir, fileName)
            : Path.Combine(HotUpdateDir, fileName);
      }

      /// <summary>条目的临时落点（`<正式路径>.part`）。</summary>
      public static string PartPathFor(HotUpdateFileEntry entry)
      {
         return DestinationFor(entry) + PartSuffix;
      }

      /// <summary>计算文件 SHA-256（小写十六进制）。文件不存在返回 null。</summary>
      public static string ComputeSha256(string path)
      {
         if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

         try
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
         catch (Exception e)
         {
            Debug.LogError($"[HotUpdate] 计算 SHA-256 失败 {path}：{e.Message}");
            return null;
         }
      }

      /// <summary>
      /// 校验文件是否与清单一致（size + sha256）。size 为 0 或 sha256 为空时跳过该项校验。
      /// </summary>
      public static bool VerifyFile(string path, long expectedSize, string expectedSha256, out string reason)
      {
         reason = null;

         if (!File.Exists(path))
         {
            reason = "文件不存在";
            return false;
         }

         if (expectedSize > 0)
         {
            var actualSize = new FileInfo(path).Length;
            if (actualSize != expectedSize)
            {
               reason = $"大小不符（期望 {expectedSize}，实际 {actualSize}）";
               return false;
            }
         }

         if (!string.IsNullOrEmpty(expectedSha256))
         {
            var actual = ComputeSha256(path);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
               reason = "SHA-256 不符";
               return false;
            }
         }

         return true;
      }

      /// <summary>
      /// 把 `.part` 提升为正式文件：先复校验，再覆盖。
      /// **先校验后覆盖**是「hash 错误时保留旧版本」的唯一保证点。
      /// </summary>
      public static bool CommitPart(HotUpdateFileEntry entry, out string reason)
      {
         var partPath = PartPathFor(entry);
         var finalPath = DestinationFor(entry);

         if (!VerifyFile(partPath, entry.size, entry.sha256, out reason))
         {
            reason = "临时文件校验失败：" + reason;
            SafeDelete(partPath);
            return false;
         }

         try
         {
            var directory = Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrEmpty(directory))
            {
               Directory.CreateDirectory(directory);
            }

            // File.Move 在目标存在时会抛异常，先删再移；旧文件已在备份目录留底
            if (File.Exists(finalPath))
            {
               File.Delete(finalPath);
            }

            File.Move(partPath, finalPath);
            reason = null;
            return true;
         }
         catch (Exception e)
         {
            reason = "落盘失败：" + e.Message;
            SafeDelete(partPath);
            return false;
         }
      }

      /// <summary>
      /// 覆盖前把当前正式文件备份到 `_backup/&lt;version&gt;/`（镜像相对路径）。
      /// 文件不存在时不备份。
      /// </summary>
      public static void BackupBeforeOverwrite(string destinationPath, string version)
      {
         if (string.IsNullOrEmpty(version) || !File.Exists(destinationPath)) return;

         try
         {
            var relative = ToPersistentRelative(destinationPath);
            var target = Path.Combine(BackupRoot(version), relative);
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
               Directory.CreateDirectory(directory);
            }

            File.Copy(destinationPath, target, true);
         }
         catch (Exception e)
         {
            // 备份失败不阻断更新，但必须留痕：这种情况下本次更新不可回滚
            Debug.LogWarning($"[HotUpdate] 备份旧文件失败（本次更新将不可回滚）：{destinationPath} -> {e.Message}");
         }
      }

      /// <summary>
      /// 读取缓存状态。不存在或解析失败返回 null（等价于「当前只有包内基线」）。
      /// </summary>
      public static HotUpdateCacheState TryLoadState()
      {
         if (!File.Exists(StatePath)) return null;

         try
         {
            var text = File.ReadAllText(StatePath, Encoding.UTF8).TrimStart('\uFEFF');
            return JsonUtility.FromJson<HotUpdateCacheState>(text);
         }
         catch (Exception e)
         {
            Debug.LogWarning($"[HotUpdate] 读取缓存状态失败，按无状态处理：{e.Message}");
            return null;
         }
      }

      /// <summary>写缓存状态（原子：先写 .part 再替换，避免掉电写坏状态文件）。</summary>
      public static bool SaveState(HotUpdateCacheState state)
      {
         if (state == null) return false;

         try
         {
            Directory.CreateDirectory(HotUpdateDir);
            var partPath = StatePath + PartSuffix;
            File.WriteAllText(partPath, JsonUtility.ToJson(state, true), new UTF8Encoding(false));

            if (File.Exists(StatePath))
            {
               File.Delete(StatePath);
            }

            File.Move(partPath, StatePath);
            return true;
         }
         catch (Exception e)
         {
            Debug.LogError($"[HotUpdate] 写缓存状态失败：{e.Message}");
            return false;
         }
      }

      /// <summary>
      /// 回滚到上一份完整内容（23 文档 §4 规则 5）。
      /// 只还原备份目录里确实存在的文件——备份缺失的文件保持当前版本。
      /// </summary>
      public static bool RollbackToPrevious(HotUpdateCacheState current, out string reason)
      {
         reason = null;

         if (current == null || string.IsNullOrEmpty(current.previousContentVersion))
         {
            reason = "没有可回滚的上一版本记录";
            return false;
         }

         var backupRoot = BackupRoot(current.previousContentVersion);
         if (!Directory.Exists(backupRoot))
         {
            reason = "上一版本备份目录不存在：" + backupRoot;
            return false;
         }

         try
         {
            var restored = 0;
            foreach (var source in Directory.GetFiles(backupRoot, "*", SearchOption.AllDirectories))
            {
               var relative = source.Substring(backupRoot.Length).TrimStart('\\', '/');
               var target = Path.Combine(PersistentRoot, relative);
               var directory = Path.GetDirectoryName(target);
               if (!string.IsNullOrEmpty(directory))
               {
                  Directory.CreateDirectory(directory);
               }

               File.Copy(source, target, true);
               restored++;
            }

            var rolledBack = new HotUpdateCacheState
            {
               contentVersion = current.previousContentVersion,
               previousContentVersion = null,
               channel = current.channel,
               platform = current.platform,
               abSetComplete = current.abSetComplete,
               updatedAtUtc = DateTime.UtcNow.ToString("o"),
               files = new HotUpdateCachedFile[0],
            };
            SaveState(rolledBack);

            Debug.Log($"[HotUpdate] 已回滚到 {rolledBack.contentVersion}（还原 {restored} 个文件）");
            return true;
         }
         catch (Exception e)
         {
            reason = "回滚失败：" + e.Message;
            return false;
         }
      }

      /// <summary>
      /// 清掉上一份状态登记、但新状态不再引用的文件（换版本后的孤儿清理）。
      /// 只删**自己登记过**的路径，不碰用户或基线放进来的其它文件。
      /// </summary>
      public static void DeleteFilesNotIn(HotUpdateCacheState previous, HotUpdateCacheState next)
      {
         if (previous?.files == null || previous.files.Length == 0) return;

         var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         if (next?.files != null)
         {
            foreach (var file in next.files)
            {
               if (file != null && !string.IsNullOrEmpty(file.fileName))
               {
                  keep.Add(file.fileName);
               }
            }
         }

         foreach (var file in previous.files)
         {
            if (file == null || string.IsNullOrEmpty(file.fileName)) continue;
            if (keep.Contains(file.fileName)) continue;

            var path = string.Equals(file.kind, HotUpdateFileEntry.KindAssetBundle, StringComparison.OrdinalIgnoreCase)
               ? Path.Combine(AssetBundleDir, file.fileName)
               : Path.Combine(HotUpdateDir, file.fileName);

            SafeDelete(path);
            Debug.Log($"[HotUpdate] 清理不再引用的文件：{path}");
         }
      }

      /// <summary>
      /// 删掉条目的临时文件（下载失败时立即调用）。
      /// `.part` 永远不是可用内容，留着只会占空间并在下次启动时被 <see cref="CleanupStaleParts"/> 收走。
      /// </summary>
      public static void DeletePart(HotUpdateFileEntry entry)
      {
         if (entry == null) return;
         SafeDelete(PartPathFor(entry));
      }

      /// <summary>清掉目录里残留的 `.part`（上次中断留下的临时文件，永远不能当作可用内容）。</summary>
      public static void CleanupStaleParts()
      {
         CleanupStalePartsIn(HotUpdateDir);
         CleanupStalePartsIn(AssetBundleDir);
      }

      private static void CleanupStalePartsIn(string directory)
      {
         if (!Directory.Exists(directory)) return;

         try
         {
            foreach (var part in Directory.GetFiles(directory, "*" + PartSuffix, SearchOption.AllDirectories))
            {
               SafeDelete(part);
               Debug.Log($"[HotUpdate] 清理残留临时文件：{part}");
            }
         }
         catch (Exception e)
         {
            Debug.LogWarning($"[HotUpdate] 清理临时文件失败：{e.Message}");
         }
      }

      private static string BackupRoot(string version)
      {
         return Path.Combine(HotUpdateDir, BackupDirName, version);
      }

      private static string ToPersistentRelative(string absolutePath)
      {
         var root = PersistentRoot.Replace('\\', '/').TrimEnd('/');
         var normalized = absolutePath.Replace('\\', '/');
         return normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? normalized.Substring(root.Length).TrimStart('/')
            : Path.GetFileName(absolutePath);
      }

      private static void SafeDelete(string path)
      {
         try
         {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
               File.Delete(path);
            }
         }
         catch (Exception e)
         {
            Debug.LogWarning($"[HotUpdate] 删除文件失败 {path}：{e.Message}");
         }
      }
   }
}
