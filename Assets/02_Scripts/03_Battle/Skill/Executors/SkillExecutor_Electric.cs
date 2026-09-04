namespace TeaGame
{
   /// <summary>
   /// 电系执行器（速射 / 单体压制型）。
   /// E：窄扇面高频雷击弹幕（对锁定目标快速叠电，配合武器连射）；
   /// Q：落雷范围爆发 + 更宽的雷幕弹幕收割。
   /// 展示同一"元素弹幕"功能在不同角色手中以不同散布/弹数/伤害呈现差异。
   /// </summary>
   public sealed class SkillExecutor_Electric : BattleRoleSkillExecutor
   {
      public override void CastElemental(BattleSkillContext ctx)
      {
         SkillEffect_ElementalBarrage.Execute(ctx);
      }

      public override void CastUltimate(BattleSkillContext ctx)
      {
         SkillEffect_AoEBurst.Execute(ctx);
         SkillEffect_ElementalBarrage.Execute(ctx);
      }
   }
}
