using TeaGame.App;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 面板可用性辅助（T3.1A 链路；P1 于 2026-09-10 改造）。
   ///
   /// 原实现是「额外 Allocate 一个 ResLoader → LoadSync → Recycle2Cache」的 AB 预检：会把同一个 bundle
   /// 同步加载两遍，且回收时机与 ResKit 的延迟卸载耦合（真机/模拟模式表现不一致）。现改为**只查 AB 清单**
   /// （不加载任何 bundle、不产生引用计数）；打开面板本身统一走 <see cref="UiPanelFlow.TryOpen{T}"/>。
   ///
   /// 注：当前无调用者——主路径已收口为 MainMenu_Runtime / LevelSelectPanel 直接 OpenPanel。
   /// </summary>
   public static class BattleUiFlowPanelOpener
   {
      /// <summary>目标面板是否在 AB 清单中（ResKit 未初始化时返回 false）。</summary>
      public static bool TryEnsureAvailable(string bundleName, string assetName)
      {
         if (ResKitBootstrap.TryResolve(bundleName, assetName))
         {
            return true;
         }

         Debug.LogWarning($"[T3.1A] 面板资源不在 AB 清单中（bundle={bundleName}, asset={assetName}）。" +
                          "请确认预编队面板（04_Prefabs/Panel/Formation/Main_FormationPanel.prefab）已由 CodeGen 绑定、" +
                          "已按 QAssetBundle 标记 assetBundleName 并重新打 AB；旧生成器（TeaGame → Battle → " +
                          "Main_BattleTeamSelectPanel）已随 v1.2 移除。");
         return false;
      }
   }
}
