using System.Collections;
using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T3.1A 极简主场景引导：启动后打开 <see cref="BattleMainHubPanel"/>。
   /// 挂在 M1_MainHub.unity 的场景对象上（Editor 工具生成场景时自动挂载）。
   /// </summary>
   public class BattleMainHubBootstrap : MonoBehaviour
   {
      private IEnumerator Start()
      {
         // 延迟一帧，等 UIKit 可用后再开面板
         yield return null;

         if (UIKit.GetPanel<BattleMainHubPanel>() != null) yield break;

         if (!BattleUiFlowPanelOpener.TryEnsureAvailable(BattleUiFlowNames.MainHubBundle, BattleUiFlowNames.MainHubPanel))
         {
            yield break;
         }

         UIKit.OpenPanel<BattleMainHubPanel>(
            UILevel.Common,
            null,
            BattleUiFlowNames.MainHubBundle,
            BattleUiFlowNames.MainHubPanel);
      }
   }
}
