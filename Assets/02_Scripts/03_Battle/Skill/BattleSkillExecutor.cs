using TeaGame;

namespace TeaGame
{
   /// <summary>
   /// T2.4 角色技能执行器（三层模型第二层：差异层）。
   /// 一个元素/角色对应一个执行器，负责把"本角色 E/Q 的行为差异"表达为
   /// 对公共功能效果（SkillEffect_*）的编排：选哪些效果、以什么顺序、复用同一数值档案。
   /// 执行器无状态：技能系统每次施放经工厂创建或复用实例均可（本阶段每次创建，量级极低）。
   /// </summary>
   public abstract class BattleRoleSkillExecutor
   {
      /// <summary>施放元素战技（E 键）。</summary>
      public abstract void CastElemental(BattleSkillContext ctx);

      /// <summary>施放极奏（Q 键，已扣能量、已进冷却）。</summary>
      public abstract void CastUltimate(BattleSkillContext ctx);
   }

   /// <summary>
   /// 执行器工厂：按角色元素解析差异层实例。
   /// 未收录的元素返回 null，技能系统将禁用该角色的 E/Q（静默跳过）。
   /// </summary>
   public static class BattleSkillExecutorFactory
   {
      public static BattleRoleSkillExecutor Create(DamageElement element)
      {
         switch (element)
         {
            case DamageElement.Fire: return new SkillExecutor_Fire();
            case DamageElement.Ice: return new SkillExecutor_Ice();
            case DamageElement.Wind: return new SkillExecutor_Wind();
            case DamageElement.Electric: return new SkillExecutor_Electric();
            default: return null;
         }
      }
   }
}
