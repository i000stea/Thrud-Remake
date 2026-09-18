using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   public class Battle_PauseMenuPanelData : UIPanelData
   {
      public string Title = string.Empty;
   }
   /// <summary>
   /// T3.1 暂停菜单面板（QFramework UIKit 标准 UIPanel）。
   /// prefab：Assets/Resources/Panels/BattlePauseMenuPanel.prefab（CodeGen 绑定，根节点挂本组件）。
   /// 由 BattleUiSystem 在暂停/恢复时经 UIKit OpenPanel/ClosePanel 添加/移除（UIRoot PopUI 层）；
   /// 「继续」→ 派发 <see cref="BattlePauseResumeRequestedEvent"/>（timeScale/暂停态由 UiSystem 复位）；
   /// 「重新开始」→ ClosePanel（移除）+ BattleLevelFlow.Restart（内部先复位 timeScale/Paused）；
   /// 「退出」→ 复位暂停态并经 SceneFlow 加载遮罩回来源场景/0ConfigScene（v1.3，<see cref="BattleSceneGate"/>）。
   /// </summary>
   public partial class Battle_PauseMenuPanel : UIPanel
   {  
      private bool mWired;

      protected override void OnInit(IUIData uiData)
      {
         Wire();
      }

      protected override void OnClose()
      {
      }

      private void Wire()
      {
         if (mWired) return;
         mWired = true;

         if (ResumeButton)
         {
            ResumeButton.onClick.AddListener(RequestResume);
         }

         if (RestartButton)
         {
            RestartButton.onClick.AddListener(RestartLevel);
         }

         if (ExitButton)
         {
            ExitButton.onClick.AddListener(ExitToMainScene);
         }
      }

      private static void RequestResume()
      {
         BattleArchitecture.Interface.SendEvent(new BattlePauseResumeRequestedEvent());
      }

      private void RestartLevel()
      {
         UIKit.ClosePanel<Battle_PauseMenuPanel>();
         BattleArchitecture.Interface.GetSystem<BattleLevelFlow>().Restart();
      }

      /// <summary>退出本关：复位暂停态（timeScale/Paused）后经 SceneFlow 加载遮罩回来源场景/0ConfigScene（v1.3）。</summary>
      private void ExitToMainScene()
      {
         // 清空面板表再离场（用 UiPanelFlow.CloseAllSafely：UIKit.CloseAllPanel 碰到已销毁条目会抛异常而中断清表）
         UiPanelFlow.CloseAllSafely();

         var state = BattleArchitecture.Interface.GetModel<BattleStateModel>();
         if (state.Paused)
         {
            state.Paused = false;
         }

         Time.timeScale = 1f;
         BattleSceneGate.TransitionTo(BattleTeamSession.ReturnSceneName);
      }
   }
}
