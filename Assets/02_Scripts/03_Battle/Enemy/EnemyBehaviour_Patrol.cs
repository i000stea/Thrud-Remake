using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.1 巡弋者行为（2026-09-03 规格）：固定"游走"姿态——
   /// 以生成点（首次 tick 位置锚定）为中心、WanderRadius 为半径随机游走，**不追踪玩家**；
   /// 攻击为指向玩家的单发速射。灰盒为浅灰/青小胶囊。
   /// </summary>
   public sealed class EnemyBehaviour_Patrol : IEnemyBehaviour
   {
      /// <summary>游走锚点（生成点）。</summary>
      private bool mHasAnchor;
      private Vector3 mAnchor;

      /// <summary>当前游走目标与更换计时。</summary>
      private Vector3 mWanderTarget;
      private float mRepositionTimer;

      /// <summary>攻击冷却计时。</summary>
      private float mShotCooldown;

      public void TickMove(BattleEnemyEntity enemy)
      {
         if (!mHasAnchor)
         {
            mAnchor = enemy.transform.position;
            mHasAnchor = true;
            PickNewWanderTarget(enemy, true);
         }

         // 距目标已近或计时到 → 换个游走点
         var toTarget = mWanderTarget - enemy.transform.position;
         if (toTarget.sqrMagnitude <= 0.35f * 0.35f)
         {
            PickNewWanderTarget(enemy, false);
         }
         else if (mRepositionTimer > 0f)
         {
            mRepositionTimer -= Time.deltaTime;
            if (mRepositionTimer <= 0f)
            {
               PickNewWanderTarget(enemy, false);
               return;
            }
         }

         var direction = toTarget.normalized;
         enemy.transform.position += direction * (enemy.MoveSpeed * Time.deltaTime);
      }

      public void TickAttack(BattleEnemyEntity enemy)
      {
         var player = BattleRuntimeBridge.PlayerTransform;
         if (!player) return;

         // 弹幕指向玩家（游走方向与玩家无关）
         if (mShotCooldown > 0f)
         {
            mShotCooldown -= Time.deltaTime;
            return;
         }

         mShotCooldown = Random.Range(enemy.AttackCooldownMin, enemy.AttackCooldownMax);
         enemy.FireBulletAt(0f);
      }

      public void OnBeforeDestroy(BattleEnemyEntity enemy)
      {
         // 巡弋者无死亡特效
      }

      private void PickNewWanderTarget(BattleEnemyEntity enemy, bool immediate)
      {
         var angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
         var radius = Mathf.Lerp(0.5f, Mathf.Max(1f, enemy.WanderRadius), Random.value);
         var x = mAnchor.x + Mathf.Cos(angle) * radius;
         var z = mAnchor.z + Mathf.Sin(angle) * radius;
         var y = mAnchor.y + Random.Range(-0.5f, 0.5f);
         mWanderTarget = new Vector3(x, y, z);
         mRepositionTimer = immediate ? 0f : Random.Range(1.5f, 3.5f);
      }
   }
}
