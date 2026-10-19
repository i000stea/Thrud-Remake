using System;
using System.Collections;
using UnityEngine;

namespace TeaGame.App
{
   /// <summary>
   /// OTA 客户端配置（Inspector 可调，见 ConfigScene 的 AppBootstrapper）。
   /// 默认关闭：首包不联网也能正常启动（23 文档 §7 验收 1）。
   /// </summary>
   [Serializable]
   public sealed class HotUpdateOtaSettings
   {
      /// <summary>是否启用 OTA 内容更新。</summary>
      public bool enabled;

      /// <summary>内容服务器根地址（局域网 `http://192.168.x.x:8080`，将来换成 CDN 地址即可）。</summary>
      public string baseUrl = "http://127.0.0.1:8080";

      /// <summary>渠道名（服务端路由的第一段）。</summary>
      public string channel = "lan";

      /// <summary>清单请求超时（秒）。下载不设总时长上限。</summary>
      public float manifestTimeoutSeconds = 10f;
   }

   /// <summary>
   /// OTA 启动编排（23 文档 §2 / §3 / §7）。
   ///
   /// **位置**：必须在 `HotUpdateLoader.LoadAll()` 与 `ResKit.InitAsync()` **之前**跑，因为它要
   /// 1) 把热更 DLL / `hotfix.json` 落到 `persistentDataPath/HotUpdate/`（加载器 persistent 优先）；
   /// 2) 把 bundle 落到 `persistentDataPath/AssetBundles/&lt;Platform&gt;/`（ResKit persistent 优先）；
   /// 3) 决定 ResKit 读哪套 AB 清单（<see cref="ResKitContentLocator"/>）。
   /// 三者都是「先落盘后加载」，顺序反了就白下。
   ///
   /// **失败策略（重要）**：任何网络 / 校验 / 解析失败都**不阻断启动**，一律回落到
   /// 「当前可用版本」（有 OTA 状态就按状态，没有就包内基线）。所以这个方法永远不会让游戏起不来。
   public static class HotUpdateOtaService
   {
      /// <summary>
      /// 启动时执行一次内容更新检查。
      /// 调用方用 `yield return HotUpdateOtaService.RunAtBoot(settings);` 等待完成。
      /// </summary>
      public static IEnumerator RunAtBoot(HotUpdateOtaSettings settings)
      {
         // 上次中断留下的 .part 一律清掉：它不是可用内容，留着只会占空间并误导排障
         HotUpdateCacheService.CleanupStaleParts();

         var currentState = HotUpdateCacheService.TryLoadState();
         var platform = HotUpdateCacheService.PlatformName;

         if (settings == null || !settings.enabled || string.IsNullOrEmpty(settings.baseUrl))
         {
            Debug.Log("[HotUpdate] OTA 未启用或未配置服务器地址：本次使用包内基线。");
            ResKitContentLocator.Apply(currentState, out var skipReason);
            Debug.Log("[HotUpdate] 内容定位：" + skipReason);
            yield break;
         }

         var backend = new LocalHttpBackend(settings.baseUrl, settings.channel, platform, settings.manifestTimeoutSeconds);
         Debug.Log($"[HotUpdate] OTA 开始：backend={backend.DisplayName}, channel={settings.channel}, platform={platform}," +
                   $" 当前内容版本={(string.IsNullOrEmpty(currentState?.contentVersion) ? "<包内基线>" : currentState.contentVersion)}");

         // ---- 1. 取清单 ----
         string manifestText = null;
         string networkError = null;
         yield return backend.GetManifestAsync(text => manifestText = text, error => networkError = error);

         if (string.IsNullOrEmpty(manifestText))
         {
            Debug.LogWarning($"[HotUpdate] 获取清单失败，保持当前可用版本：{networkError}");
            ResKitContentLocator.Apply(currentState, out _);
            yield break;
         }

         // ---- 2. 解析 + 范围校验 ----
         if (!HotUpdateManifestService.TryParse(manifestText, out var manifest, out var parseError))
         {
            Debug.LogError($"[HotUpdate] 清单不可用，保持当前可用版本：{parseError}");
            ResKitContentLocator.Apply(currentState, out _);
            yield break;
         }

         if (!HotUpdateManifestService.ValidateScope(manifest, settings.channel, platform, out var scopeError))
         {
            Debug.LogError($"[HotUpdate] 清单与当前环境不匹配，保持当前可用版本：{scopeError}");
            ResKitContentLocator.Apply(currentState, out _);
            yield break;
         }

         if (HotUpdateManifestService.IsUpToDate(manifest, currentState))
         {
            Debug.Log($"[HotUpdate] 已是最新 contentVersion={manifest.contentVersion}，跳过下载。");
            ResKitContentLocator.Apply(currentState, out var upToDateMode);
            Debug.Log("[HotUpdate] 内容定位：" + upToDateMode);
            yield break;
         }

         // ---- 3. 下载 + 校验 ----
         var pending = HotUpdateManifestService.ComputePendingDownloads(manifest);
         var previousVersion = currentState?.contentVersion;
         Debug.Log($"[HotUpdate] 目标 contentVersion={manifest.contentVersion}，清单文件 {manifest.files.Length} 个，" +
                   $"需下载 {pending.Count} 个（上一版本={(string.IsNullOrEmpty(previousVersion) ? "<包内基线>" : previousVersion)}）。");

         var aborted = false;
         var abortReason = (string)null;

         foreach (var entry in pending)
         {
            var partPath = HotUpdateCacheService.PartPathFor(entry);
            var done = false;
            var downloadError = (string)null;
            var lastLoggedStep = -1;

            yield return backend.DownloadFileAsync(entry, partPath, progress =>
            {
               // 只在跨过 25% 台阶时打日志，避免大文件刷屏
               var step = Mathf.FloorToInt(Mathf.Clamp01(progress) * 4f);
               if (step != lastLoggedStep)
               {
                  lastLoggedStep = step;
                  Debug.Log($"[HotUpdate] 下载中 {entry.id} ({entry.FileName}) {step * 25}%");
               }
            }, () => done = true, error => downloadError = error);

            if (!done)
            {
               // 立刻清掉半成品：不让 .part 留在盘上被人误当可用内容，也不给下次启动留垃圾
               HotUpdateCacheService.DeletePart(entry);
               Debug.LogWarning($"[HotUpdate] 下载失败 {entry.id} ({entry.FileName})：{downloadError}");
               if (entry.required)
               {
                  aborted = true;
                  abortReason = $"必需文件下载失败：{entry.id} ({downloadError})";
                  break;
               }

               continue;
            }

            // 覆盖前先备份旧文件，保证本次更新可回滚
            HotUpdateCacheService.BackupBeforeOverwrite(HotUpdateCacheService.DestinationFor(entry), previousVersion);

            if (!HotUpdateCacheService.CommitPart(entry, out var commitError))
            {
               Debug.LogWarning($"[HotUpdate] 校验/落盘失败 {entry.id} ({entry.FileName})：{commitError}");
               if (entry.required)
               {
                  aborted = true;
                  abortReason = $"必需文件校验失败：{entry.id} ({commitError})";
                  break;
               }

               continue;
            }

            Debug.Log($"[HotUpdate] 已落地 {entry.id} -> {HotUpdateCacheService.DestinationFor(entry)}");
         }

         if (aborted)
         {
            // 关键：不写状态 = 新版本不算生效，旧文件仍由上一份状态兜住
            Debug.LogError($"[HotUpdate] 本次更新作废，保持当前可用版本：{abortReason}");
            ResKitContentLocator.Apply(currentState, out _);
            yield break;
         }

         // ---- 4. 全量复核 + 提交状态 ----
         if (!HotUpdateManifestService.VerifyAllRequiredFiles(manifest, out var verifyError))
         {
            Debug.LogError($"[HotUpdate] 更新未完整，不切换版本：{verifyError}");
            ResKitContentLocator.Apply(currentState, out _);
            yield break;
         }

         var newState = new HotUpdateCacheState
         {
            contentVersion = manifest.contentVersion,
            previousContentVersion = previousVersion,
            channel = manifest.channel,
            platform = platform,
            abSetComplete = manifest.abSetComplete,
            updatedAtUtc = DateTime.UtcNow.ToString("o"),
            files = HotUpdateManifestService.BuildCachedFiles(manifest),
         };

         if (!HotUpdateCacheService.SaveState(newState))
         {
            Debug.LogError("[HotUpdate] 写缓存状态失败：新内容不会在下次启动生效（本次仍按新内容加载）。");
         }

         HotUpdateCacheService.DeleteFilesNotIn(currentState, newState);

         ResKitContentLocator.Apply(newState, out var mode);
         Debug.Log($"[HotUpdate] OTA 完成：contentVersion={newState.contentVersion}，内容定位：{mode}");
      }
   }
}
