using System.IO;
using System.Linq;
using QFramework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TeaGame.Tools.Editor
{
   /// <summary>
   /// T2 Android 一键构建工具（13 Demo §3.3）。
   /// 分工步菜单（便于真机联调时只重打其中一段）+ 一键组合：
   ///   1. Switch Platform to Android   —— 切 Android 目标（IL2CPP 编译在切换/出包时触发）
   ///   2. Build AssetBundles (Android) —— ResKit 按当前活动目标打 AB（写入 StreamingAssets/AssetBundles/Android）
   ///   3. Build APK (Release)          —— 非 Development，debug keystore 直接外放（正式签名后补）
   ///   4. Build APK (Development)      —— 联调用（挂脚本调试/看日志）
   ///   5. Build All (One-Click)        —— 2 + 3（已切 Android 时的日常出包）
   ///
   /// 每次构建前会幂等应用 Android 关键设置：包名 com.i000stea.thrudremark、
   /// IL2CPP + ARM64、横屏、MinSdk 28（覆盖 13 §T0 的设定核对）。
   /// 场景列表取 Build Settings 中 enabled 的条目（当前 = 0Config/1Login/2Main/BattleGraybox）。
   /// 产物：Build/Android/ThrudDemo_&lt;版本&gt;_&lt;verCode&gt;.apk（Build/ 已被 .gitignore 忽略，不入库）。
   /// </summary>
   public static class AndroidDemoBuilder
   {
      private const string MenuRoot = "Thrud/Demo/Android/";
      private const string BundleId = "com.i000stea.thrudremark";

      private const string ApkFolderRelative = "Build/Android";
      private static string ApkFolderAbsolute => Path.Combine(Application.dataPath, "../", ApkFolderRelative);

      // ---------------- 菜单 ----------------

      [MenuItem(MenuRoot + "1. Switch Platform to Android")]
      public static void SwitchToAndroid()
      {
         EnsureAndroidPlatform();
      }

      [MenuItem(MenuRoot + "2. Build AssetBundles (Android)")]
      public static void BuildAssetBundlesMenu()
      {
         EnsureAndroidPlatform();
         EnsurePlayerSettings();
         BuildResKitAssetBundles();
      }

      [MenuItem(MenuRoot + "3. Build APK (Release · debug keystore)")]
      public static void BuildApkReleaseMenu()
      {
         EnsureAndroidPlatform();
         EnsurePlayerSettings();
         BuildPlayerApk(development: false);
      }

      [MenuItem(MenuRoot + "4. Build APK (Development · 联调)")]
      public static void BuildApkDevMenu()
      {
         EnsureAndroidPlatform();
         EnsurePlayerSettings();
         BuildPlayerApk(development: true);
      }

      [MenuItem(MenuRoot + "5. Build All (One-Click · AB + APK Release)")]
      public static void BuildAllMenu()
      {
         EnsureAndroidPlatform();
         EnsurePlayerSettings();
         BuildResKitAssetBundles();
         BuildPlayerApk(development: false);
      }

      // ---------------- 分步实现 ----------------

      private static void EnsureAndroidPlatform()
      {
         if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return;

         Debug.Log("[T2] 正在切换构建目标到 Android …");
         var ok = EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Android, BuildTarget.Android);
         if (!ok)
         {
            Debug.LogError("[T2] 切换到 Android 目标失败（请检查 Android 模块/许可）。");
            return;
         }

         Debug.Log("[T2] 已切换到 Android 目标。");
      }

      /// <summary>幂等应用 Android 关键设置（包名/IL2CPP+ARM64/横屏/MinSdk）。</summary>
      private static void EnsurePlayerSettings()
      {
         PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
         PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
         PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
         PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeRight;
         PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;

         Debug.Log($"[T2] Android 设置就绪：包名 {BundleId}、IL2CPP + ARM64、横屏、MinSdk 28。" +
                   $"版本 {PlayerSettings.bundleVersion} / Code {PlayerSettings.Android.bundleVersionCode}");
      }

      private static void BuildResKitAssetBundles()
      {
         Debug.Log("[T2] 开始 ResKit AssetBundle 构建（Android 目标）…");
         ResKitEditorAPI.BuildAssetBundles();
         AssetDatabase.Refresh();
         Debug.Log("[T2] AssetBundle 构建完成（StreamingAssets/AssetBundles/Android）。");
      }

      private static void BuildPlayerApk(bool development)
      {
         var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();
         if (scenes.Length == 0)
         {
            Debug.LogError("[T2] Build Settings 无启用的场景，无法出包。");
            return;
         }

         Directory.CreateDirectory(ApkFolderAbsolute);

         var version = PlayerSettings.bundleVersion ?? "0.0.0";
         var verCode = PlayerSettings.Android.bundleVersionCode;
         var mode = development ? "dev" : "release";
         var apkPath = Path.Combine(
            ApkFolderAbsolute,
            $"ThrudDemo_{version}_{verCode}_{mode}.apk").Replace('\\', '/');

         var options = new BuildPlayerOptions
         {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = development ? BuildOptions.Development : BuildOptions.None,
         };

         Debug.Log($"[T2] 开始 BuildPlayer → {apkPath}（Development={development}，场景 {scenes.Length} 个）…");
         var report = BuildPipeline.BuildPlayer(options);

         if (report.summary.result == BuildResult.Succeeded)
         {
            Debug.Log($"[T2] ✅ APK 构建成功：{apkPath}（用时 {(int)report.summary.totalTime.TotalSeconds}s）");
            EditorUtility.RevealInFinder(apkPath);
         }
         else
         {
            Debug.LogError($"[T2] APK 构建失败：{report.summary.result}。详见 Console 报错。");
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
         }
      }
   }
}
