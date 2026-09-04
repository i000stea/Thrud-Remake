using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 公共功能效果：范围爆发（AoE）。
   /// 以瞄准中心（锁敌点 / 相机射线落点）为圆心，对半径内全部敌人结算一次元素伤害。
   /// 伤害走 <see cref="BattleEnemyEntity.ApplyElementalDamage"/>，元素克制/状态/飘字随之生效。
   /// 数值（半径/伤害）来自当前槽位档案。
   /// </summary>
   public static class SkillEffect_AoEBurst
   {
      /// <summary>中心点求解的射线最大距离。</summary>
      private const float AimMaxDistance = 30f;

      public static void Execute(BattleSkillContext ctx)
      {
         var slot = ctx.SlotData;
         if (slot == null || slot.BurstDamage <= 0f || slot.BurstRadius <= 0f)
         {
            return;
         }

         var center = BattleSkillEffect.ResolveAimCenter(ctx, AimMaxDistance);
         var radius = slot.BurstRadius;
         var damage = slot.BurstDamage;
         var element = ctx.RoleElement;

         BattleSkillEffect.ShowImpactRing(center, radius, element);

         var radiusSqr = radius * radius;
         foreach (var enemy in BattleSkillEffect.EnumerateEnemies())
         {
            var offset = enemy.transform.position - center;
            if (offset.sqrMagnitude > radiusSqr)
            {
               continue;
            }

            enemy.ApplyElementalDamage(damage, element);
         }
      }
   }
}
