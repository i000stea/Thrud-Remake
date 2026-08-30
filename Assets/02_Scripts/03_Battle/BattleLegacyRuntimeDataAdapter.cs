using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 旧版运行时数据适配器。
   /// 该类用于把 `EntityBattleData` 包装为新版 `BattleRoleRuntimeData` 可用的结构，
   /// 让迁移过程可以分阶段进行，而无需一次性替换所有场景和 prefab。
   /// </summary>
   public static class BattleLegacyRuntimeDataAdapter
   {
      /// <summary>
      /// 把旧版运行时数据转换成新版运行时数据。
      /// </summary>
      public static BattleRoleRuntimeData Convert(EntityBattleData legacyData)
      {
         if (legacyData == null) return null;

         return new BattleRoleRuntimeData(
            legacyData.roleName,
            legacyData.wepName,
            legacyData.roleInst,
            legacyData.roleAnim,
            legacyData.wepInst,
            new LegacyBattleWeaponViewAdapter(legacyData.wepComponent),
            new LegacyBattleWeaponModeAdapter(legacyData.wepMode));
      }
   }

   /// <summary>
   /// 旧版武器模式适配器。
   /// 新武器系统只关心“按下扳机”这一行为，因此可以通过轻量包装复用旧 `WepMode_0Base`。
   /// </summary>
   public sealed class LegacyBattleWeaponModeAdapter : BattleWeaponModeBase
   {
      private readonly WepMode_0Base mLegacyMode;

      public LegacyBattleWeaponModeAdapter(WepMode_0Base legacyMode)
      {
         mLegacyMode = legacyMode;
      }

      public override void PressTrigger()
      {
         mLegacyMode?.PressTriggrt();
      }
   }
}
