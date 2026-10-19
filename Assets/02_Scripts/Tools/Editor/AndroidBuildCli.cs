using UnityEditor;
using UnityEngine;

namespace TeaGame.Tools.Editor
{
   /// <summary>
   /// 命令行（Unity batchmode）出包入口。**后续标准打包路径，不依赖编辑器里点菜单。**
   ///
   /// 用法（在仓库根执行；<b>必须先关闭正在使用本工程的 Unity 编辑器</b>，否则工程被锁）：
   /// <code>
   /// "&lt;Unity.exe&gt;" -batchmode -quit -projectPath "&lt;工程根&gt;" -buildTarget Android \
   ///     -executeMethod TeaGame.Tools.Editor.AndroidBuildCli.BuildAllThin \
   ///     -logFile "Build/Android/build-thin.log"
   /// </code>
   /// 或用现成脚本：`build-thin.cmd`（默认跑 BuildAllThin）。
   ///
   /// **退出码**：0 = 成功；1 = 失败（门禁/AB/出包任一步不过）。CI 直接看退出码即可。
   ///
   /// 说明为什么每个入口是独立方法而不是带参数：`-executeMethod` 只接受无参静态方法；
   /// 需要不同变体就多开一个方法，比解析自定义命令行参数稳。
   /// </summary>
   public static class AndroidBuildCli
   {
      /// <summary>
      /// 标准出包：门禁 + 重打 AB + **瘦体 APK**（包内不含 AB）+ 发布 OTA 内容。
      /// 这是「后续打包都用瘦体」的默认入口。
      /// </summary>
      public static void BuildAllThin()
      {
         var ok = AndroidDemoBuilder.CliBuildAllThin(development: false);

         // AB 已重打，OTA 内容必须跟着更新，否则真机下到的还是旧集合
         if (ok)
         {
            ok = OtaContentPublisher.Publish();
         }

         Finish(ok, "BuildAllThin");
      }

      /// <summary>瘦体 Development 包（联调用，含调试符号）。</summary>
      public static void BuildAllThinDev()
      {
         Finish(AndroidDemoBuilder.CliBuildAllThin(development: true), "BuildAllThinDev");
      }

      /// <summary>只出瘦体 APK（不重打 AB）。适合「AB 没变、只改了 AOT 代码」的迭代。</summary>
      public static void BuildThinApkOnly()
      {
         Finish(AndroidDemoBuilder.CliBuildApk(development: false, thin: true), "BuildThinApkOnly");
      }

      /// <summary>胖包 APK（包内带基线 AB）。仅用于对照与离线演示，不是默认路径。</summary>
      public static void BuildFatApk()
      {
         Finish(AndroidDemoBuilder.CliBuildApk(development: false, thin: false), "BuildFatApk");
      }

      /// <summary>只重打 ResKit AssetBundle（不动 APK）。</summary>
      public static void BuildAssetBundles()
      {
         Finish(AndroidDemoBuilder.CliBuildAssetBundles(), "BuildAssetBundles");
      }

      /// <summary>只把当前 AB + 热更产物发布成 OTA 内容（重写局域网服务目录与 manifest.json）。</summary>
      public static void PublishOtaContent()
      {
         Finish(OtaContentPublisher.Publish(), "PublishOtaContent");
      }

      /// <summary>
      /// 收尾：批处理下用退出码表达成败，交互模式下只打日志。
      /// </summary>
      private static void Finish(bool ok, string what)
      {
         if (ok)
         {
            Debug.Log($"[CLI] ✅ {what} 完成。");
         }
         else
         {
            Debug.LogError($"[CLI] ❌ {what} 失败——见上文第一条 Error。");
         }

         if (Application.isBatchMode)
         {
            EditorApplication.Exit(ok ? 0 : 1);
         }
      }
   }
}
