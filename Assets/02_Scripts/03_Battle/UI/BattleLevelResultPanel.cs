using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// T3.1 结算弹窗数据：标题文案由 BattleUiSystem 在打开时注入。
   /// </summary>
   public class BattleLevelResultPanelData : UIPanelData
   {
      public string Title = string.Empty;
   }

   /// <summary>
   /// T3.1 结算弹窗面板（QFramework UIKit 标准 UIPanel）。
   /// prefab：Assets/Resources/Panels/BattleLevelResultPanel.prefab（CodeGen 绑定，根节点挂本组件）。
   /// BattleUiSystem 在 Victory/Defeat 时经 <see cref="UIKit.OpenPanel{T}"/> 加载到 UIRoot 的
   /// PopUI 层（添加），点击「再来一次」→ ClosePanel（移除）+ BattleLevelFlow.Restart；
   /// 「退出」→ 回到主场景 <see cref="BattleTeamSession.MainSceneName"/>（T3.1A）。
   /// 面板根不挂 Canvas，交由 UIRoot 按 UILevel 归位。
   /// </summary>
   public partial class BattleLevelResultPanel : UIPanel
   {
      [SerializeField] private Text titleText;
      [SerializeField] private Button restartButton;
      [SerializeField] private Button exitButton;

      private bool mWired;

      protected override void OnInit(IUIData uiData)
      {
         Wire();
      }

      protected override void OnOpen(IUIData uiData)
      {
         var data = uiData as BattleLevelResultPanelData;
         if (titleText)
         {
            titleText.text = data == null ? string.Empty : data.Title;
         }
      }

      protected override void OnClose()
      {
      }

      private void Wire()
      {
         if (mWired) return;
         mWired = true;

         if (restartButton)
         {
            restartButton.onClick.AddListener(RestartLevel);
         }

         if (exitButton)
         {
            exitButton.onClick.AddListener(ExitToMainScene);
         }
      }

      private void RestartLevel()
      {
         UIKit.ClosePanel<BattleLevelResultPanel>();
         BattleArchitecture.Interface.GetSystem<BattleLevelFlow>().Restart();
      }

      /// <summary>退出到来源场景/0ConfigScene（v1.0 A3）。</summary>
      private void ExitToMainScene()
      {
         if (UIKit.GetPanel<BattleLevelResultPanel>() != null)
         {
            UIKit.ClosePanel<BattleLevelResultPanel>();
         }

         Time.timeScale = 1f;
         SceneManager.LoadScene(BattleTeamSession.ReturnSceneName);
      }
   }
}
