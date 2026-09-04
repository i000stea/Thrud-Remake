using DG.Tweening;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 公共功能效果：聚怪（把周围敌人拉向中心）。
   /// 以瞄准中心为圆心，把半径内敌人沿水平方向拉至"中心与外圈的保留比例"处；
   /// 使用 DOTween 移动敌人 transform（敌人无刚体、移动由行为直接改位置，无物理冲突）。
   /// 与元素伤害解耦：如需"拉怪 + 冻结/易伤"，由执行器在聚怪后追加一次伤害效果。
   /// </summary>
   public static class SkillEffect_PullEnemies
   {
      /// <summary>中心点求解的射线最大距离。</summary>
      private const float AimMaxDistance = 30f;

      /// <summary>拉扯动画时长（秒）。</summary>
      private const float PullDuration = 0.35f;

      public static void Execute(BattleSkillContext ctx)
      {
         var slot = ctx.SlotData;
         if (slot == null || slot.PullRadius <= 0f)
         {
            return;
         }

         var center = BattleSkillEffect.ResolveAimCenter(ctx, AimMaxDistance);
         var radius = slot.PullRadius;
         var keepRatio = Mathf.Clamp(slot.PullKeepRatio, 0.1f, 0.6f);

         BattleSkillEffect.ShowImpactRing(center, radius, ctx.RoleElement);

         var radiusSqr = radius * radius;
         foreach (var enemy in BattleSkillEffect.EnumerateEnemies())
         {
            var position = enemy.transform.position;
            var offset = new Vector3(position.x - center.x, 0f, position.z - center.z);
            if (offset.sqrMagnitude <= 0.0001f)
            {
               continue;
            }

            if (offset.sqrMagnitude > radiusSqr)
            {
               continue;
            }

            // 打断该敌人身上旧的拉扯/位移，避免多段技能互相打架
            DOTween.Kill(enemy.transform);

            var target = new Vector3(
               center.x + offset.x * keepRatio,
               position.y,
               center.z + offset.z * keepRatio);

            enemy.transform.DOMove(target, PullDuration).SetEase(Ease.OutQuad);
         }
      }
   }
}
