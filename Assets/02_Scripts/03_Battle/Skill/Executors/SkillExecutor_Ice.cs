namespace TeaGame
{
   /// <summary>
   /// 冰系执行器（控制型）。
   /// E：聚怪拉拢 → 接小型冰爆（让被聚敌人吃到冰元素并挂减速）；
   /// Q：更大范围聚怪 → 大冰爆团灭拉拢目标。
   /// 聚怪 + 爆发构成一次"先控后伤"的组合，展示执行器对公共功能的有序编排。
   /// </summary>
   public sealed class SkillExecutor_Ice : BattleRoleSkillExecutor
   {
      public override void CastElemental(BattleSkillContext ctx)
      {
         SkillEffect_PullEnemies.Execute(ctx);
         SkillEffect_AoEBurst.Execute(ctx);
      }

      public override void CastUltimate(BattleSkillContext ctx)
      {
         SkillEffect_PullEnemies.Execute(ctx);
         SkillEffect_AoEBurst.Execute(ctx);
      }
   }
}
