using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeaGame.App
{
   /// <summary>
   /// 版本清单解析与比对（23 文档 §3 / §4）。
   /// 只管「读懂清单 + 算出要下什么」，不碰网络、不碰磁盘。
   /// </summary>
   public static class HotUpdateManifestService
   {
      /// <summary>
      /// 解析清单文本。schemaVersion 不认识时**显式失败**，不按旧格式硬解析（§4 规则 1）。
      /// </summary>
      public static bool TryParse(string json, out HotUpdateManifest manifest, out string error)
      {
         manifest = null;
         error = null;

         if (string.IsNullOrEmpty(json))
         {
            error = "清单文本为空";
            return false;
         }

         try
         {
            manifest = JsonUtility.FromJson<HotUpdateManifest>(json.TrimStart('\uFEFF'));
         }
         catch (Exception e)
         {
            error = "清单 JSON 解析失败：" + e.Message;
            return false;
         }

         if (manifest == null)
         {
            error = "清单 JSON 解析结果为空";
            return false;
         }

         if (manifest.schemaVersion != HotUpdateManifest.SupportedSchemaVersion)
         {
            error = $"不支持的清单结构版本 schemaVersion={manifest.schemaVersion}" +
                    $"（本客户端只支持 {HotUpdateManifest.SupportedSchemaVersion}）";
            return false;
         }

         if (string.IsNullOrEmpty(manifest.contentVersion))
         {
            error = "清单缺少 contentVersion";
            return false;
         }

         if (manifest.files == null)
         {
            manifest.files = new HotUpdateFileEntry[0];
         }

         return true;
      }

      /// <summary>
      /// 校验清单与当前渠道 / 平台是否匹配。渠道必须严格相等；平台大小写不敏感。
      /// </summary>
      public static bool ValidateScope(HotUpdateManifest manifest, string expectedChannel, string expectedPlatform, out string error)
      {
         error = null;

         if (!string.IsNullOrEmpty(manifest.channel) &&
             !string.Equals(manifest.channel, expectedChannel, StringComparison.Ordinal))
         {
            error = $"清单渠道({manifest.channel})与客户端配置({expectedChannel})不一致";
            return false;
         }

         if (!string.IsNullOrEmpty(manifest.platform) &&
             !string.Equals(manifest.platform, expectedPlatform, StringComparison.OrdinalIgnoreCase))
         {
            error = $"清单平台({manifest.platform})与当前平台({expectedPlatform})不一致";
            return false;
         }

         return true;
      }

      /// <summary>
      /// 内容版本是否与本地生效版本相同。相同则整次更新跳过。
      /// </summary>
      public static bool IsUpToDate(HotUpdateManifest manifest, HotUpdateCacheState state)
      {
         return state != null &&
                !string.IsNullOrEmpty(state.contentVersion) &&
                string.Equals(state.contentVersion, manifest.contentVersion, StringComparison.Ordinal);
      }

      /// <summary>
      /// 算出需要下载的条目：本地文件缺失、或 size / sha256 与清单不符的都算。
      /// </summary>
      public static List<HotUpdateFileEntry> ComputePendingDownloads(HotUpdateManifest manifest)
      {
         var pending = new List<HotUpdateFileEntry>();

         foreach (var entry in manifest.files)
         {
            if (entry == null || string.IsNullOrEmpty(entry.FileName)) continue;

            var destination = HotUpdateCacheService.DestinationFor(entry);
            if (HotUpdateCacheService.VerifyFile(destination, entry.size, entry.sha256, out _))
            {
               continue;
            }

            pending.Add(entry);
         }

         return pending;
      }

      /// <summary>
      /// 确认清单里**所有必需条目**在校验意义上都已就位（含本次跳过的）。
      /// 只有这里返回 true，才允许写状态把新 contentVersion 认定为生效版本——
      /// 这是「代码 DLL 与场景 AB 绑定同一 contentVersion」的落地点（§4 规则 3、4）。
      /// </summary>
      public static bool VerifyAllRequiredFiles(HotUpdateManifest manifest, out string error)
      {
         error = null;

         foreach (var entry in manifest.files)
         {
            if (entry == null || !entry.required) continue;

            var destination = HotUpdateCacheService.DestinationFor(entry);
            if (HotUpdateCacheService.VerifyFile(destination, entry.size, entry.sha256, out var reason))
            {
               continue;
            }

            error = $"必需文件未就位：id={entry.id}, file={entry.FileName}, 原因={reason}";
            return false;
         }

         return true;
      }

      /// <summary>
      /// 由清单生成「已落地文件」记录，供状态文件保存。
      /// </summary>
      public static HotUpdateCachedFile[] BuildCachedFiles(HotUpdateManifest manifest)
      {
         var list = new List<HotUpdateCachedFile>();

         foreach (var entry in manifest.files)
         {
            if (entry == null || string.IsNullOrEmpty(entry.FileName)) continue;

            list.Add(new HotUpdateCachedFile
            {
               id = entry.id,
               kind = entry.kind,
               fileName = entry.FileName,
               size = entry.size,
               sha256 = entry.sha256,
            });
         }

         return list.ToArray();
      }
   }
}
