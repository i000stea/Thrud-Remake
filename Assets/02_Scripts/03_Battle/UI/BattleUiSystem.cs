using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗 UI 系统（T3.1：结算/暂停面板走 QFramework UIKit 的 UIRoot 机制添加/移除）。
   ///
   /// 面板本身是标准 <see cref="UIPanel"/>，由 UIKit 从 AssetBundle（ResKit，见 QAssetBundle 常量）
   /// 加载 prefab 并挂到 UIRoot 的 PopUI 层；本系统只负责两件事：
   /// 1. 监听关卡状态/暂停请求事件，决定"何时 OpenPanel / ClosePanel"哪个面板；
   /// 2. 统管暂停状态（Paused / timeScale / 鼠标），与 Battle_PlayerControl 完全解耦。
   /// 不再持有任何 UI prefab 引用、不做运行时 UI 构建、不自行实例化面板。
   /// </summary>
   public sealed class BattleUiSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      private IUnRegister mLevelStateUnregister;
      private IUnRegister mPauseToggleUnregister;
      private IUnRegister mPauseResumeUnregister;

      protected override void OnInit()
      {
         mLevelStateUnregister = Architecture.RegisterEvent<BattleLevelStateChangedEvent>(OnLevelStateChanged);
         mPauseToggleUnregister = Architecture.RegisterEvent<BattlePauseToggleRequestedEvent>(_ => TogglePause());
         mPauseResumeUnregister = Architecture.RegisterEvent<BattlePauseResumeRequestedEvent>(_ => ResumeGame());
      }

      protected override void OnDeinit()
      {
         mLevelStateUnregister?.UnRegister();
         mLevelStateUnregister = null;
         mPauseToggleUnregister?.UnRegister();
         mPauseToggleUnregister = null;
         mPauseResumeUnregister?.UnRegister();
         mPauseResumeUnregister = null;
      }

      // ---------------- AB 面板加载 ----------------

      // P1（2026-09-10）：原先这里有个 TryEnsurePanelAvailable——额外 Allocate 一个 ResLoader，
      // LoadSync 校验面板 prefab 后再 Recycle2Cache。该写法的两个问题：
      //   1) 会把同一个 bundle 同步加载两遍（formation 面板 bundle 实测 43.8MB）；
      //   2) 正确性依赖 ResKit「延迟卸载」的时机（回收后引用计数归零 → ResMgr.Update 里的
      //      RemoveUnusedRes 卸载 AB → 可能销毁刚加载的 prefab），真机与编辑器 SimulationMode 表现不一致。
      // 现统一走 UiPanelFlow.TryOpen：内部先清理 UIKit 表里的已销毁残留，再用 OpenPanel 的返回值判定成败。

      // ---------------- 结算面板 ----------------

      private void OnLevelStateChanged(BattleLevelStateChangedEvent e)
      {
         switch (e.To)
         {
            case BattleLevelState.Victory:
               OpenResult("胜利！");
               break;
            case BattleLevelState.Defeat:
               OpenResult("失败…");
               break;
            default:
               CloseResultIfOpen(); // Prepare/Wave/Exit：不残留结算面板
               break;
         }
      }

      private static void OpenResult(string title)
      {
         var panel = UiPanelFlow.TryOpen<Battle_LevelResultPanel>(
            UILevel.PopUI,
            new Battle_LevelResultPanelData { Title = title },
            QAssetBundle.Battle_levelresultpanel_prefab.BundleName,
            QAssetBundle.Battle_levelresultpanel_prefab.Battle_LevelResultPanel);

         if (panel == null)
         {
            Debug.LogWarning("[T3.1] 结算面板未能打开（战斗流程不因此中断，详见上一条 [UI] 错误）。");
         }
      }

      private static void CloseResultIfOpen()
      {
         UiPanelFlow.CloseAlive<Battle_LevelResultPanel>();
      }

      // ---------------- 暂停 ----------------
      /// <summary>
      /// 暂停
      /// </summary>
      private void TogglePause()
      {
         var state = State;
         if (state.LevelState == BattleLevelState.Victory || state.LevelState == BattleLevelState.Defeat)
         {
            return; // 终局由结算弹窗接管
         }

         if (state.Paused)
         {
            ResumeGame();
         }
         else
         {
            PauseGame();
         }
      }

      /// <summary>
      /// 暂停游戏
      /// </summary>
      private void PauseGame()
      {
         // 获取状态，避免重复调用
         var state = State;
         if (state.Paused) return;

         // 先打开面板（失败则不改状态，保持与旧实现一致的"面板打不开就不暂停"语义），
         // 面板自身经 UIKit 的 ResKitPanelLoader 从 AB 加载并持有该 bundle 的引用。
         var panel = UiPanelFlow.TryOpen<Battle_PauseMenuPanel>(
            UILevel.PopUI,
            null,
            QAssetBundle.Battle_pausemenupanel_prefab.BundleName,
            QAssetBundle.Battle_pausemenupanel_prefab.Battle_PauseMenuPanel);

         if (panel == null)
         {
            Debug.LogWarning("[T3.1] 暂停面板未能打开，本次暂停请求已忽略（详见上一条 [UI] 错误）。");
            return;
         }

         state.Paused = true;
         Time.timeScale = 0f;
         Architecture.GetSystem<BattleInputSystem>().Clear();
         Cursor.lockState = CursorLockMode.None;
         Cursor.visible = true;

         Debug.Log("[T3.1] 游戏暂停（timeScale=0，进程未暂停）——ESC 或点「继续」恢复。");
      }

      /// <summary>
      /// 继续游戏
      /// </summary>
      private void ResumeGame()
      {
         var state = State;
         if (!state.Paused) return;

         state.Paused = false;
         Time.timeScale = 1f;
         Architecture.GetSystem<BattleInputSystem>().Clear();

         UiPanelFlow.CloseAlive<Battle_PauseMenuPanel>(); // 从 UIRoot 移除暂停菜单（已销毁则跳过）
      }
   }
}
