using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   public class BattlePauseMenuPanelData : UIPanelData
   {
      public string Title = string.Empty;
   }
   /// <summary>
   /// T3.1 暂停菜单面板（QFramework UIKit 标准 UIPanel）。
   /// prefab：Assets/Resources/Panels/PauseMenuPanel.prefab（Editor 工具生成，根节点挂本组件）。
   /// 由 BattleUiSystem 在暂停/恢复时经 UIKit OpenPanel/ClosePanel 添加/移除（UIRoot PopUI 层）；
   /// 「继续」→ 派发 <see cref="BattlePauseResumeRequestedEvent"/>（timeScale/暂停态由 UiSystem 复位）；
   /// 「重开本关」→ ClosePanel（移除）+ BattleLevelFlow.Restart（内部先复位 timeScale/Paused）。
   /// </summary>
   public partial class BattlePauseMenuPanel : UIPanel
   {
      [SerializeField] private Button resumeButton;
      [SerializeField] private Button restartButton;

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

         if (resumeButton)
         {
            resumeButton.onClick.AddListener(RequestResume);
         }

         if (restartButton)
         {
            restartButton.onClick.AddListener(RestartLevel);
         }
      }

      private static void RequestResume()
      {
         BattleArchitecture.Interface.SendEvent(new BattlePauseResumeRequestedEvent());
      }

      private void RestartLevel()
      {
         UIKit.ClosePanel<BattlePauseMenuPanel>();
         BattleArchitecture.Interface.GetSystem<BattleLevelFlow>().Restart();
      }
   }
}
