using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// T3.1 结算弹窗数据：标题文案由 BattleUiSystem 在打开时注入。
   /// </summary>
   public class Battle_LevelResultPanelData : UIPanelData
   {
      public string Title = string.Empty;
   }

   /// <summary>
   /// T3.1 结算弹窗面板（QFramework UIKit 标准 UIPanel）。
   /// prefab：Assets/Resources/Panels/BattleLevelResultPanel.prefab（CodeGen 绑定，根节点挂本组件）。
   /// BattleUiSystem 在 Victory/Defeat 时经 <see cref="UIKit.OpenPanel{T}"/> 加载到 UIRoot 的
   /// PopUI 层（添加），点击「再来一次」→ ClosePanel（移除）+ BattleLevelFlow.Restart；
   /// 「退出」→ 经 SceneFlow 加载遮罩回来源场景/0ConfigScene（v1.3，<see cref="BattleSceneGate"/>）。
   /// 面板根不挂 Canvas，交由 UIRoot 按 UILevel 归位。
   /// </summary>
   public partial class Battle_LevelResultPanel : UIPanel
   {
      private bool mWired;

      protected override void OnInit(IUIData uiData)
      {
         Wire();
      }

      protected override void OnOpen(IUIData uiData)
      {
         if (ResultTitle)
         {
            ResultTitle.text = uiData is not Battle_LevelResultPanelData data ? string.Empty : data.Title;
         }
      }

      protected override void OnClose()
      {
      }

      private void Wire()
      {
         if (mWired) return;
         mWired = true;

         if (RestartButton)
         {
            RestartButton.onClick.AddListener(RestartLevel);
         }

         if (ExitButton)
         {
            ExitButton.onClick.AddListener(ExitToMainScene);
         }
      }

      private void RestartLevel()
      {
         UIKit.ClosePanel<Battle_LevelResultPanel>();
         BattleArchitecture.Interface.GetSystem<BattleLevelFlow>().Restart();
      }

      /// <summary>退出：复位 timeScale 后经 SceneFlow 加载遮罩回来源场景/0ConfigScene（v1.3）。</summary>
      private void ExitToMainScene()
      {
         // 清空面板表再离场，避免 UIRoot 随场景卸载后 Table 残留已销毁面板
         UIKit.CloseAllPanel();

         Time.timeScale = 1f;
         BattleSceneGate.TransitionTo(BattleTeamSession.ReturnSceneName);
      }
   }
}
