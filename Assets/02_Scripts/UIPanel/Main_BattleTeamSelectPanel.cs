using QFramework;
using UnityEngine.SceneManagement;

namespace TeaGame
{
   /// <summary>
   /// 预编队面板 Shell（v1.2 MVC 细则）。
   /// 本 partial 只做 UIKit 生命周期与装配：创建 <see cref="Main_BattleTeamSelectController"/>（C）、
   /// 以自身（实现 <see cref="IMainTeamSelectView"/>，见 View partial）注入，并把控制器“切换场景”请求落为
   /// ClosePanel + LoadScene。不含玩法/交互逻辑。
   /// </summary>
   public partial class Main_BattleTeamSelectPanel : UIPanel, IMainTeamSelectView
   {
      private Main_BattleTeamSelectController mController;

      protected override void OnInit(IUIData uiData)
      {
         // 绑定静态按钮 → View 事件（幂等；Controller 在每次 Open 时订阅）
         ((IMainTeamSelectView)this).WireView();
      }

      protected override void OnOpen(IUIData uiData)
      {
         mData = uiData as Main_BattleTeamSelectPanelData ?? new Main_BattleTeamSelectPanelData();
         var resolvedCatalog = ResolveCatalog();

         mController = new Main_BattleTeamSelectController(this);
         mController.OnRequestExitScene += HandleRequestExitScene;
         mController.Open(resolvedCatalog);
      }

      protected override void OnClose()
      {
         if (mController != null)
         {
            mController.OnRequestExitScene -= HandleRequestExitScene;
            mController.Close();
            mController = null;
         }
      }

      private BattleTeamCatalog ResolveCatalog()
      {
         var data = mData as Main_BattleTeamSelectPanelData;
         if (data != null && data.Catalog != null)
         {
            return data.Catalog;
         }

         // 编目回退引用：serialized 字段定义在 View partial（同一类型，直接可读）
         return catalog;
      }

      private void HandleRequestExitScene(string sceneName)
      {
         if (UIKit.GetPanel<Main_BattleTeamSelectPanel>() != null)
         {
            UIKit.ClosePanel<Main_BattleTeamSelectPanel>();
         }

         SceneManager.LoadScene(sceneName);
      }
   }
}
