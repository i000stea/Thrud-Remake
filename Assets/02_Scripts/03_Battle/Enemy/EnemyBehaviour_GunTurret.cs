using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.1 站桩炮台行为（2026-09-03 规格）：固定"站桩"姿态，不移动；
   /// 指向玩家打直线/扇形弹幕（弹数/散布/弹速/冷却参数化，重装变体=高血慢速重弹）。
   /// 灰盒为橙色球体（重装变体=深红大球体或方块）。
   /// </summary>
   public sealed class EnemyBehaviour_GunTurret : IEnemyBehaviour
   {
      /// <summary>射击冷却计时。</summary>
      private float mShotCooldown;

      /// <summary>最大开火距离（超出不开火，避免灰盒远距离弹道无意义）。</summary>
      private const float MaxFireDistance = 30f;

      public void TickMove(BattleEnemyEntity enemy)
      {
         // 站桩炮台不移动。
      }

      public void TickAttack(BattleEnemyEntity enemy)
      {
         var player = BattlePlayerRegistry.CurrentTransform;
         if (!player) return;

         var distance = Vector3.Distance(enemy.transform.position, player.position);
         if (distance > MaxFireDistance) return;

         if (mShotCooldown > 0f)
         {
            mShotCooldown -= Time.deltaTime;
            return;
         }

         mShotCooldown = Random.Range(enemy.AttackCooldownMin, enemy.AttackCooldownMax);
         FireBarrage(enemy);
      }

      public void OnBeforeDestroy(BattleEnemyEntity enemy)
      {
         // 炮台无死亡特效
      }

      /// <summary>
      /// 以瞄准玩家方向为基准，按配置的数量与总散布角打一梭弹幕。
      /// </summary>
      private static void FireBarrage(BattleEnemyEntity enemy)
      {
         var count = Mathf.Max(1, enemy.BarrageCount);
         var spread = enemy.BarrageSpreadDeg;

         for (var i = 0; i < count; i++)
         {
            float yawOffset;
            if (count <= 1)
            {
               yawOffset = 0f;
            }
            else
            {
               var t = i / (float)(count - 1);
               yawOffset = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
            }

            enemy.FireBulletAt(yawOffset);
         }
      }
   }
}
