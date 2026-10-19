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

      /// <summary>
      /// 内容服务器根地址。**推荐留空**：留空时由 <see cref="LanOtaDiscovery"/> 自动定位
      /// （本机回环 → 上次成功地址 → UDP 广播），PC 换 IP 也不用改配置。
      /// 开发时要固定指向某台机器（或将来指向 CDN）才填这里。
      /// </summary>
      public string baseUrl = string.Empty;

      /// <summary>渠道名（服务端路由的第一段）。</summary>
      public string channel = "lan";

      /// <summary>清单请求超时（秒）。下载不设总时长上限。</summary>
      public float manifestTimeoutSeconds = 10f;

      /// <summary>
      /// 是否允许自动发现内容服务器（UDP 广播应答）。
      /// 关掉后只认 <see cref="baseUrl"/> 与本机回环，取不到就按「保持当前可用版本」处理。
      /// </summary>
      public bool autoDiscover = true;

      /// <summary>UDP 广播端口，必须与服务端 `--discovery-port` 一致（0 = 关闭广播）。</summary>
      public int discoveryPort = 8080;

      /// <summary>广播等待上限（秒）。到点没有可用应答就回落到「保持当前可用版本」。</summary>
      public float discoveryTimeoutSeconds = 1.5f;
   }

   /// <summary>OTA 阶段（登录场景的进度条按它显示文案）。</summary>
   public enum HotUpdateOtaStage
   {
      /// <summary>还没开始检查。</summary>
      Idle = 0,

      /// <summary>未启用或未配置服务器地址。</summary>
      Disabled = 1,

      /// <summary>正在取清单 / 比对版本。</summary>
      Checking = 2,

      /// <summary>已是最新内容，无需下载。</summary>
      UpToDate = 3,

      /// <summary>正在下载。</summary>
      Downloading = 4,

      /// <summary>下载并校验完成。</summary>
      Completed = 5,

      /// <summary>失败（网络 / 校验 / 解析），已回落到当前可用版本。</summary>
      Failed = 6,

      /// <summary>用户暂停（当前文件已中断，恢复后该文件从头重下）。</summary>
      Paused = 7,

      /// <summary>用户停止（本次更新未提交，保持当前可用版本）。</summary>
      Stopped = 8,
   }

   /// <summary>
   /// OTA 运行状态快照。UI 只读它，不关心下载细节，也不知道后端是谁。
   /// 由 <see cref="HotUpdateOtaService"/> 写入。
   /// </summary>
   public sealed class HotUpdateOtaStatus
   {
      /// <summary>当前阶段。</summary>
      public HotUpdateOtaStage Stage = HotUpdateOtaStage.Idle;

      /// <summary>给玩家看的短句（进度条上方的文案）。</summary>
      public string Message = "等待检查资源";

      /// <summary>总进度 0..1（按字节加权，不是文件数）。</summary>
      public float Progress;

      /// <summary>本地生效的内容版本（空 = 包内基线）。</summary>
      public string LocalVersion;

      /// <summary>远端内容版本（取到清单后才有）。</summary>
      public string RemoteVersion;

      /// <summary>失败原因（成功为 null）。</summary>
      public string Error;

      /// <summary>本次要处理的文件总数 / 已完成数。</summary>
      public int FilesTotal;
      public int FilesDone;

      /// <summary>是否正在跑（UI 据此决定是否接受重复触发、以及是否锁住「开始游戏」）。</summary>
      public bool IsRunning =>
         Stage == HotUpdateOtaStage.Checking ||
         Stage == HotUpdateOtaStage.Downloading ||
         Stage == HotUpdateOtaStage.Paused;

      /// <summary>是否被用户暂停。</summary>
      public bool IsPaused => Stage == HotUpdateOtaStage.Paused;

      /// <summary>是否已经下载完（可用于「开始游戏」判定）。</summary>
      public bool IsBlockingGameStart => IsRunning;
   }

   /// <summary>
   /// OTA 启动编排（23 文档 §2 / §3 / §7）。
   ///
   /// **两段职责，刻意分开**：
   /// 1. <see cref="ApplyCachedContentAtBoot"/>——启动时**不联网**，只清残留 `.part`、读缓存状态、
   ///    决定 ResKit 读哪套 AB 清单。它必须在 `HotUpdateLoader.LoadAll()` 与 `ResKit.InitAsync()`
   ///    之前跑完，因为热更 DLL 与 AB 清单的落点由它决定。
   /// 2. <see cref="RunCheck"/>——真正的联网检查与下载，由**登录场景**触发（登录场景本来就承担
   ///    「热更检查」这一步），这样下载进度可以可视化，也不会把网络等待塞进启动画面。
   ///
   /// 分成两段还有一个硬理由：`ResKit.InitAsync()` 在启动时就决定了读包内还是读 persistent 的 AB 清单，
   /// 所以登录阶段才下载到的清单要下次启动才生效；而**单个 bundle 是逐文件解析的**，
   /// 登录阶段下载完的 bundle 在本次会话后续场景加载里就能命中（详见 <see cref="ResKitContentLocator"/>）。
   ///
   /// **失败策略（重要）**：任何网络 / 校验 / 解析失败都**不阻断流程**，一律回落到「当前可用版本」
   /// （有 OTA 状态就按状态，没有就包内基线）。所以检查失败不会让游戏进不去。
   /// </summary>
   public static class HotUpdateOtaService
   {
      /// <summary>启动时传入的配置（登录场景随后复用，避免两处 Inspector 配置打架）。</summary>
      public static HotUpdateOtaSettings Settings { get; private set; }

      /// <summary>当前状态快照（UI 每帧读）。</summary>
      public static HotUpdateOtaStatus Status { get; } = new HotUpdateOtaStatus();

      /// <summary>用户是否请求停止本次更新（中断传输后整轮结束，不提交版本）。</summary>
      private static bool sStopRequested;

      /// <summary>用户是否请求暂停（中断当前文件后挂起，恢复时该文件从头重下）。</summary>
      private static bool sPauseRequested;

      /// <summary>启动编排是否已经跑过（<see cref="ApplyCachedContentAtBoot"/> 完成后为 true）。</summary>
      private static bool sBootApplied;

      /// <summary>
      /// 等待启动编排交出 <see cref="Settings"/> 的上限（秒）。
      ///
      /// **为什么需要等**：编辑器里如果你直接从 `scene_0001_login` 进 Play，ConfigScene 是之后被
      /// `Editor_ConfigSceneGuard` 拉起来的，于是登录场景的 `Start()` 会**早于** `AppBootstrapper.Start()`。
      /// 此时 `Settings` 还是 null，`RunCheck` 会直接判定「未启用」——这就是编辑器里看到
      /// `[HotUpdate] OTA 未启用或未配置服务器地址` 而配置明明是开着的真正原因（跟 enabled 默认值无关）。
      /// </summary>
      private const float BootWaitSeconds = 5f;

      /// <summary>
      /// OTA 协程的常驻宿主（ConfigScene 的 <c>AppBootstrapper</c>）。
      ///
      /// **为什么不能挂在登录场景上**：登录场景是玩法场景，用户点「开始游戏」就会卸载它，
      /// 挂在上面的下载协程会被直接杀死——表现为「下载到一半就没了，且没有任何报错」。
      /// ConfigScene 永不卸载（决策 D20 的入口场景保护），所以协程必须挂它。
      /// </summary>
      public static MonoBehaviour Host { get; private set; }

      /// <summary>由启动编排注册常驻宿主。</summary>
      public static void RegisterHost(MonoBehaviour host)
      {
         if (host != null)
         {
            Host = host;
         }
      }

      /// <summary>
      /// 在常驻宿主上启动一次检查（已在跑则忽略）。
      /// </summary>
      /// <param name="fallbackHost">没有注册常驻宿主时的兜底宿主（编辑器直接从登录场景 Play 的场景）。</param>
      /// <returns>是否真的启动了。</returns>
      public static bool TryStartCheck(MonoBehaviour fallbackHost = null)
      {
         if (Status.IsRunning)
         {
            return false;
         }

         var host = Host != null ? Host : fallbackHost;
         if (host == null)
         {
            Status.Stage = HotUpdateOtaStage.Failed;
            Status.Message = "资源检查未能启动：没有可用的常驻宿主";
            Debug.LogWarning("[HotUpdate] 无法启动检查：常驻宿主未注册，且没有兜底宿主。");
            return false;
         }

         host.StartCoroutine(RunCheck());
         return true;
      }

      /// <summary>用户请求停止本次更新（中断传输、丢弃半成品、不提交版本）。</summary>
      public static void RequestStop()
      {
         if (!Status.IsRunning)
         {
            return;
         }

         Debug.Log("[HotUpdate] 用户请求停止资源更新。");
         sStopRequested = true;
         sPauseRequested = false;   // 停止优先于暂停，解除挂起
      }

      /// <summary>用户请求暂停（下载中才有效）。</summary>
      public static void RequestPause()
      {
         if (Status.Stage != HotUpdateOtaStage.Downloading)
         {
            return;
         }

         Debug.Log("[HotUpdate] 用户请求暂停资源更新。");
         sPauseRequested = true;
      }

      /// <summary>用户请求继续（暂停中才有效）。</summary>
      public static void RequestResume()
      {
         if (!sPauseRequested)
         {
            return;
         }

         Debug.Log("[HotUpdate] 用户请求继续资源更新。");
         sPauseRequested = false;
      }

      /// <summary>
      /// 启动阶段：清残留临时文件、读缓存、决定 ResKit 内容定位。**不联网。**
      /// 调用方在 `HotUpdateLoader.LoadAll()` 与 `ResKit.InitAsync()` 之前调用，
      /// 不满足「先落盘后加载」的顺序就会白下。
      /// </summary>
      /// <returns>
      /// **现在能不能直接初始化 ResKit**。
      /// - true：有可用 AB 清单（持久层 OTA 内容自洽，或包内带基线）；
      /// - false：**薄包首启**——包内没有基线、持久层也还没下下来。
      ///   此时必须**推迟** `ResKit.InitAsync()`（它在 `ResMgr` 里是一次性的，跑一次就置位，
      ///   失败后再也补不回来），等登录场景把资源下完再初始化。
      /// </returns>
      public static bool ApplyCachedContentAtBoot(HotUpdateOtaSettings settings)
      {
         Settings = settings;

         // 上次中断留下的 .part 一律清掉：它不是可用内容，留着只会占空间并误导排障
         HotUpdateCacheService.CleanupStaleParts();

         var state = HotUpdateCacheService.TryLoadState();
         Status.LocalVersion = state?.contentVersion;

         // 只在还没开始时写文案：登录场景可能已经先跑起检查（编辑器从登录场景进 Play 的顺序），
         // 这里再无条件改写 Status 会把「正在检查」覆盖回 Idle。
         if (Status.Stage == HotUpdateOtaStage.Idle)
         {
            Status.Message = string.IsNullOrEmpty(Status.LocalVersion) ? "当前使用包内基线" : "当前使用已下载内容";
         }

         var persistentReady = ResKitContentLocator.Apply(state, out var reason);
         var ready = persistentReady || HasPackageBaseline(out var packageReason);

         Debug.Log($"[HotUpdate] 启动内容定位：{reason}（本地版本=" +
                   $"{(string.IsNullOrEmpty(Status.LocalVersion) ? "<包内基线>" : Status.LocalVersion)}）；" +
                   $"包内基线={(HasPackageBaseline(out _) ? "有" : "无")}；ResKit 可现在初始化={ready}");

         if (!ready)
         {
            Debug.LogWarning("[HotUpdate] 薄包首启：包内没有 AB 基线、持久层也没有可用内容。" +
                             "本次**推迟** ResKit 初始化，等登录场景下载完成后再 init。");
            Status.Message = "首次启动：需要先下载资源";
         }

         sBootApplied = true;
         sBootContentReady = ready;
         return ready;
      }

      /// <summary>编辑器中（SimulationMode）ResKit 从 AssetDatabase 建清单，与包内 AB 无关。</summary>
      private static bool IsSimulationMode => QFramework.AssetBundlePathHelper.SimulationMode;

      /// <summary>
      /// 包内是否带 AB 基线。由构建画像（`Assets/Resources/BuildProfile.txt`）决定——
      /// Android 上 StreamingAssets 在 APK 内，运行期没法用 `File.Exists` 判断。
      /// </summary>
      private static bool HasPackageBaseline(out string reason)
      {
         if (IsSimulationMode)
         {
            reason = "编辑器 SimulationMode：清单来自 AssetDatabase";
            return true;
         }

         if (!BuildProfile.PackageHasAssetBundles)
         {
            reason = "构建画像标注为薄包（包内不含 AB）";
            return false;
         }

         reason = "构建画像标注包内带 AB 基线";
         return true;
      }

      /// <summary>
      /// ResKit 是否已经可用（可以加载 AB 场景/面板）。UI 用它决定是否锁住「开始游戏」。
      /// </summary>
      public static bool IsResKitReady => IsSimulationMode || QFramework.ResMgr.ResMgrInited;

      /// <summary>启动编排判定过「现在就能初始化 ResKit」（有包内基线或有自洽的持久层内容）。</summary>
      private static bool sBootContentReady;

      /// <summary>
      /// 现在能不能初始化 ResKit。供 <see cref="ResKitBootstrap.EnsureInitialized"/> 这类兜底路径用，
      /// 避免薄包首启被抢先初始化（ResMgr 的初始化一次性，抢跑即毒死整条链路）。
      /// </summary>
      public static bool CanInitializeResKitNow()
      {
         if (IsResKitReady || sBootContentReady)
         {
            return true;
         }

         // 已经下载完但还没来得及 init（下载完成到 InitAsync 之间的窗口）
         var state = HotUpdateCacheService.TryLoadState();
         return ResKitContentLocator.Apply(state, out _) || HasPackageBaseline(out _);
      }

      /// <summary>
      /// 薄包首启：资源下载完成后补上被推迟的 ResKit 初始化。
      /// **顺序不能反**——必须先按新状态切好内容定位（读 persistent 的清单），再 InitAsync。
      /// </summary>
      private static IEnumerator InitResKitIfDeferred(HotUpdateCacheState state)
      {
         if (QFramework.ResMgr.ResMgrInited)
         {
            yield break;
         }

         if (state == null)
         {
            yield break;
         }

         ResKitContentLocator.Apply(state, out var mode);
         Debug.Log($"[HotUpdate] 资源已就绪，补做被推迟的 ResKit 初始化（{mode}）");

         yield return QFramework.ResKit.InitAsync();

         Debug.Log($"[HotUpdate] ResKit 初始化完成，IsResKitReady={IsResKitReady}（清单可解析=" +
                   $"{ResKitBootstrap.TryResolve(QAssetBundle.Main_menupanel_prefab.BundleName, QAssetBundle.Main_menupanel_prefab.Main_MenuPanel)}）");
      }

      /// <summary>
      /// 联网检查并下载（登录场景调用）。用 `yield return` 等待完成；
      /// 过程中持续更新 <see cref="Status"/>，UI 据此画进度条。
      /// </summary>
      public static IEnumerator RunCheck()
      {
         // 等启动编排交出配置：编辑器从登录场景进 Play 时，ConfigScene（以及它的 AppBootstrapper）
         // 可能比登录场景晚一步初始化。这里等一会儿而不是立刻判定「未启用」。
         var bootDeadline = Time.realtimeSinceStartup + BootWaitSeconds;
         while (!sBootApplied && Time.realtimeSinceStartup < bootDeadline)
         {
            yield return null;
         }

         if (!sBootApplied)
         {
            Debug.LogWarning("[HotUpdate] 等待启动编排超时（AppBootstrapper 未执行？），本次不检查资源。");
            yield break;
         }

         var settings = Settings;

         sStopRequested = false;
         sPauseRequested = false;
         Status.Error = null;
         Status.Progress = 0f;
         Status.FilesDone = 0;
         Status.FilesTotal = 0;

         if (settings == null || !settings.enabled)
         {
            Status.Stage = HotUpdateOtaStage.Disabled;
            Status.Message = "未启用资源更新（使用包内基线）";
            Debug.Log("[HotUpdate] OTA 未启用：" +
                      $"enabled={(settings == null ? "<无配置>" : settings.enabled.ToString())}。" +
                      "配置位置：scene_0000_config 的 AppBootstrapper → ota。本次使用包内基线。");
            yield break;
         }

         if (!settings.autoDiscover && string.IsNullOrEmpty(settings.baseUrl))
         {
            Status.Stage = HotUpdateOtaStage.Disabled;
            Status.Message = "未配置服务器地址，且未开启自动发现";
            Debug.LogWarning("[HotUpdate] ota.baseUrl 为空且 autoDiscover=false：无从定位服务器，本次使用包内基线。");
            yield break;
         }

         var currentState = HotUpdateCacheService.TryLoadState();
         Status.LocalVersion = currentState?.contentVersion;
         var platform = HotUpdateCacheService.PlatformName;

         // ---- 0. 定位内容服务器（PC 的局域网 IP 会变，不能写死） ----
         Status.Stage = HotUpdateOtaStage.Checking;
         Status.Message = "正在定位资源服务器…";

         string baseUrl = null;
         string discoverError = null;
         yield return LanOtaDiscovery.ResolveAsync(settings.baseUrl, settings.channel, platform,
            settings.discoveryPort, settings.discoveryTimeoutSeconds, settings.autoDiscover,
            url => baseUrl = url, reason => discoverError = reason);

         if (string.IsNullOrEmpty(baseUrl))
         {
            yield return Fail($"定位内容服务器失败：{discoverError}", currentState);
            yield break;
         }

         var backend = new LocalHttpBackend(baseUrl, settings.channel, platform, settings.manifestTimeoutSeconds);

         Status.Message = "正在检查资源版本…";
         Debug.Log($"[HotUpdate] OTA 检查开始：backend={backend.DisplayName}, channel={settings.channel}, platform={platform}," +
                   $" 本地版本={(string.IsNullOrEmpty(Status.LocalVersion) ? "<包内基线>" : Status.LocalVersion)}");

         // ---- 1. 取清单 ----
         string manifestText = null;
         string networkError = null;
         yield return backend.GetManifestAsync(text => manifestText = text, error => networkError = error);

         if (string.IsNullOrEmpty(manifestText))
         {
            yield return Fail($"获取清单失败：{networkError}", currentState);
            yield break;
         }

         // ---- 2. 解析 + 范围校验 ----
         if (!HotUpdateManifestService.TryParse(manifestText, out var manifest, out var parseError))
         {
            yield return Fail($"清单不可用：{parseError}", currentState);
            yield break;
         }

         Status.RemoteVersion = manifest.contentVersion;

         if (!HotUpdateManifestService.ValidateScope(manifest, settings.channel, platform, out var scopeError))
         {
            yield return Fail($"清单与当前环境不匹配：{scopeError}", currentState);
            yield break;
         }

         if (HotUpdateManifestService.IsUpToDate(manifest, currentState))
         {
            Status.Stage = HotUpdateOtaStage.UpToDate;
            Status.Progress = 1f;
            Status.Message = "资源已是最新（" + manifest.contentVersion + "）";
            Debug.Log($"[HotUpdate] 已是最新 contentVersion={manifest.contentVersion}，跳过下载。");
            ResKitContentLocator.Apply(currentState, out _);

            // 内容已齐但 ResKit 可能还没初始化（薄包首启走的就是这条路：启动时没内容 → 推迟 init）
            yield return InitResKitIfDeferred(currentState);
            yield break;
         }

         // ---- 3. 下载 + 校验（进度按字节加权） ----
         var pending = HotUpdateManifestService.ComputePendingDownloads(manifest);
         var previousVersion = currentState?.contentVersion;
         long totalBytes = 0;
         foreach (var entry in pending)
         {
            totalBytes += entry.size > 0 ? entry.size : 1;
         }

         Status.FilesTotal = pending.Count;
         Status.Stage = HotUpdateOtaStage.Downloading;
         Status.Message = pending.Count == 0 ? "正在校验已下载内容…" : $"正在下载 {pending.Count} 个文件";
         Debug.Log($"[HotUpdate] 目标 contentVersion={manifest.contentVersion}，清单文件 {manifest.files.Length} 个，" +
                   $"需下载 {pending.Count} 个（上一版本={(string.IsNullOrEmpty(previousVersion) ? "<包内基线>" : previousVersion)}）。");

         long downloadedBytes = 0;
         var aborted = false;
         string abortReason = null;

         foreach (var entry in pending)
         {
            if (sStopRequested)
            {
               break;
            }

            var entrySize = entry.size > 0 ? entry.size : 1;
            var baseBytes = downloadedBytes;
            var entryDone = false;
            string entryError = null;

            // 同一个文件可能要重下多次：暂停时当前文件被中断（服务端无 Range 不能续传），恢复后整文件重来
            while (!entryDone)
            {
               // 暂停挂起：不发网络请求，也不推进进度，等恢复或停止
               while (sPauseRequested && !sStopRequested)
               {
                  Status.Stage = HotUpdateOtaStage.Paused;
                  Status.Message = $"已暂停：{entry.FileName}（恢复后该文件从头重下）";
                  yield return null;
               }

               if (sStopRequested)
               {
                  break;
               }

               Status.Stage = HotUpdateOtaStage.Downloading;
               Status.Message = $"正在下载 {Status.FilesDone + 1}/{Status.FilesTotal}：{entry.FileName}";

               var done = false;
               string downloadError = null;
               var lastLoggedStep = -1;
               var partPath = HotUpdateCacheService.PartPathFor(entry);

               yield return backend.DownloadFileAsync(entry, partPath, progress =>
               {
                  // 只在跨过 25% 台阶时打日志，避免大文件刷屏
                  var step = Mathf.FloorToInt(Mathf.Clamp01(progress) * 4f);
                  if (step != lastLoggedStep)
                  {
                     lastLoggedStep = step;
                     Debug.Log($"[HotUpdate] 下载中 {entry.id} ({entry.FileName}) {step * 25}%");
                  }

                  if (totalBytes > 0)
                  {
                     Status.Progress = Mathf.Clamp01((baseBytes + entrySize * Mathf.Clamp01(progress)) / (float)totalBytes);
                  }
               }, () => done = true, error => downloadError = error,
                  () => sStopRequested || sPauseRequested);

               if (done)
               {
                  entryDone = true;
                  break;
               }

               // 半成品一律删掉：它不是可用内容，也不能当断点
               HotUpdateCacheService.DeletePart(entry);

               if (sStopRequested || sPauseRequested)
               {
                  Debug.Log($"[HotUpdate] 已中断 {entry.id}（{(sStopRequested ? "停止" : "暂停")}），半成品已删除");
                  continue;   // 回到 while 顶部：暂停就挂起，停止就跳出
               }

               entryError = downloadError;
               Debug.LogWarning($"[HotUpdate] 下载失败 {entry.id} ({entry.FileName})：{entryError}");
               break;
            }

            if (sStopRequested)
            {
               break;
            }

            if (!entryDone)
            {
               if (entry.required)
               {
                  aborted = true;
                  abortReason = $"必需文件下载失败：{entry.id}（{entryError}）";
                  break;
               }

               downloadedBytes += entrySize;
               Status.FilesDone++;
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
                  abortReason = $"必需文件校验失败：{entry.id}（{commitError}）";
                  break;
               }

               downloadedBytes += entrySize;
               Status.FilesDone++;
               continue;
            }

            Debug.Log($"[HotUpdate] 已落地 {entry.id} -> {HotUpdateCacheService.DestinationFor(entry)}");
            downloadedBytes += entrySize;
            Status.FilesDone++;
            if (totalBytes > 0)
            {
               Status.Progress = Mathf.Clamp01(downloadedBytes / (float)totalBytes);
            }
         }

         if (sStopRequested)
         {
            // 用户主动停止：不写状态 = 新版本不生效，已落地的文件由上一份状态兜住（下次仍会重下缺失文件）
            sPauseRequested = false;
            Status.Stage = HotUpdateOtaStage.Stopped;
            Status.Message = "已停止资源更新（保持当前可用版本）";
            Status.Error = null;
            Debug.LogWarning("[HotUpdate] 用户已停止资源更新，未提交新版本。");
            ResKitContentLocator.Apply(currentState, out _);
            yield break;
         }

         if (aborted)
         {
            // 关键：不写状态 = 新版本不算生效，旧文件仍由上一份状态兜住
            yield return Fail($"本次更新作废，保持当前可用版本：{abortReason}", currentState);
            yield break;
         }

         // ---- 4. 全量复核 + 提交状态 ----
         if (!HotUpdateManifestService.VerifyAllRequiredFiles(manifest, out var verifyError))
         {
            yield return Fail($"更新未完整，不切换版本：{verifyError}", currentState);
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
            Debug.LogError("[HotUpdate] 写缓存状态失败：新内容不会在下次启动生效。");
         }

         HotUpdateCacheService.DeleteFilesNotIn(currentState, newState);
         Status.LocalVersion = newState.contentVersion;
         Status.Progress = 1f;

         ResKitContentLocator.Apply(newState, out var mode);
         Debug.Log($"[HotUpdate] OTA 完成：contentVersion={newState.contentVersion}，内容定位：{mode}");

         Status.Stage = HotUpdateOtaStage.Completed;
         Status.Message = "资源已更新到 " + newState.contentVersion +
                          (newState.abSetComplete ? "（整套资源下次启动完整生效）" : "");

         // 薄包首启：启动时没有任何内容 → 推迟了 ResKit 初始化，这里补上
         yield return InitResKitIfDeferred(newState);
      }

      /// <summary>失败收尾：记录原因、回落到当前可用版本。</summary>
      private static IEnumerator Fail(string reason, HotUpdateCacheState currentState)
      {
         Status.Stage = HotUpdateOtaStage.Failed;
         Status.Error = reason;
         Status.Message = "资源更新失败：" + reason;
         Debug.LogWarning($"[HotUpdate] {reason}");

         ResKitContentLocator.Apply(currentState, out _);
         yield break;
      }
   }
}
