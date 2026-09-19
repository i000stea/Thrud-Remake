using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame.BattleEditor
{
   /// <summary>Actor 迁移的资产收尾；不手写 YAML，不改变 prefab GUID。</summary>
   public static class BattleActorMigrationSetup
   {
      private const string RigPath = "Assets/04_Prefabs/03Battle/Battle_PlayerControl.prefab";
      private const string PanelPath = "Assets/04_Prefabs/Panel/Battle/Battle_MainPanel.prefab";

      [MenuItem("Thrud/Battle/Actor/1. 校验主 HUD 并移除 rig 旧 HUD")]
      public static void Migrate()
      {
         if (EditorApplication.isPlayingOrWillChangePlaymode)
         {
            Debug.LogError("[Actor迁移] 请退出 Play 后执行。");
            return;
         }
         // 先验证接收方，失败时不改任何资产。
         var panel = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
         var view = panel ? panel.GetComponent<Battle_MainPanel>() : null;
         var ammo = panel ? panel.transform.Find("WeaponAmmoUI") : null;
         if (!view || !view.Cross_LockEnemy || !view.Health_Main || !view.Health_Value
             || !view.SkillHudRoleLine || !view.SkillHudELine || !view.SkillHudQLine
             || !view.SkillHudSwitchLine || !ammo || !ammo.GetComponent<Text>())
         {
            throw new InvalidOperationException("[Actor迁移] 主 HUD 引用或 WeaponAmmoUI 缺失，未修改 rig。请检查 " + PanelPath);
         }

         var root = PrefabUtility.LoadPrefabContents(RigPath);
         try
         {
            var oldHud = root.transform.Find("MainCanvas");
            if (oldHud)
            {
               UnityEngine.Object.DestroyImmediate(oldHud.gameObject);
               PrefabUtility.SaveAsPrefabAsset(root, RigPath);
               Debug.Log("[Actor迁移] 已移除 rig 的旧 MainCanvas（含 Bind 节点）；主 HUD 校验通过。请检查场景实例是否存在旧 HUD 覆盖，再重新生成 rig CodeGen。发布前重建 AB。");
            }
            else Debug.Log("[Actor迁移] rig 已无旧 HUD，主 HUD 校验通过，无需修改。");
         }
         finally { PrefabUtility.UnloadPrefabContents(root); }
      }
   }
}
