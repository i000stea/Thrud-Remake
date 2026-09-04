namespace TeaGame
{
   /// <summary>
   /// 风系执行器（弹幕 / 应对型）。
   /// E：风场吹散场上敌人弹幕 → 风刃扇面齐射；
   /// Q：再吹散（更大窗口）→ 大扇面风刃 → 中心风爆收尾。
   /// 展示"清弹幕"与"元素弹幕/范围爆发"两个公共功能在同角色内的组合。
   /// </summary>
   public sealed class SkillExecutor_Wind : BattleRoleSkillExecutor
   {
      public override void CastElemental(BattleSkillContext ctx)
      {
         SkillEffect_ClearEnemyBullets.Execute(ctx);
         SkillEffect_ElementalBarrage.Execute(ctx);
      }

      public override void CastUltimate(BattleSkillContext ctx)
      {
         SkillEffect_ClearEnemyBullets.Execute(ctx);
         SkillEffect_ElementalBarrage.Execute(ctx);
         SkillEffect_AoEBurst.Execute(ctx);
      }
   }
}
