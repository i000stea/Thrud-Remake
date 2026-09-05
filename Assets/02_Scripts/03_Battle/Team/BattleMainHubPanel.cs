using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeaGame
{
   public class BattleMainHubPanelData : UIPanelData
   {
   }

   /// <summary>
   /// T3.1A 极简主场景面板：只有一个入口「出战编队」。
   /// prefab：Assets/Resources/Panels/BattleMainHubPanel.prefab（Editor 工具生成，根节点挂本组件，AB=battlemainhubpanel_prefab）。
   /// 「出战编队」→ 清空旧会话 → 进入选队场景（正式主城/关卡选择在 M2 替换本灰盒入口）。
   /// </summary>
   public class BattleMainHubPanel : UIPanel
   {
      [SerializeField] private Text titleText;
      [SerializeField] private Text hintText;
      [SerializeField] private Button startButton;

      private bool mWired;

      protected override void OnInit(IUIData uiData)
      {
         Wire();
      }

      protected override void OnOpen(IUIData uiData)
      {
         if (titleText)
         {
            titleText.text = "M1 · 出战编队入口";
         }

         if (hintText)
         {
            hintText.text = "灰盒主场景：点击「出战编队」进入 1-4 人角色×武器编队选择。\n（正式主城 / 关卡选择将于 M2 接入）";
         }
      }

      protected override void OnClose()
      {
      }

      private void Wire()
      {
         if (mWired) return;
         mWired = true;

         if (startButton)
         {
            startButton.onClick.AddListener(OpenTeamSelect);
         }
      }

      private void OpenTeamSelect()
      {
         if (UIKit.GetPanel<BattleMainHubPanel>() != null)
         {
            UIKit.ClosePanel<BattleMainHubPanel>();
         }

         BattleTeamSession.Clear(); // 新一次出战：旧编队作废
         SceneManager.LoadScene(BattleTeamSession.TeamSelectSceneName);
      }
   }
}
