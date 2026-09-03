using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.1 自爆·地雷行为（2026-09-03 规格）：出生后静置不动；
   /// 玩家进入感应半径（BlastRadius，较大值）或被击杀 → 较大范围爆炸（BlastDamage）。
   /// 玩家击杀地雷时若距离过近会被波及——需在安全距离处理。
   /// 灰盒为暗红球体/扁球。
   /// </summary>
   public sealed class EnemyBehaviour_Mine : IEnemyBehaviour
   {
      /// <summary>是否已引爆（防二次结算）。</summary>
      private bool mBlasted;

      public void TickMove(BattleEnemyEntity enemy)
      {
         // 地雷静置，不移动。
      }

      public void TickAttack(BattleEnemyEntity enemy)
      {
         var player = BattleRuntimeBridge.PlayerTransform;
         if (!player) return;

         var distance = Vector3.Distance(enemy.transform.position, player.position);
         if (distance > enemy.BlastRadius) return;

         // 玩家踩入感应半径 → 主动引爆
         DoBlast(enemy);
         enemy.Die();
      }

      public void OnBeforeDestroy(BattleEnemyEntity enemy)
      {
         // 被击杀销毁前补一次引爆（幂等：主动引爆后不会二次结算）
         DoBlast(enemy);
      }

      private void DoBlast(BattleEnemyEntity enemy)
      {
         if (mBlasted) return;
         mBlasted = true;

         // 灰盒可视化：先闪出爆炸范围圈，再结算伤害
         enemy.ShowBlastRing(enemy.BlastRadius);
         enemy.DamagePlayerInRadius(enemy.BlastRadius, enemy.BlastDamage);
      }
   }
}
