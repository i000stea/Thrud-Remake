using System.IO;
using QFramework;
using UnityEngine;

namespace TeaGame.App
{
   /// <summary>
   /// 把已验证的 OTA 缓存交给 ResKit（23 文档 §3 的最后一个职责）。
   ///
   /// **为什么不需要改 ResKit**：ResKit 的两条解析规则本来就是「persistent 优先、逐文件回退包内」：
   /// - `AssetBundleSettings.AssetBundleName2Url` → 先找 `persistentDataPath/AssetBundles/&lt;Platform&gt;/&lt;bundle&gt;`，
   ///   没有才回退 `StreamingAssets/AssetBundles/&lt;Platform&gt;/&lt;bundle&gt;`；
   /// - AB 清单（`asset_bundle_config.bin`）则由 `AssetBundleSettings.LoadAssetResFromStreamingAssetsPath`
   ///   统一决定读包内还是读 persistent。
   ///
   /// 因此这里有两种模式，按清单的 `abSetComplete` 显式选择，绝不混用：
   /// 1. **逐文件覆盖（默认）**：清单只列个别 bundle。保持读包内基线清单，
   ///    OTA 下来的 bundle 因为落在 persistent 而被自动优先命中——
   ///    这正是「只替换 scene_0002_main，主菜单内容改变，其余 UI 面板照常」的机制。
   /// 2. **整套覆盖**：清单声明 `abSetComplete=true` 且确实带了 `asset_bundle_config.bin`，
   ///    才把开关切到 persistent。此时 persistent 必须自洽（所有 bundle 都在），否则整个 App 会缺资源，
   ///    所以这个开关只允许在「整套」语义下打开。
   /// </summary>
   public static class ResKitContentLocator
   {
      /// <summary>AB 清单文件名（与 ResKit 的 ResDatas.FileName 一致）。</summary>
      public const string AssetBundleConfigFileName = "asset_bundle_config.bin";

      /// <summary>
      /// 在 <c>ResKit.InitAsync()</c> **之前**调用，决定 ResKit 读哪套 AB 清单。
      /// </summary>
      /// <returns>是否切到了 persistent 全量模式。</returns>
      public static bool Apply(HotUpdateCacheState state, out string reason)
      {
         reason = null;

         if (state == null || string.IsNullOrEmpty(state.contentVersion))
         {
            reason = "没有 OTA 内容记录，使用包内基线";
            RevertToBaseline();
            return false;
         }

         if (!state.abSetComplete)
         {
            // 逐文件覆盖：包内基线清单 + persistent 里的同名 bundle 自动优先
            reason = $"逐文件覆盖模式（contentVersion={state.contentVersion}），AB 清单仍读包内基线";
            RevertToBaseline();
            return false;
         }

         var persistentConfig = Path.Combine(HotUpdateCacheService.AssetBundleDir, AssetBundleConfigFileName);
         if (!File.Exists(persistentConfig))
         {
            reason = $"清单声明 abSetComplete=true，但 OTA 缓存里没有 {AssetBundleConfigFileName}，回退包内基线";
            RevertToBaseline();
            return false;
         }

         // 光有清单不够：整套模式下 ResKit 只读 persistent，任何一个 bundle 缺失都会在加载时炸。
         // 这里按状态里登记的清单逐个点检，不自洽就整体退回包内基线——
         // 这条保证了「OTA 没下全 / 被手工删过」时，游戏仍能用包内基线正常进主场景。
         if (!AllBundlesPresent(state, out var missing))
         {
            reason = $"持久化 AB 不完整（缺 {missing}），回退包内基线以免加载失败";
            RevertToBaseline();
            return false;
         }

         // 整套覆盖：切到 persistent
         AssetBundleSettings.LoadAssetResFromStreamingAssetsPath = false;
         reason = $"整套覆盖模式（contentVersion={state.contentVersion}），AB 清单与 bundle 均读 persistent";
         Debug.Log($"[HotUpdate] ResKit 内容定位：{reason}（{HotUpdateCacheService.AssetBundleDir}）");
         return true;
      }

      /// <summary>
      /// 回到包内基线（首包无网络、OTA 内容缺失、或逐文件覆盖模式下的默认态）。
      /// 逐文件覆盖依赖「persistent 优先」的回退规则，所以这里**只**动清单开关，不删 persistent 文件。
      /// </summary>
      public static void RevertToBaseline()
      {
         AssetBundleSettings.LoadAssetResFromStreamingAssetsPath = true;
         Debug.Log("[HotUpdate] ResKit 内容定位：AB 清单读包内基线（StreamingAssets）");
      }

      /// <summary>
      /// 点检状态里登记的每个 AB 是否都还在磁盘上。缺失返回 false 并给出第一个缺失的文件名。
      /// </summary>
      private static bool AllBundlesPresent(HotUpdateCacheState state, out string missing)
      {
         missing = null;
         if (state?.files == null) return true;

         foreach (var file in state.files)
         {
            if (file == null || string.IsNullOrEmpty(file.fileName)) continue;
            if (!string.Equals(file.kind, HotUpdateFileEntry.KindAssetBundle, System.StringComparison.OrdinalIgnoreCase))
            {
               continue;
            }

            var path = Path.Combine(HotUpdateCacheService.AssetBundleDir, file.fileName);
            if (File.Exists(path)) continue;

            missing = file.fileName;
            return false;
         }

         return true;
      }
   }
}
