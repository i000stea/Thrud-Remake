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
         ResKit.Init();
         ResLoader.Allocate();
         // 延迟一帧，等 UIKit 可用后再开面板
         yield return null;

         if (UIKit.GetPanel<Main_MenuPanel>() != null) yield break;

         var a = UIKit.OpenPanel<Main_MenuPanel>(
            UILevel.Bg,
            null,
            QAssetBundle.Main_menupanel_prefab.BundleName, 
            QAssetBundle.Main_menupanel_prefab.Main_MenuPanel);

         a.btn_Test.clickEvent.AddListener(OpenLevelSelect);
      }

      /// <summary>
      /// T3.1C 步骤7（D10 入口链路）：测试按钮 → 关卡选择覆盖层 →（进入）→ 角色选择。
      /// 原实现直接打开角色选择；现按选关-介绍-进入链路先在 Main_FormationPanel 之前插入关卡选择。
      /// </summary>  
      private void OpenLevelSelect()  
      {
         // 经 UIKit 打开关卡选择覆盖层（数据由预置体上序列化的关卡目录提供）
         var a = UIKit.OpenPanel<LevelSelectPanel>(
            UILevel.PopUI,
            null,
            QAssetBundle.Levelselectpanel_prefab.BundleName,
            QAssetBundle.Levelselectpanel_prefab.LevelSelectPanel);
      }
   }
}