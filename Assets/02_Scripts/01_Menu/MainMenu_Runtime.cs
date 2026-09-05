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

         UIKit.OpenPanel<Main_MenuPanel>(
            UILevel.Common,
            null,
            QAssetBundle.Main_menupanel_prefab.BundleName,
            QAssetBundle.Main_menupanel_prefab.Main_MenuPanel);

      }
   }
}