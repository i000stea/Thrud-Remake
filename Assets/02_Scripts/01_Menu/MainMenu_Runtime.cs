using QFramework;
using System.Collections;
using System.Collections.Generic;
using TeaGame.App;
using UnityEngine;
namespace TeaGame
{
   public class MainMenu_Runtime : MonoBehaviour
   {
      private IEnumerator Start()
      {
         // P0：ResKit 初始化收口——已初始化则只做清单校验（幂等）；
         // 不再在这里裸调 ResKit.Init()，也不再分配永不回收的空 ResLoader
         //（面板加载由 UIKit 自己的 ResKitPanelLoader 负责，额外 loader 只会泄漏引用计数）。
         ResKitBootstrap.EnsureInitialized();
         // 延迟一帧，等 UIKit 可用后再开面板
         yield return null;

         if (UIKit.GetPanel<Main_MenuPanel>() != null) yield break;

         // P1：统一走 UiPanelFlow.TryOpen（内部先摘除 UIKit 表里随旧场景卸载而残留的已销毁条目，
         // 否则这里的 OpenPanel 会复用已销毁实例并抛 MissingReferenceException → 主菜单面板不出现）
         var panel = UiPanelFlow.TryOpen<Main_MenuPanel>(
            UILevel.Bg,
            null,
            QAssetBundle.Main_menupanel_prefab.BundleName,
            QAssetBundle.Main_menupanel_prefab.Main_MenuPanel);

         if (panel == null)
         {
            Debug.LogError("[Main] 主菜单面板打开失败（详见上一条 [UI] 错误），本场景暂无可用入口。");
            yield break;
         }

         panel.btn_Test.clickEvent.AddListener(OpenLevelSelect);
      }

      /// <summary>
      /// T3.1C 步骤7（D10 入口链路）：测试按钮 → 关卡选择覆盖层 →（进入）→ 角色选择。
      /// 原实现直接打开角色选择；现按选关-介绍-进入链路先在 Main_FormationPanel 之前插入关卡选择。
      /// </summary>  
      private void OpenLevelSelect()  
      {
         // 经 UIKit 打开关卡选择覆盖层（数据由预置体上序列化的关卡目录提供）
         UiPanelFlow.TryOpen<LevelSelectPanel>(
            UILevel.PopUI,
            null,
            QAssetBundle.Levelselectpanel_prefab.BundleName,
            QAssetBundle.Levelselectpanel_prefab.LevelSelectPanel);
      }
   }
}