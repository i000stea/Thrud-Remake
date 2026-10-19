using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using QFramework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TeaGame.Tools.Editor
{
   /// <summary>
   /// Demo Android 构建工具（T2 / 13 §3.3；**使用说明与真机验收清单见
   /// `Docs/01_里程碑与执行/15_Demo打包与外放_构建说明与真机验收清单.md`**）。
   ///
   /// 菜单（`Thrud/Demo/Android/`）：
   ///   0. 构建前置检查 (只读)   —— 只读门禁：编译状态 / 目标平台 / 关键设置 / 场景 / link.xml /
   ///                              编目 AB 标记 / AB 产物是否过期。**出包前先跑这条**。
   ///   1. Switch Platform to Android —— 切 Android 目标（会触发脚本重编译与域重载，故单独一步）。
   ///   2. Build AssetBundles (Android) —— ResKit 打 AB + **校验产物**（每个已标记 bundle 都要在盘上）。
   ///   3. Build APK (Release)   —— 非 Development，debug keystore，直接外放（正式签名后补）。
   ///   4. Build APK (Development) —— 联调用（挂调试/看日志）。
   ///   5. Build All (One-Click) —— 门禁 → 打 AB → 校验 AB → 出 Release APK → 写构建报告。
   ///   6. 版本号 +1             —— bundleVersion 末位数字 +1，并同步 versionCode（外放每个包可区分）。
   ///   7. 清理 AB 产物目录中的孤儿文件 —— 删掉"在盘上但不在清单里"的旧产物（StreamingAssets 整体进 APK，
   ///                              残留 = 白送的包体；实测一例残留 44.7MB）。
   ///
   /// 设计要点（都是踩过坑换来的，别改回去）：
   /// - **不做"自动切平台后继续构建"**：`SwitchActiveBuildTarget` 会触发域重载，同一菜单调用里的后续步骤
   ///   随时可能被中断；更危险的是 **AB 按"当前活动目标"打包**（`ResKitEditorAPI.BuildAssetBundles()` 内部
   ///   取 `EditorUserBuildSettings.activeBuildTarget`），若目标不是 Android 就打出了错误平台的 AB，
   ///   而 APK 却是 Android → 真机面板全空 / 编目解析失败。故菜单 2–5 在目标非 Android 时**直接中止**。
   /// - **AB 打完必须回读产物**：ResKit 构建只回报 Console，不做产物断言；"构建成功但盘上没有"是最贵的假成功。
   /// - **包名/AB 常量脱钩是历史坑**（09 交接关键坑 30）：编目 AB 名以代码常量为准，用默认名只出警告不阻断。
   /// - 产物 `Build/Android/ThrudDemo_&lt;版本&gt;_&lt;versionCode&gt;_&lt;dev|release&gt;.apk`（`Build/`、`*.apk` 均已 .gitignore）。
   /// </summary>
   public static class AndroidDemoBuilder
   {
      private const string MenuRoot = "Thrud/Demo/Android/";
      private const string BundleId = "com.i000stea.thrudremark";

      private const string ApkFolderRelative = "Build/Android";
      private static string ApkFolderAbsolute => Path.GetFullPath(Path.Combine(Application.dataPath, "../", ApkFolderRelative));

      /// <summary>包体目标上限（13 §3.3「包体控制目标 &lt;200MB」）。超过只告警，不阻断出包。</summary>
      private const long ApkSizeWarnBytes = 200L * 1024 * 1024;

      /// <summary>战斗出战编目资产：真机取角色/武器资源的**唯一根资产**（见 14 文档 D2/D3 与 09 关键坑 28）。</summary>
      private const string CatalogAssetPath = "Assets/Data/BattleTeamCatalog.asset";

      /// <summary>编目 AB 的规范名（与 <c>BattleAssetService.RoleDataBundleName</c> 必须一致）。</summary>
      private const string RoleBundleName = "battle_characters";

      /// <summary>Inspector 默认名兜底（历史标记，见 09 关键坑 30）；命中即告警建议改规范名。</summary>
      private const string RoleBundleFallbackName = "battleteamcatalog_asset";

      /// <summary>IL2CPP 引擎类型保留（09 关键坑 27：动态 AddComponent 的类型会被 Strip Engine Code 剥离）。</summary>
      private const string LinkXmlPath = "Assets/link.xml";

      private static readonly List<string> sGateLog = new List<string>();

      // ---------------- 菜单 ----------------

      [MenuItem(MenuRoot + "0. 构建前置检查 (只读)", false, 0)]
      public static void PreflightMenu()
      {
         var ok = Preflight(abRequired: false);
         Debug.Log(ok
            ? "[T2] 前置检查通过：可以执行「5. Build All (One-Click)」。"
            : "[T2] 前置检查未通过：按上面最后一条 ❌ 提示处理后再出包。");
      }

      [MenuItem(MenuRoot + "1. Switch Platform to Android", false, 10)]
      public static void SwitchToAndroid()
      {
         EnsureAndroidPlatform(allowSwitch: true);
         Debug.Log("[T2] 切平台结束。**请等编译完成后重新执行「5. Build All (One-Click)」**（切平台会触发域重载，本菜单不继续出包）。");
      }

      [MenuItem(MenuRoot + "2. Build AssetBundles (Android)", false, 20)]
      public static void BuildAssetBundlesMenu()
      {
         if (!EnsureAndroidPlatform(allowSwitch: false)) return;
         EnsurePlayerSettings();
         if (!Preflight(abRequired: false)) return;
         BuildResKitAssetBundles();
      }

      /// <summary>
      /// **一键瘦体出包（编辑器内）**：门禁 → 重打 AB → 瘦体 APK → 发布 OTA 内容。
      ///
      /// 与命令行 `build-thin.cmd`（`AndroidBuildCli.BuildAllThin`）**走同一份代码**，
      /// 所以编辑器点菜单与 CI 跑批处理的结果一致，不存在两条路径漂移的问题。
      /// 批处理下这条会自己落退出码；编辑器内只打日志、不退出。
      /// </summary>
      [MenuItem(MenuRoot + "0. 一键瘦体出包（AB + 瘦体 APK + 发布 OTA）", false, 0)]
      public static void OneClickThinMenu()
      {
         AndroidBuildCli.BuildAllThin();
      }

      [MenuItem(MenuRoot + "3. Build APK (Release · debug keystore)", false, 30)]
      public static void BuildApkReleaseMenu()
      {
         if (!EnsureAndroidPlatform(allowSwitch: false)) return;
         EnsurePlayerSettings();
         if (!Preflight(abRequired: true)) return;
         BuildPlayerApk(development: false, thin: false);
      }

      [MenuItem(MenuRoot + "4. Build APK (Development · 联调)", false, 40)]
      public static void BuildApkDevMenu()
      {
         if (!EnsureAndroidPlatform(allowSwitch: false)) return;
         EnsurePlayerSettings();
         if (!Preflight(abRequired: true)) return;
         BuildPlayerApk(development: true, thin: false);
      }

      /// <summary>
      /// 一键出包（**默认薄包**：包内不含 AB，资源全走 OTA），但**不发布** OTA 内容。
      /// 要连 OTA 内容一起更新，用「0. 一键瘦体出包」或命令行 `build-thin.cmd`。
      /// </summary>
      [MenuItem(MenuRoot + "5. Build All (One-Click · 校验 + AB + 瘦体 APK)", false, 50)]
      public static void BuildAllMenu()
      {
         if (!EnsureAndroidPlatform(allowSwitch: false)) return;
         EnsurePlayerSettings();
         if (!Preflight(abRequired: false)) return;
         if (!BuildResKitAssetBundles()) return;   // AB 产物校验不通过就不出包（否则真机必然空屏）
         if (!Preflight(abRequired: true)) return;  // AB 打完后按"产物齐备"的标准再过一次门禁
         BuildPlayerApk(development: false, thin: true);
      }

      [MenuItem(MenuRoot + "8. Build APK (Thin · 包内不含 AB，资源全走 OTA)", false, 60)]
      public static void BuildApkThinMenu()
      {
         if (!EnsureAndroidPlatform(allowSwitch: false)) return;
         EnsurePlayerSettings();

         // 仍然要求 AB 产物齐备：这次不出进包，但要拿来发布成 OTA 内容
         if (!Preflight(abRequired: true)) return;

         BuildPlayerApk(development: false, thin: true);
      }

      // ---------------- 命令行入口用的公共包装（只返回成败，不做任何交互） ----------------

      /// <summary>
      /// 命令行：只重打 ResKit AssetBundle。返回是否成功。
      /// 供 `-executeMethod` 使用，逻辑与菜单同源。
      /// </summary>
      public static bool CliBuildAssetBundles()
      {
         EnsurePlayerSettings();
         return BuildResKitAssetBundles();
      }

      /// <summary>命令行：出一份 APK（<paramref name="thin"/>=true 为薄包）。返回是否成功。</summary>
      public static bool CliBuildApk(bool development, bool thin)
      {
         if (!EnsureAndroidPlatform(allowSwitch: false)) return false;
         EnsurePlayerSettings();
         if (!Preflight(abRequired: true)) return false;
         return BuildPlayerApk(development, thin);
      }

      /// <summary>命令行：一键 = 门禁 + AB + 瘦体 APK。返回是否成功。</summary>
      public static bool CliBuildAllThin(bool development)
      {
         if (!EnsureAndroidPlatform(allowSwitch: false)) return false;
         EnsurePlayerSettings();
         if (!Preflight(abRequired: false)) return false;
         if (!BuildResKitAssetBundles()) return false;
         if (!Preflight(abRequired: true)) return false;
         return BuildPlayerApk(development, thin: true);
      }

      /// <summary>薄包构建期间的产物暂存目录（在 Assets 之外，因此不会被扫进 StreamingAssets）。</summary>
      private static string ThinHoldRoot =>
         Path.GetFullPath(Path.Combine(Application.dataPath, "../", "AssetBundles.thinhold"));

      /// <summary>
      /// 把整个目录（连同它的 `.meta`）在文件系统里搬来搬去。
      /// `.meta` 一起搬是为了还原时 GUID 不变，避免无谓的重新导入。
      /// </summary>
      private static void MoveAssetDirectory(string from, string to)
      {
         if (!Directory.Exists(from))
         {
            Debug.LogWarning($"[T2] 目录不存在，跳过移动：{from}");
            return;
         }

         var parent = Path.GetDirectoryName(to);
         if (!string.IsNullOrEmpty(parent))
         {
            Directory.CreateDirectory(parent);
         }

         if (Directory.Exists(to))
         {
            Directory.Delete(to, true);
         }

         Directory.Move(from, to);

         var metaFrom = from + ".meta";
         var metaTo = to + ".meta";
         if (!File.Exists(metaFrom)) return;

         if (File.Exists(metaTo))
         {
            File.Delete(metaTo);
         }

         File.Move(metaFrom, metaTo);
      }

      /// <summary>
      /// 写构建画像（`Assets/Resources/BuildProfile.txt`），运行时据此判断本包是否携带 AB 基线。
      /// </summary>
      private static void WriteBuildProfile(bool packageHasBundles)
      {
         const string assetDir = "Assets/Resources";
         const string assetPath = assetDir + "/BuildProfile.txt";

         Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../", assetDir)));

         var content =
            "# 构建画像（自动生成，勿手改）\n" +
            $"package.bundles={(packageHasBundles ? "true" : "false")}\n" +
            $"builtAtUtc={System.DateTime.UtcNow:o}\n";

         // 不带 BOM：与热更清单一致，避免运行期解析时多出 U+FEFF
         File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../", assetPath)), content,
            new System.Text.UTF8Encoding(false));

         AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
         Debug.Log($"[T2] 构建画像已写入：package.bundles={(packageHasBundles ? "true" : "false")}");
      }

      [MenuItem(MenuRoot + "6. 版本号 +1 (bundleVersion / versionCode 同步)", false, 90)]
      public static void BumpVersionMenu()
      {
         var current = PlayerSettings.bundleVersion ?? "0.0.0";
         var next = IncrementTrailingNumber(current);
         var nextCode = PlayerSettings.Android.bundleVersionCode + 1;

         PlayerSettings.bundleVersion = next;
         PlayerSettings.Android.bundleVersionCode = nextCode;

         Debug.Log($"[T2] 版本号已更新：bundleVersion {current} → {next}，versionCode → {nextCode}。" +
                   "（APK 文件名包含两者，外放时每个包都可区分。）");
      }

      [MenuItem(MenuRoot + "7. 清理 AB 产物目录中的孤儿文件", false, 100)]
      public static void CleanOrphanBundlesMenu()
      {
         var abDir = AssetBundleOutputDir();
         var orphans = FindOrphanBundleFiles(abDir);
         if (orphans.Count == 0)
         {
            Debug.Log($"[T2] AB 产物目录没有孤儿文件（{Relative(abDir)}）。");
            return;
         }

         var total = 0L;
         for (var i = 0; i < orphans.Count; i++)
         {
            var size = new FileInfo(orphans[i]).Length;
            total += size;
            Debug.LogWarning($"[T2] 删除孤儿产物：{Relative(orphans[i])}（{size / 1024f / 1024f:F1} MB）");
            File.Delete(orphans[i]);

            var meta = orphans[i] + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
         }

         AssetDatabase.Refresh();
         Debug.Log($"[T2] 已清理 {orphans.Count} 个孤儿 AB 产物，释放 {total / 1024f / 1024f:F1} MB。" +
                   "（这些文件不在 AB 清单里，但 StreamingAssets 是整体进 APK 的，留着就是纯浪费的包体。）");
      }

      // ---------------- 前置检查（门禁） ----------------

      /// <summary>
      /// 只读门禁。返回 false = 有阻断项（❌），调用方应中止本次出包。
      /// <paramref name="abRequired"/> = true 时额外要求 AB 产物齐备（出 APK 前用）。
      /// </summary>
      private static bool Preflight(bool abRequired)
      {
         sGateLog.Clear();
         var fatal = false;

         void Ok(string msg) => Record("✅ " + msg);
         void Warn(string msg) => Record("⚠️ " + msg);
         void Bad(string msg)
         {
            fatal = true;
            Record("❌ " + msg);
         }

         // 1) 编译状态
         if (EditorApplication.isCompiling)
         {
            Bad("编辑器正在编译脚本，请等编译结束再出包。");
         }
         else if (EditorUtility.scriptCompilationFailed)
         {
            Bad("存在脚本编译错误（Console 有 CS 报错）：IL2CPP 出包必然失败，先修编译。");
         }
         else
         {
            Ok("脚本编译状态正常（无 CS 报错）。");
         }

         // 2) 目标平台
         var target = EditorUserBuildSettings.activeBuildTarget;
         if (target == BuildTarget.Android)
         {
            Ok("活动构建目标是 Android。");
         }
         else
         {
            Bad($"活动构建目标是 {target}，不是 Android：请先执行「1. Switch Platform to Android」，" +
                "等编译结束后再回来出包（AB 会按当前目标打包，切错平台 = 真机拿不到 AB）。");
         }

         // 3) 关键设置快照
         Ok($"包名 {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}；" +
            $"版本 {PlayerSettings.bundleVersion} / Code {PlayerSettings.Android.bundleVersionCode}；" +
            $"MinSdk {(int)PlayerSettings.Android.minSdkVersion}；" +
            $"架构 {PlayerSettings.Android.targetArchitectures}；" +
            $"朝向 {PlayerSettings.defaultInterfaceOrientation}；" +
            $"keystore {(PlayerSettings.Android.useCustomKeystore ? "自定义" : "debug（内测外放）")}。");

         // 4) 场景列表
         var scenes = EnabledScenes();
         if (scenes.Length == 0)
         {
            Bad("Build Settings 里没有启用的场景，无法出包。");
         }
         else
         {
            var missing = scenes.Where(p => !File.Exists(ToAbsolute(p))).ToArray();
            if (missing.Length > 0)
            {
               Bad($"Build Settings 引用了不存在的场景：{string.Join("、", missing)}。");
            }
            else
            {
               Ok($"Build Settings 启用场景 {scenes.Length} 个：{string.Join(" → ", scenes.Select(Path.GetFileNameWithoutExtension))}。");
            }
         }

         // 5) link.xml（引擎类型保留；缺失时真机动态 AddComponent 会报 "class 'MeshCollider' doesn't exist"）
         if (File.Exists(ToAbsolute(LinkXmlPath)))
         {
            var text = File.ReadAllText(ToAbsolute(LinkXmlPath));
            if (text.Contains("MeshCollider"))
            {
               Ok($"{LinkXmlPath} 存在且保留了 MeshCollider（Strip Engine Code 仍开启，属预期）。");
            }
            else
            {
               Warn($"{LinkXmlPath} 里没看到 MeshCollider：若真机报 class 'MeshCollider' doesn't exist，按 09 关键坑 27 补。");
            }
         }
         else
         {
            Warn($"未找到 {LinkXmlPath}：运行时会动态 AddComponent 的引擎类型可能被剥离（09 关键坑 27）。");
         }

         // 6) 出战编目 AB 标记
         var roleBundle = CheckRoleBundle();
         if (roleBundle == null)
         {
            Bad($"编目资产未标 AB：请执行「Thrud/Demo/Characters/1. 标记编目为 AB」。" +
                "不标 → 角色/武器资源不进包 → 真机进关无角色、退回主场景（09 关键坑 28）。");
         }

         // 7) AB 产物
         var abDir = AssetBundleOutputDir();
         if (!Directory.Exists(abDir))
         {
            if (abRequired)
            {
               Bad($"AB 产物目录不存在：{Relative(abDir)}。先执行「2. Build AssetBundles (Android)」。");
            }
            else
            {
               Warn($"AB 产物目录尚不存在：{Relative(abDir)}（本次会重新构建）。");
            }
         }
         else
         {
            var summary = DescribeBuiltBundles(abDir, fatalLog: abRequired);
            if (!summary.Ok && abRequired) Bad(summary.Message);
            else if (!summary.Ok) Warn(summary.Message);
            else Ok(summary.Message);

            if (roleBundle != null)
            {
               var roleFile = FindBundleFile(abDir, roleBundle);
               if (roleFile == null)
               {
                  var msg = $"编目 AB 没有产物文件（bundle={roleBundle}）→ 真机取不到角色资源，先执行「2. Build AssetBundles (Android)」。";
                  if (abRequired) Bad(msg); else Warn(msg);
               }
               else
               {
                  var sizeMb = new FileInfo(roleFile).Length / 1024f / 1024f;
                  Ok($"编目 AB 产物：{Path.GetFileName(roleFile)}（{sizeMb:F1} MB）。");

                  var stale = CheckStale(roleFile);
                  if (stale != null) Warn(stale);
               }
            }
         }

         // 8) 孤儿产物（在盘上但不在清单里 → 仍会进 APK，白白撑包体）
         var orphans = FindOrphanBundleFiles(abDir);
         if (orphans.Count > 0)
         {
            var orphanMb = orphans.Sum(f => new FileInfo(f).Length) / 1024f / 1024f;
            Warn($"AB 产物目录有 {orphans.Count} 个未被清单引用的文件（合计 {orphanMb:F1} MB）：" +
                 $"{string.Join("、", orphans.Select(Path.GetFileName))}。" +
                 "StreamingAssets 整体进 APK，这些是纯浪费的包体，建议执行「7. 清理 AB 产物目录中的孤儿文件」。");
         }

         // 9) 同版本同模式包已存在 → 会被覆盖
         var version = PlayerSettings.bundleVersion ?? "0.0.0";
         var verCode = PlayerSettings.Android.bundleVersionCode;
         var releaseApk = Path.Combine(ApkFolderAbsolute, $"ThrudDemo_{version}_{verCode}_release.apk");
         if (File.Exists(releaseApk))
         {
            Warn($"已存在同名 Release 包（{Path.GetFileName(releaseApk)}），本次构建会覆盖它；" +
                 "要留档请先执行「6. 版本号 +1」。");
         }

         Record(fatal ? "—— 门禁结果：不通过（见上面 ❌）——" : "—— 门禁结果：通过 ——");
         Debug.Log("[T2] 构建前置检查：\n  " + string.Join("\n  ", sGateLog));
         return !fatal;
      }

      /// <summary>返回实际用于运行的编目 bundle 名（没标/标错返回 null）。</summary>
      private static string CheckRoleBundle()
      {
         var importer = AssetImporter.GetAtPath(CatalogAssetPath);
         if (importer == null)
         {
            Record($"⚠️ 未找到编目资产 {CatalogAssetPath}（路径被改名/移动？）。");
            return null;
         }

         var marked = importer.assetBundleName;
         if (string.IsNullOrEmpty(marked)) return null;

         if (marked == RoleBundleName)
         {
            Record($"✅ 编目 AB 标记正确：{CatalogAssetPath} → 「{RoleBundleName}」。");
            return marked;
         }

         if (marked == RoleBundleFallbackName)
         {
            Record($"⚠️ 编目 AB 用的是 Inspector 默认名「{marked}」（代码常量是「{RoleBundleName}」）。" +
                   "运行时靠候选名兜底仍可用，但建议执行「Thrud/Demo/Characters/1」统一到规范名（09 关键坑 30）。");
            return marked;
         }

         Record($"❌ 编目 AB 标记为「{marked}」，既不是「{RoleBundleName}」也不是默认名「{RoleBundleFallbackName}」：" +
                "真机清单里找不到 → 编目解析失败。请执行「Thrud/Demo/Characters/1」重标。");
         return null;
      }

      private struct BundleSummary
      {
         public bool Ok;
         public string Message;
      }

      /// <summary>回读 AB 产物：每个已标记的 bundle 都必须能在盘上找到文件，并统计总体积。</summary>
      private static BundleSummary DescribeBuiltBundles(string abDir, bool fatalLog)
      {
         var names = AssetDatabase.GetAllAssetBundleNames();
         var missing = new List<string>();
         var totalBytes = 0L;
         var found = 0;

         for (var i = 0; i < names.Length; i++)
         {
            var file = FindBundleFile(abDir, names[i]);
            if (file == null)
            {
               missing.Add(names[i]);
               continue;
            }

            found++;
            totalBytes += new FileInfo(file).Length;
         }

         var config = Path.Combine(abDir, "asset_bundle_config.bin");
         var configOk = File.Exists(config);
         if (configOk) totalBytes += new FileInfo(config).Length;

         if (missing.Count > 0 || !configOk)
         {
            var parts = new List<string>();
            if (missing.Count > 0) parts.Add($"缺 {missing.Count} 个 bundle 产物（{string.Join("、", missing)}）");
            if (!configOk) parts.Add("缺 asset_bundle_config.bin（ResKit 清单，真机初始化要用）");
            return new BundleSummary
            {
               Ok = false,
               Message = $"AB 产物不完整：{string.Join("；", parts)}。重新执行「2. Build AssetBundles (Android)」" +
                         "并确认 Console 无打包报错。",
            };
         }

         return new BundleSummary
         {
            Ok = true,
            Message = $"AB 产物齐备：{found} 个 bundle + 清单，合计 {totalBytes / 1024f / 1024f:F1} MB（{Relative(abDir)}）" +
                      (fatalLog ? "" : "；注意：改过任何被标 AB 的资产后都要重打，否则包内是旧资源。"),
         };
      }

      /// <summary>AB 产物是否比其源资产旧（粗略判定：只看编目资产的直接依赖闭包）。</summary>
      private static string CheckStale(string bundleFile)
      {
         try
         {
            var latest = DateTime.MinValue;
            var deps = AssetDatabase.GetDependencies(CatalogAssetPath, true);
            for (var i = 0; i < deps.Length; i++)
            {
               var full = ToAbsolute(deps[i]);
               if (!File.Exists(full)) continue;
               var t = File.GetLastWriteTimeUtc(full);
               if (t > latest) latest = t;
            }

            var bundleTime = File.GetLastWriteTimeUtc(bundleFile);
            if (latest > bundleTime)
            {
               return $"编目依赖里有比 AB 产物更新的资产（资产 {latest:MM-dd HH:mm} > AB {bundleTime:MM-dd HH:mm}）：" +
                      "AB 可能已过期，建议重打（菜单 2）。";
            }
         }
         catch (Exception e)
         {
            return $"过期检查跳过（{e.GetType().Name}）。";
         }

         return null;
      }

      // ---------------- 构建步骤 ----------------

      private static bool EnsureAndroidPlatform(bool allowSwitch)
      {
         if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
         {
            Debug.Log("[T2] 当前已是 Android 目标。");
            return true;
         }

         if (!allowSwitch)
         {
            Debug.LogError($"[T2] 当前构建目标是 {EditorUserBuildSettings.activeBuildTarget}，不是 Android。" +
                           "请先执行「1. Switch Platform to Android」（切平台会触发域重载，必须单独一步），" +
                           "等编译完成后再执行本菜单。已中止，避免打出错误平台的 AB。");
            return false;
         }

         Debug.Log("[T2] 正在切换构建目标到 Android …（会触发脚本重编译/域重载）");
         var ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
         if (!ok)
         {
            Debug.LogError("[T2] 切换到 Android 目标失败：请检查是否安装了 Android 模块（Unity Hub → 安装 → 添加模块）与许可状态。");
            return false;
         }

         Debug.Log("[T2] 已切换到 Android 目标。");
         return true;
      }

      /// <summary>幂等应用 Android 关键设置（包名 / IL2CPP + ARM64 / 横屏 / MinSdk / debug 签名）。</summary>
      private static void EnsurePlayerSettings()
      {
         PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
         PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
         PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
         PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeRight;
         PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
         // 内测外放用 debug keystore（13 §3.3「debug 签名 apk 直接外放」）；正式签名时改回 true 并配 Build/keystore。
         PlayerSettings.Android.useCustomKeystore = false;

         Debug.Log($"[T2] Android 设置就绪：包名 {BundleId}、IL2CPP + ARM64、横屏、MinSdk 28、debug 签名；" +
                   $"版本 {PlayerSettings.bundleVersion} / Code {PlayerSettings.Android.bundleVersionCode}。");
      }

      /// <summary>打 AB 并校验产物；校验不通过返回 false（调用方不要继续出包）。</summary>
      private static bool BuildResKitAssetBundles()
      {
         Debug.Log($"[T2] 开始 ResKit AssetBundle 构建（目标 {EditorUserBuildSettings.activeBuildTarget}）…");
         ResKitEditorAPI.BuildAssetBundles(); // 内部按 activeBuildTarget 打；含 QAssets.cs 重新生成
         AssetDatabase.Refresh();

         var summary = DescribeBuiltBundles(AssetBundleOutputDir(), fatalLog: true);
         if (!summary.Ok)
         {
            Debug.LogError("[T2] " + summary.Message);
            return false;
         }

         Debug.Log("[T2] " + summary.Message);
         return true;
      }

      /// <summary>
      /// 出包主流程；<paramref name="thin"/> 时把 AB 产物临时移出 Assets（不进包），出完自动还原。
      /// 返回是否成功——菜单与命令行都靠它判成败（命令行据此给退出码）。
      /// </summary>
      private static bool BuildPlayerApk(bool development, bool thin)
      {
         if (!thin)
         {
            return BuildPlayerApkCore(development, thin: false);
         }

         // 移出整个 AssetBundles（含别的平台目录）：只要还在 Assets 里就会被塞进包
         var abDir = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "AssetBundles"));
         var holdDir = Path.Combine(ThinHoldRoot, "AssetBundles");
         var moved = false;

         try
         {
            WriteBuildProfile(packageHasBundles: false);
            MoveAssetDirectory(abDir, holdDir);
            moved = true;
            AssetDatabase.Refresh();

            Debug.LogWarning("[T2] 薄包模式：AB 产物已临时移出 Assets，本次 APK 不含任何资源。" +
                             "装包后首次启动必须能连上 OTA 服务器，否则进不了主菜单。");

            return BuildPlayerApkCore(development, thin: true);
         }
         finally
         {
            if (moved)
            {
               MoveAssetDirectory(holdDir, abDir);
            }

            WriteBuildProfile(packageHasBundles: true);
            AssetDatabase.Refresh();
            Debug.Log("[T2] 出包收尾：AB 产物与构建画像已还原（编辑器回到胖包状态）。");
         }
      }

      private static bool BuildPlayerApkCore(bool development, bool thin)
      {
         var scenes = EnabledScenes();
         if (scenes.Length == 0)
         {
            Debug.LogError("[T2] Build Settings 无启用的场景，无法出包。");
            return false;
         }

         Directory.CreateDirectory(ApkFolderAbsolute);

         var version = PlayerSettings.bundleVersion ?? "0.0.0";
         var verCode = PlayerSettings.Android.bundleVersionCode;
         var mode = development ? "dev" : "release";
         if (thin)
         {
            mode += "-thin";
         }

         var apkPath = Path.Combine(ApkFolderAbsolute, $"ThrudDemo_{version}_{verCode}_{mode}.apk").Replace('\\', '/');

         var options = new BuildPlayerOptions
         {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = development ? BuildOptions.Development : BuildOptions.None,
         };

         Debug.Log($"[T2] 开始 BuildPlayer → {apkPath}（Development={development}，Thin={thin}，" +
                   $"场景 {scenes.Length} 个，首次 IL2CPP 编译约 5–15 分钟）…");
         var report = BuildPipeline.BuildPlayer(options);

         if (report.summary.result != BuildResult.Succeeded)
         {
            Debug.LogError($"[T2] APK 构建失败：{report.summary.result}（{report.summary.totalErrors} 个错误）。详见 Console。");
            foreach (var step in report.steps)
            {
               foreach (var msg in step.messages)
               {
                  if (msg.type == LogType.Error || msg.type == LogType.Exception)
                  {
                     Debug.LogError($"[T2][{step.name}] {msg.content}");
                  }
               }
            }

            return false;
         }

         var sizeBytes = File.Exists(apkPath) ? new FileInfo(apkPath).Length : 0L;
         var sizeMb = sizeBytes / 1024f / 1024f;
         Debug.Log($"[T2] ✅ APK 构建成功：{apkPath}（{sizeMb:F1} MB，用时 {(int)report.summary.totalTime.TotalSeconds}s，" +
                   $"报告体积 {report.summary.totalSize / 1024f / 1024f:F1} MB）。");

         if (sizeBytes > ApkSizeWarnBytes)
         {
            Debug.LogWarning($"[T2] ⚠️ 包体 {sizeMb:F1} MB 已超过 200MB 目标（13 §3.3）：" +
                             "外放前考虑按 14 文档 P3 做 AB 划分/去掉重复依赖闭包。");
         }
         else
         {
            Debug.Log($"[T2] 包体 {sizeMb:F1} MB，在 200MB 目标内（余量 {ApkSizeWarnBytes / 1024f / 1024f - sizeMb:F1} MB）。");
         }

         WriteBuildReport(apkPath, sizeMb, mode, scenes);

         // 批处理下没有可交互的文件管理器，别在这里浪费一次异常
         if (!Application.isBatchMode)
         {
            EditorUtility.RevealInFinder(apkPath);
         }

         Debug.Log("[T2] 下一步：按 Docs/01_里程碑与执行/15…《真机验收清单 v0》装包逐项验收（A 安装启动 → H 包体分发）。");
         return true;
      }

      /// <summary>把本次构建的门禁结论 + 包体信息落一份文本报告（Build/ 不入库，仅供留档/回报）。</summary>
      private static void WriteBuildReport(string apkPath, float apkSizeMb, string mode, string[] scenes)
      {
         try
         {
            var abDir = AssetBundleOutputDir();
            var abFiles = Directory.Exists(abDir)
               ? Directory.GetFiles(abDir).Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToArray()
               : new string[0];
            var abTotalMb = abFiles.Sum(f => new FileInfo(f).Length) / 1024f / 1024f;

            var sb = new StringBuilder();
            sb.AppendLine($"Thrud Demo 构建报告（{mode}）");
            sb.AppendLine($"时间        : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Unity       : {Application.unityVersion}");
            sb.AppendLine($"版本        : {PlayerSettings.bundleVersion} / Code {PlayerSettings.Android.bundleVersionCode}");
            sb.AppendLine($"包名        : {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}");
            sb.AppendLine($"目标        : Android / {PlayerSettings.Android.targetArchitectures} / IL2CPP / MinSdk {(int)PlayerSettings.Android.minSdkVersion}");
            sb.AppendLine($"签名        : {(PlayerSettings.Android.useCustomKeystore ? "自定义 keystore" : "debug keystore（内测）")}");
            sb.AppendLine($"场景        : {string.Join(" → ", scenes)}");
            sb.AppendLine($"AB 产物     : {abFiles.Length} 个文件，合计 {abTotalMb:F1} MB（{Relative(abDir)}）");
            sb.AppendLine($"APK         : {apkPath}");
            sb.AppendLine($"APK 体积    : {apkSizeMb:F1} MB（目标 < 200MB）");
            sb.AppendLine();
            sb.AppendLine("本次门禁结论：");
            foreach (var line in sGateLog) sb.AppendLine("  " + line);
            sb.AppendLine();
            sb.AppendLine("验收清单      : Docs/01_里程碑与执行/15_Demo打包与外放_构建说明与真机验收清单.md");

            var reportPath = Path.Combine(ApkFolderAbsolute,
               $"build_report_{PlayerSettings.bundleVersion}_{PlayerSettings.Android.bundleVersionCode}_{mode}.txt");
            File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[T2] 构建报告已写出：{reportPath.Replace('\\', '/')}");
         }
         catch (Exception e)
         {
            Debug.LogWarning($"[T2] 写构建报告失败（不影响出包）：{e.Message}");
         }
      }

      // ---------------- 工具 ----------------

      private static string[] EnabledScenes()
      {
         return EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();
      }

      /// <summary>ResKit 的 AB 产物目录（与 BuildScript 的 GetPlatformName 对齐：仅 Android 用到本工具）。</summary>
      private static string AssetBundleOutputDir()
      {
         return Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "AssetBundles", "Android"));
      }

      /// <summary>在 AB 产物目录里找 bundle 文件（兼容 ResKit 的 AppendHash 后缀）。</summary>
      private static string FindBundleFile(string abDir, string bundleName)
      {
         if (!Directory.Exists(abDir)) return null;

         var files = Directory.GetFiles(abDir);
         for (var i = 0; i < files.Length; i++)
         {
            var name = Path.GetFileName(files[i]);
            if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
            {
               continue;
            }

            if (name == bundleName ||
                name.StartsWith(bundleName + "_", StringComparison.Ordinal) ||
                name.StartsWith(bundleName + ".", StringComparison.Ordinal))
            {
               return files[i];
            }
         }

         return null;
      }

      /// <summary>
      /// 找出"在盘上但不在 AB 清单里"的产物文件。
      /// 典型来源：bundle 改过名（如 Inspector 默认名 → 规范名）后旧产物残留。
      /// 这些文件 ResKit 不会用，但 **StreamingAssets 是整体进 APK 的** → 白白撑大包体（实测一例 44.7MB）。
      /// 白名单：`.meta` / `.manifest` / `asset_bundle_config.bin` / 平台自身的同名 bundle。
      /// </summary>
      private static List<string> FindOrphanBundleFiles(string abDir)
      {
         var result = new List<string>();
         if (!Directory.Exists(abDir)) return result;

         var marked = new HashSet<string>(AssetDatabase.GetAllAssetBundleNames());
         var platformBundle = EditorUserBuildSettings.activeBuildTarget.ToString();
         var files = Directory.GetFiles(abDir);

         for (var i = 0; i < files.Length; i++)
         {
            var name = Path.GetFileName(files[i]);
            if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
            {
               continue;
            }

            if (name == "asset_bundle_config.bin" || name == platformBundle) continue;
            if (marked.Contains(name)) continue;

            // ResKit 可开 AppendHash（<bundle>_<hash>），按最后一个下划线前缀再判一次
            var cut = name.LastIndexOf('_');
            if (cut > 0 && marked.Contains(name.Substring(0, cut))) continue;

            result.Add(files[i]);
         }

         return result;
      }

      private static string ToAbsolute(string assetOrFullPath)
      {
         if (Path.IsPathRooted(assetOrFullPath)) return assetOrFullPath;
         return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetOrFullPath));
      }

      private static string Relative(string absolutePath)
      {
         var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
         return absolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            ? absolutePath.Substring(projectRoot.Length).TrimStart('\\', '/').Replace('\\', '/')
            : absolutePath.Replace('\\', '/');
      }

      /// <summary>"alpha.0.47" → "alpha.0.48"；末尾没有数字则补 ".1"。</summary>
      private static string IncrementTrailingNumber(string version)
      {
         var i = version.Length;
         while (i > 0 && char.IsDigit(version[i - 1])) i--;
         if (i == version.Length) return version + ".1";

         var prefix = version.Substring(0, i);
         var digits = version.Substring(i);
         if (!int.TryParse(digits, out var n)) return version + ".1";
         return prefix + (n + 1);
      }

      private static void Record(string line)
      {
         sGateLog.Add(line);
      }
   }
}
