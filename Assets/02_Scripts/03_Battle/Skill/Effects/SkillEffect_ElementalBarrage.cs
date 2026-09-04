using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 公共功能效果：元素弹幕（一次水平扇面齐射）。
   /// 以武器开火点为起点，按当前面向（锁敌 → 锁定点；否则相机前向）铺开扇面发射多发
   /// 携带角色元素的子弹。子弹走对象池与 BattleBullet 新路径，命中照常结算/攒能。
   /// </summary>
   public static class SkillEffect_ElementalBarrage
   {
      public static void Execute(BattleSkillContext ctx)
      {
         var slot = ctx.SlotData;
         if (slot == null || slot.ProjectileCount <= 0 || slot.ProjectileDamage <= 0f)
         {
            return;
         }

         var firePoint = ctx.FirePoint;
         var origin = firePoint ? firePoint.position : ctx.Origin + Vector3.up * 1.4f;

         // 基础朝向：锁敌 → 指向锁定点；未锁敌 → 相机/控制器前向。
         Quaternion baseRotation;
         var lockTarget = ctx.LockTarget;
         if (lockTarget)
         {
            var direction = lockTarget.position - origin;
            baseRotation = direction.sqrMagnitude > 0.0001f
               ? Quaternion.LookRotation(direction.normalized)
               : Quaternion.identity;
         }
         else
         {
            var forward = ctx.AimForward;
            baseRotation = forward.sqrMagnitude > 0.0001f
               ? Quaternion.LookRotation(forward.normalized)
               : Quaternion.identity;
         }

         var count = slot.ProjectileCount;
         var spread = Mathf.Max(0f, slot.SpreadDeg);
         var step = count > 1 ? spread / (count - 1) : 0f;
         var startAngle = -spread * 0.5f;

         for (var i = 0; i < count; i++)
         {
            var rotation = baseRotation * Quaternion.Euler(0f, startAngle + step * i, 0f);
            BattleSkillEffect.SpawnElementBullet(
               ctx,
               origin,
               rotation,
               slot.ProjectileDamage,
               ctx.RoleElement,
               slot.ProjectileLife);
         }
      }
   }
}
