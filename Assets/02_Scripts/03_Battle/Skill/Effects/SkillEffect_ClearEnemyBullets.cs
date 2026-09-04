namespace TeaGame
{
   /// <summary>
   /// 公共功能效果：清弹幕（清除场上所有存活的敌人子弹）。
   /// 遍历 <see cref="BattleEnemyBullet"/> 维护的存活集合，将每个实例立即回收到对象池。
   /// 用于风系"吹散弹幕"等应对型技能；敌人本体的子弹（炮台连发、追踪弹）一旦被清空，
   /// 本波次内敌人重新装填前玩家有窗口期。
   /// </summary>
   public static class SkillEffect_ClearEnemyBullets
   {
      public static void Execute(BattleSkillContext ctx)
      {
         BattleEnemyBullet.DespawnAllActive();
      }
   }
}
