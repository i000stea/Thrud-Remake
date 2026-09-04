using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.1 自爆·导弹行为（2026-09-03 规格）：高速追踪玩家，贴近（≤BlastRadius，小值）
   /// 后小范围爆炸并自毁。被击落（外部伤害致死）不爆炸。
   /// 灰盒为黄色小胶囊/球体。
   /// </summary>
   public sealed class EnemyBehaviour_Missile : IEnemyBehaviour
   {
      /// <summary>追踪超时（秒）：超时自毁（无伤），避免玩家脱战后导弹无限巡航。</summary>
      private const float MaxLifetime = 20f;

      private float mLifetime;

      public void TickMove(BattleEnemyEntity enemy)
      {
         var player = BattleRuntimeBridge.PlayerTransform;
         if (!player) return;

         // 生命周期计时
         mLifetime += Time.deltaTime;

         var toPlayer = player.position - enemy.transform.position;
         var distance = toPlayer.magnitude;

         // 已贴近玩家 → 交给 TickAttack 引爆；否则继续追踪
         if (distance <= enemy.BlastRadius) return;

         var direction = toPlayer / Mathf.Max(0.01f, distance);
         enemy.transform.position += direction * (enemy.CurrentMoveSpeed * Time.deltaTime);
      }

      public void TickAttack(BattleEnemyEntity enemy)
      {
         var player = BattleRuntimeBridge.PlayerTransform;
         if (player)
         {
            var distance = Vector3.Distance(enemy.transform.position, player.position);
            if (distance <= enemy.BlastRadius)
            {
               BlowUp(enemy);
               return;
            }
         }

         if (mLifetime >= MaxLifetime)
         {
            enemy.Die(); // 超时自毁（无伤）
         }
      }

      public void OnBeforeDestroy(BattleEnemyEntity enemy)
      {
         // 被击落/超时自毁：不产生爆炸
      }

      private static void BlowUp(BattleEnemyEntity enemy)
      {
         // 灰盒可视化：先闪出爆炸范围圈（=实际伤害半径），再结算伤害
         enemy.ShowBlastRing(enemy.BlastRadius * 1.4f);
         enemy.DamagePlayerInRadius(enemy.BlastRadius * 1.4f, enemy.BlastDamage);
         enemy.Die();
      }
   }
}
