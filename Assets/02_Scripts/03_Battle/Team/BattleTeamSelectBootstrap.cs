using System.Collections;
using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T3.1A 出战编队场景引导：启动后打开 <see cref="BattleTeamSelectPanel"/>（注入编目）。
   /// 挂在 M1_TeamSelect.unity 的场景对象上；编目 <see cref="BattleTeamCatalog"/> 在编辑器内生成并引用。
   /// </summary>
   public class BattleTeamSelectBootstrap : MonoBehaviour
   {
      [SerializeField] private BattleTeamCatalog catalog;

      private IEnumerator Start()
      {
         yield return null;

         if (UIKit.GetPanel<BattleTeamSelectPanel>() != null) yield break;

         if (!BattleUiFlowPanelOpener.TryEnsureAvailable(BattleUiFlowNames.TeamSelectBundle, BattleUiFlowNames.TeamSelectPanel))
         {
            yield break;
         }

         UIKit.OpenPanel<BattleTeamSelectPanel>(
            UILevel.Common,
            new BattleTeamSelectPanelData { Catalog = catalog },
            BattleUiFlowNames.TeamSelectBundle,
            BattleUiFlowNames.TeamSelectPanel);
      }
   }
}
