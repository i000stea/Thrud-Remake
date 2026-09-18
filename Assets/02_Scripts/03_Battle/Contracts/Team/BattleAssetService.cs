using QFramework;
using TeaGame.App;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 角色/武器资源常驻服务（P0，2026-09-10）。
   ///
   /// **问题**：角色/武器用的 `RoleItem_Data` / `WeaponItem_Data`（含 `rolePreafab` / `wepPrefab` /
   /// 立绘）过去只靠"编队面板 prefab 序列化了 `BattleTeamCatalog`"这条**意外引用**被拉进构建：
   /// 一旦引用链变化（面板 prefab 换 AB、字段被清）真机就取不到，而场景里又没有对编目的引用，
   /// 于是整条角色资源不进包（真机日志：`未解析到出战编目` → `缺少出战编队会话或出战编目` → 退回主场景）。
   /// 详见 `Docs/03_专项设计与重构/14_真机资源与战斗装配_分阶段修复指南.md` D2/D3。
   ///
   /// **方案（用户决策）**：把编目及其依赖闭包**显式打成一个 AB**
   /// （bundle = <see cref="RoleDataBundleName"/>；只需标记 `Assets/Data/BattleTeamCatalog.asset`
   /// 一个资产，角色/武器 SO、模型 prefab、材质贴图会作为依赖闭包自动入包），
   /// 运行时由本服务经 ResKit 显式加载**并常驻持有**——不再依赖任何"场景/prefab 里恰好有引用"。
   ///
   /// **为什么必须常驻**：AB 卸载会销毁其中已加载的对象，而战斗装配、换人、切武器都会在运行时
   /// 反复按 itemID 解析同一批 SO 与 prefab；只要有静态引用还在，就不能让引用计数归零
   /// （09 关键坑 26 的教训）。代价是这批资源在进程生命周期内常驻，由 <see cref="Release"/> 显式释放。
   ///
   /// 本类刻意不是 MonoBehaviour：静态持有才能跨场景存活（0ConfigScene → 2MainScene → BattleGraybox）。
   /// </summary>
   public static class BattleAssetService
   {
      /// <summary>规范 bundle 名（按用途命名；P3 还会往这个包里继续加角色/武器资源，推荐统一到它）。</summary>
      public const string RoleDataBundleName = "battle_characters";

      /// <summary>
      /// 兼容候选 bundle 名（按顺序尝试，命中即用）。
      /// 第二个是 **Unity Inspector 的 AssetBundle 下拉「New…」默认名**（资产名小写 + "_" + 扩展名）：
      /// 2026-09-10 真机实测——手工标记时接受了默认名，bundle 变成 `battleteamcatalog_asset`，
      /// 而代码常量写的是 `battle_characters` → 真机清单里找不到 → 编目解析失败、退回主场景；
      /// 编辑器因为还有 `AssetDatabase` 回落所以完全看不出来（只有真机暴露）。
      /// 保留候选是为了让已打好的包立刻可用，不必为一个名字重打 40MB+ 的 AB；
      /// 统一命名仍建议走 `Thrud/Demo/Characters/1`（会写成规范名）。
      /// </summary>
      public static readonly string[] RoleDataBundleCandidates =
      {
         RoleDataBundleName,
         "battleteamcatalog_asset",
      };

      /// <summary>出战编目资源名（= 资产文件名；ResKit 内部按小写比对）。</summary>
      public const string CatalogAssetName = "BattleTeamCatalog";

      /// <summary>常驻 loader：持有角色/武器 AB 的引用，保证战斗中对象不被卸载。</summary>
      private static ResLoader sLoader;

      private static BattleTeamCatalog sCatalog;
      private static bool sReportedUnavailable;

      /// <summary>实际命中的 bundle 名（诊断用；未加载成功时为 null）。</summary>
      public static string ResolvedBundleName { get; private set; }

      /// <summary>
      /// 候选 bundle 里哪个在 AB 清单中可用（都没有则返回 null）。只读清单、零副作用、可反复调用。
      /// </summary>
      public static string ResolveAvailableBundle()
      {
         for (var i = 0; i < RoleDataBundleCandidates.Length; i++)
         {
            var candidate = RoleDataBundleCandidates[i];
            if (ResKitBootstrap.TryResolve(candidate, CatalogAssetName, typeof(BattleTeamCatalog)))
            {
               return candidate;
            }
         }

         return null;
      }

      /// <summary>
      /// 取常驻出战编目；AB 不可解析时返回 null（调用方按 场景引用 → 编辑器直读 依次回落）。
      ///
      /// 顺序很关键：**先查 AB 清单**（只读、零副作用、可反复调用），清单里没有就立刻回落——
      /// 绝不先直接 LoadSync：漏打标/漏打 AB 时，QFramework 会在
      /// <c>IResLoaderExtensions.LoadSync</c> → <c>ResLoader.LoadAssetSync</c> 里对 null 取 <c>.Asset</c>
      /// 抛 NRE，把调用方（面板 OnOpen / 战斗装配）整条链路带崩（2026-09-10 编辑器实测）。
      /// </summary>
      public static BattleTeamCatalog ResolveCatalog()
      {
         if (sCatalog != null)
         {
            return sCatalog;
         }

         // ① 先定 bundle（只读清单判定，命中即用；支持规范名与 Inspector 默认名两种标记）
         var bundle = ResolveAvailableBundle();
         if (bundle == null)
         {
            ReportUnavailableOnce();
            return null;
         }

         if (sLoader == null)
         {
            sLoader = ResLoader.Allocate();
         }

         try
         {
            sCatalog = sLoader.LoadSync<BattleTeamCatalog>(bundle, CatalogAssetName);
         }
         catch (System.Exception e)
         {
            // 兜底：即便清单能解析，加载过程本身也可能异常；一次失败不允许打断面板/战斗装配
            sCatalog = null;
            Debug.LogError($"[BattleAssets] 从 AB 加载出战编目异常（bundle={bundle}, asset={CatalogAssetName}）：{e}");
         }

         if (sCatalog != null)
         {
            sReportedUnavailable = false;
            ResolvedBundleName = bundle;

            var nameHint = bundle == RoleDataBundleName
               ? string.Empty
               : $"（注意：bundle 名「{bundle}」不是规范名「{RoleDataBundleName}」，" +
                 "建议用 Thrud/Demo/Characters/1 统一后重打 AB）";

            Debug.Log($"[BattleAssets] 角色/武器资源已从 AB 加载并常驻：{bundle}/{CatalogAssetName}" +
                      $"（角色 {sCatalog.Roles.Length} / 武器 {sCatalog.Weapons.Length}）{nameHint}");
            return sCatalog;
         }

         // 失败：回收 loader 避免留下无意义引用；下一次调用会重新尝试（清单可能是刚打好的）
         sLoader.Recycle2Cache();
         sLoader = null;
         ResolvedBundleName = null;
         ReportUnavailableOnce();
         return null;
      }

      /// <summary>
      /// 显式释放常驻的编目与 AB 引用（当前战斗流程不需要调用；留给未来热更/主动卸载）。
      /// 释放后再次 <see cref="ResolveCatalog"/> 会重新加载。
      /// </summary>
      public static void Release()
      {
         if (sLoader != null)
         {
            sLoader.Recycle2Cache();
            sLoader = null;
         }

         sCatalog = null;
         sReportedUnavailable = false;
         ResolvedBundleName = null;
      }

      /// <summary>
      /// 编目不可用只提示一次（清单查询是只读的、每次调用都会重试；但日志不能刷屏）。
      /// </summary>
      private static void ReportUnavailableOnce()
      {
         if (sReportedUnavailable)
         {
            return;
         }

         sReportedUnavailable = true;
         Debug.LogWarning(
            $"[BattleAssets] 角色/武器编目暂不可用：候选 bundle（{string.Join("、", RoleDataBundleCandidates)}）" +
            $"都不含资产 {CatalogAssetName}——本次回落场景引用 / 编辑器直读，编辑器内功能不受影响。\n" +
            $"  真机前置：把 Assets/Data/BattleTeamCatalog.asset 的 assetBundleName 设为 {RoleDataBundleName}" +
            "（菜单 Thrud/Demo/Characters/1），再执行 Thrud/Demo/Characters/2 构建 AB。" +
            "角色/武器 SO、模型 prefab、贴图会作为依赖闭包自动入包。\n" +
            "  编辑器说明：该判断来自 ResKit 初始化时的 AB 清单快照——标记/构建完重新进入 Play 即生效。");
      }
   }
}
