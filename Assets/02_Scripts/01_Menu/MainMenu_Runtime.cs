using QFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TeaGame
{
   public class MainMenu_Runtime : MonoBehaviour
   {
      private IEnumerator Start()
      {
         // 延迟一帧，等 UIKit 可用后再开面板
         yield return null;

         if (UIKit.GetPanel<Main_MenuPanel>() != null) yield break;

         var a = UIKit.OpenPanel<Main_MenuPanel>(
            UILevel.Bg,
            null,
            QAssetBundle.Main_menupanel_prefab.BundleName);

         a.btn_Test.clickEvent.AddListener(OpenTestTeam);
      }

      private void OpenTestTeam()
      {
        //  // 经 SceneFlow 加载编队场景：保留旧场景不卸载、不显示 Loading 遮罩，完成后打印日志
        //  TeaGame.App.SceneFlow.Instance?.RequestLoad(new TeaGame.App.SceneRequest
        //  {
        //     SceneName = "M1_TeamSelect",
        //     ShowLoading = false,
        //     KeepPrevious = true,
        //     OnComplete = () => Debug.Log("[OpenTestTeam] 编队场景加载完成")
        //  });
         var a = UIKit.OpenPanel<Main_FormationPanel>(
            UILevel.PopUI,
            null,
            QAssetBundle.Main_battleteamselectpanel_prefab.BundleName);

      }
   }
}