namespace TeaGame
{
   /// <summary>
   /// 火系执行器（爆发型）。
   /// E：范围火爆（AoEBurst）；
   /// Q：大范围核爆后接火雨弹幕，负责清场。
   /// </summary>
   public sealed class SkillExecutor_Fire : BattleRoleSkillExecutor
   {
      public override void CastElemental(BattleSkillContext ctx)
      {
         SkillEffect_AoEBurst.Execute(ctx);
      }

      public override void CastUltimate(BattleSkillContext ctx)
      {
         SkillEffect_AoEBurst.Execute(ctx);
         SkillEffect_ElementalBarrage.Execute(ctx);
      }
   }
}
