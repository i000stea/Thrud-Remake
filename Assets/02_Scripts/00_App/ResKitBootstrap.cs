using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace TeaGame.App
{
   /// <summary>
   /// ResKit 初始化收口（P0，2026-09-10）。
   ///
   /// 背景：<c>ResMgr.Init/InitAsync</c> 都是「置位即返回」——<c>mResMgrInited</c> 在真正读到
   /// AB 清单之前就已置 true，且读清单失败只落一条日志。于是只要有一次初始化没读到清单
   /// （平台目录缺失 / 清单损坏 / 初始化时机被抢先），后续所有 ResKit 调用都只会报下游的
   /// <c>Failed to Create Res. Not Find By ResSearchKeys</c> 或 <c>Failed to Load Res</c>，
   /// 看起来像「资源没进包」，实际是「清单没加载」。
   /// <see cref="Validate"/> 把这件事变成启动时一条明确、可执行的错误。
   ///
   /// 使用约定：真机唯一初始化点仍是 0ConfigScene 的 <c>AppBootstrapper</c>（异步，Android 必需）；
   /// 其他场景的兜底调用 <see cref="EnsureInitialized"/>（幂等 + 校验），不要再直接写 <c>ResKit.Init()</c>。
   /// </summary>
   public static class ResKitBootstrap
   {
      /// <summary>参与启动校验的 UI 面板资源（bundle / asset，与 QAssetBundle 生成常量一致）。</summary>
      private static readonly PanelRes[] ExpectedPanels =
      {
         new PanelRes(QAssetBundle.Main_menupanel_prefab.BundleName,
            QAssetBundle.Main_menupanel_prefab.Main_MenuPanel),
         new PanelRes(QAssetBundle.Levelselectpanel_prefab.BundleName,
            QAssetBundle.Levelselectpanel_prefab.LevelSelectPanel),
         new PanelRes(QAssetBundle.Main_formationpanel_prefab.BundleName,
            QAssetBundle.Main_formationpanel_prefab.Main_FormationPanel),
         new PanelRes(QAssetBundle.Battle_levelresultpanel_prefab.BundleName,
            QAssetBundle.Battle_levelresultpanel_prefab.Battle_LevelResultPanel),
         new PanelRes(QAssetBundle.Battle_pausemenupanel_prefab.BundleName,
            QAssetBundle.Battle_pausemenupanel_prefab.Battle_PauseMenuPanel),
         new PanelRes(QAssetBundle.Battle_touchinputpanel_prefab.BundleName,
            QAssetBundle.Battle_touchinputpanel_prefab.Battle_TouchInputPanel),
         new PanelRes(QAssetBundle.Loadinggroup_prefab.BundleName,
            QAssetBundle.Loadinggroup_prefab.LoadingGroup),
      };

      private static bool sValidationDone;
      private static bool sValidationResult;

      /// <summary>
      /// 兜底初始化 + 校验（幂等）。
      /// 已经初始化过时只做校验；从未初始化时同步补一次并明确警告调用链异常
      /// （真机正常路径必须由 AppBootstrapper 走异步初始化）。
      /// </summary>
      public static bool EnsureInitialized()
      {
         if (!ResMgr.ResMgrInited)
         {
            if (Application.isEditor)
            {
               Debug.Log("[ResKit] 首次初始化（编辑器：SimulationMode 下正常由 ResKit.CheckAutoInit 自动完成，此处补一次同步 Init）。");
            }
            else
            {
               Debug.LogError("[ResKit] 进入场景时 ResKit 仍未初始化：正常路径应由 0ConfigScene 的 AppBootstrapper 调用 " +
                              "ResKit.InitAsync()（0ConfigScene 必须常驻 Build Settings[0]）。已回退同步 Init——" +
                              "Android 上该路径会阻塞主线程扫描 APK 读清单，请核对启动链路。");
            }

            ResKit.Init();
         }

         return Validate();
      }

      /// <summary>
      /// 校验 AB 清单是否真的可用：逐个确认 UI 面板资源能从清单解析（= 面板加载的前提）。
      /// 只查清单，不加载任何 bundle；结果缓存，只汇报一次。
      /// </summary>
      public static bool Validate()
      {
         if (sValidationDone)
         {
            return sValidationResult;
         }

         sValidationDone = true;
         sValidationResult = false;

         if (!ResMgr.ResMgrInited)
         {
            Debug.LogError("[ResKit] 初始化校验失败：ResKit 尚未初始化（InitAsync/Init 未执行）——" +
                           "所有经 ResKit 的面板/资源加载都会失败。");
            return false;
         }

         List<string> missing = null;
         for (var i = 0; i < ExpectedPanels.Length; i++)
         {
            var expected = ExpectedPanels[i];
            if (TryResolve(expected.Bundle, expected.Asset))
            {
               continue;
            }

            if (missing == null)
            {
               missing = new List<string>();
            }

            missing.Add($"{expected.Bundle}/{expected.Asset}");
         }

         var mode = Application.isEditor
            ? $"编辑器（SimulationMode={AssetBundlePathHelper.SimulationMode}）"
            : "真机 AB";
         var platform = AssetBundlePathHelper.GetPlatformName();

         // 角色/武器编目（battle 装配的前置数据）：单独校验一次。
         // 2026-09-10 真机教训——标记名与代码常量不一致时，只有"打到真机进战斗"才暴露；
         // 放在启动校验里能一眼看出来（UI 面板能开 ≠ 角色资源能取到）。
         var roleBundle = BattleAssetService.ResolveAvailableBundle();
         if (roleBundle == null)
         {
            Debug.LogError($"[ResKit] 角色/武器编目校验失败：候选 AB" +
                           $"（{string.Join("、", BattleAssetService.RoleDataBundleCandidates)}）" +
                           $"都不含资产 {BattleAssetService.CatalogAssetName}——真机进战斗会因编目缺失而退回主场景。\n" +
                           "  处理：编辑器执行 Thrud/Demo/Characters/1（标记编目为 AB）+ 2（构建 AB）后重新出包。");
         }
         else if (roleBundle != BattleAssetService.RoleDataBundleName)
         {
            Debug.LogWarning($"[ResKit] 角色/武器编目 AB 命中「{roleBundle}」，但规范名是" +
                             $"「{BattleAssetService.RoleDataBundleName}」（Inspector 默认名？）。" +
                             "本次可用；建议用 Thrud/Demo/Characters/1 统一命名后重打 AB。");
         }

         if (missing == null)
         {
            sValidationResult = true;
            Debug.Log($"[ResKit] 初始化校验通过：{mode}，平台目录 {platform}，" +
                      $"{ExpectedPanels.Length} 个 UI 面板资源均可从 AB 清单解析" +
                      $"，角色/武器编目 bundle = {roleBundle}。");
            return true;
         }

         Debug.LogError($"[ResKit] 初始化校验失败：{mode}，平台目录 {platform}，" +
                        $"{missing.Count} 个 UI 面板资源不在 AB 清单中 → {string.Join("、", missing)}。\n" +
                        "排查顺序：① 面板 prefab 的 assetBundleName 是否已标记（Assets/04_Prefabs/Panel/**）；" +
                        "② 是否已执行 Thrud/Demo/Android/2. Build AssetBundles（生成 " +
                        "StreamingAssets/AssetBundles/<平台>/asset_bundle_config.bin）；" +
                        "③ 关闭 ResKit 窗口的 SimulationMode 时，对应平台目录必须已构建" +
                        "（本工程当前只构建了 Android 目录，在 Windows 编辑器直跑会解析失败）。");
         return false;
      }

      /// <summary>
      /// 资源能否从 AB 清单解析（只查清单：不加载 bundle、不产生引用计数、不抛异常）。
      /// <paramref name="assetType"/> 省略时按 GameObject 判定；类型只用于收窄匹配，
      /// 收窄不中时会回退为"名字 + bundle 命中即可"，因此仅做存在性判断是可靠的。
      /// </summary>
      public static bool TryResolve(string bundleName, string assetName, System.Type assetType = null)
      {
         if (!ResMgr.ResMgrInited)
         {
            return false;
         }

         var keys = ResSearchKeys.Allocate(assetName, bundleName, assetType != null ? assetType : typeof(GameObject));
         var data = AssetBundleSettings.AssetBundleConfigFile.GetAssetData(keys);
         keys.Recycle2Cache();

         return data != null;
      }

      /// <summary>校验用条目（bundle 名 + prefab 资源名）。</summary>
      private readonly struct PanelRes
      {
         public readonly string Bundle;
         public readonly string Asset;

         public PanelRes(string bundle, string asset)
         {
            Bundle = bundle;
            Asset = asset;
         }
      }
   }
}
