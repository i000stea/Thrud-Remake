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

      // ---------------- AB 面板 prefab 校验 ----------------

      /// <summary>
      /// 校验战斗 UI 面板 prefab 能否经 ResKit（AssetBundle）加载。
      /// 面板 prefab 已打 AB（meta 的 assetBundleName 与 QAssetBundle 常量对应），
      /// 不再用 Resources.Load（路径加载，与 AB 资源定位不一致）。
      /// 加载成功返回 true；失败打警告并返回 false（此时调用方应中止后续打开面板）。
      /// </summary>
      private static bool TryEnsurePanelAvailable(string panelDisplayName, string bundleName, string assetName)
      {
         var loader = ResLoader.Allocate();
         var prefab = loader.LoadSync<GameObject>(bundleName, assetName);
         loader.Recycle2Cache();

         if (prefab != null) return true;

         Debug.LogWarning($"[T3.1] 未找到 {panelDisplayName} 面板 prefab（bundle={bundleName}, asset={assetName}）。" +
                          "请运行 TeaGame → Battle → Rebuild Battle UI Prefabs 并确认 prefab 已按 QAssetBundle 标记 AB。");
         return false;
      }

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
         if (!TryEnsurePanelAvailable("结算", QAssetBundle.Battlelevelresultpanel_prefab.BundleName,
                QAssetBundle.Battlelevelresultpanel_prefab.Battle_LevelResultPanel))
         {
            return;
         }

         UIKit.OpenPanel<BattleLevelResultPanel>(
            UILevel.PopUI,
            new BattleLevelResultPanelData { Title = title },
            assetBundleName: QAssetBundle.Battlelevelresultpanel_prefab.BundleName,
            prefabName: QAssetBundle.Battlelevelresultpanel_prefab.Battle_LevelResultPanel);
      }

      private static void CloseResultIfOpen()
      {
         if (UIKit.GetPanel<BattleLevelResultPanel>() != null)
         {
            UIKit.ClosePanel<BattleLevelResultPanel>();
         }
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

         // 先经 ResKit 校验 AB 资源，避免状态已改但面板加载失败
         if (!TryEnsurePanelAvailable("暂停", QAssetBundle.Battlepausemenupanel_prefab.BundleName,
                QAssetBundle.Battlepausemenupanel_prefab.Battle_PauseMenuPanel))
         {
            return;
         }

         state.Paused = true;
         Time.timeScale = 0f;
         Architecture.GetSystem<BattleInputSystem>().Clear();
         Cursor.lockState = CursorLockMode.None;
         Cursor.visible = true;

         // UIKit 的 PanelLoader 由 SupportOldQF（ResKitPanelLoaderPool）提供，
         // 传 assetBundleName + prefab 资源名即走 AssetBundle 加载（不再走 Resources.Load）。
         UIKit.OpenPanel<BattlePauseMenuPanel>(
            UILevel.PopUI,
            assetBundleName: QAssetBundle.Battlepausemenupanel_prefab.BundleName,
            prefabName: QAssetBundle.Battlepausemenupanel_prefab.Battle_PauseMenuPanel);
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

         if (UIKit.GetPanel<BattlePauseMenuPanel>() != null)
         {
            UIKit.ClosePanel<BattlePauseMenuPanel>(); // 从 UIRoot 移除暂停菜单
         }
      }
   }
}
