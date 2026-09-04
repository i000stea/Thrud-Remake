using System.Collections.Generic;
using TeaFramework;

namespace TeaGame
{
   /// <summary>
   /// T2.4 技能槽位。
   /// Elemental = 元素战技（E 键），Ultimate = 极奏（Q 键，消耗能量）。
   /// 槽位数值与 <see cref="BattleSkillCooldownChangedEvent.Slot"/>（0/1）保持一致。
   /// </summary>
   public enum BattleSkillSlot
   {
      Elemental = 0,
      Ultimate = 1
   }

   /// <summary>
   /// 单个技能槽的数值行（E / Q 各持一份）。
   /// 采用"数据优先"：执行器与公共效果只读取本行参数，不内置魔法数；
   /// 数值集中在 <see cref="BattleSkillCatalog"/> 目录中便于调平。
   /// 灰盒阶段不同效果复用同一组字段（含义按效果解释），后续可迁移为 ScriptableObject 资产。
   /// </summary>
   [System.Serializable]
   public sealed class BattleSkillSlotData
   {
      /// <summary>冷却时长（秒）。</summary>
      public float Cooldown = 3.5f;

      /// <summary>能量消耗（E 通常为 0；Q 消耗极奏能量）。</summary>
      public float EnergyCost;

      // —— 范围爆发（AoEBurst）——
      /// <summary>爆发半径。</summary>
      public float BurstRadius = 6f;

      /// <summary>爆发结算伤害（乘元素克制/状态）。</summary>
      public float BurstDamage = 26f;

      // —— 元素弹幕（ElementalBarrage）——
      /// <summary>弹幕子弹数量。</summary>
      public int ProjectileCount = 8;

      /// <summary>弹幕水平总散布角（度）。</summary>
      public float SpreadDeg = 45f;

      /// <summary>弹幕单发伤害。</summary>
      public float ProjectileDamage = 6f;

      /// <summary>弹幕子弹寿命（秒，超时自回）。</summary>
      public float ProjectileLife = 4f;

      // —— 聚怪（PullEnemies）——
      /// <summary>聚怪作用半径。</summary>
      public float PullRadius = 9f;

      /// <summary>聚怪保留距离比例（0.3 = 拉至中心与外圈的 30% 处）。</summary>
      public float PullKeepRatio = 0.3f;
   }

   /// <summary>
   /// 单个角色的技能档案（数据优先；目录中每个元素一行）。
   /// 档案只描述"数值与成本"，具体调用哪些公共功能、以何种顺序组合，
   /// 由对应角色的执行器（三层模型中的差异层）决定。
   /// </summary>
   public sealed class BattleRoleSkillProfile
   {
      /// <summary>所属角色元素（档案检索键）。</summary>
      public DamageElement Element;

      /// <summary>元素战技（E）数值。</summary>
      public BattleSkillSlotData Elemental = new();

      /// <summary>极奏（Q）数值。</summary>
      public BattleSkillSlotData Ultimate = new();

      /// <summary>按槽取数值行。</summary>
      public BattleSkillSlotData GetSlot(BattleSkillSlot slot)
      {
         return slot == BattleSkillSlot.Ultimate ? Ultimate : Elemental;
      }
   }

   /// <summary>
   /// T2.4 技能数值目录（数据优先）。
   /// demo 四元素角色各一行，按角色元素检索；灰盒阶段无美术/资产负担，
   /// 后续若需策划在编辑器调参，可整体迁移为 ScriptableObject 资产表。
   /// </summary>
   public static class BattleSkillCatalog
   {
      /// <summary>命中敌人获得的能量（全局共享能量槽）。</summary>
      public const float EnergyGainPerHit = 2.5f;

      /// <summary>玩家击杀敌人获得的能量。</summary>
      public const float EnergyGainPerKill = 12f;

      /// <summary>Q 极奏能量消耗（各档案 Ultimate.EnergyCost 引用值）。</summary>
      public const float UltimateEnergyCost = 50f;

      private static readonly Dictionary<DamageElement, BattleRoleSkillProfile> sProfiles =
         new Dictionary<DamageElement, BattleRoleSkillProfile>
         {
            // 火：爆发型。E=范围爆发；Q=清场核爆 + 火雨弹幕。
            {
               DamageElement.Fire,
               new BattleRoleSkillProfile
               {
                  Element = DamageElement.Fire,
                  Elemental = new BattleSkillSlotData
                  {
                     Cooldown = 3f,
                     BurstRadius = 5.5f,
                     BurstDamage = 30f
                  },
                  Ultimate = new BattleSkillSlotData
                  {
                     Cooldown = 14f,
                     EnergyCost = UltimateEnergyCost,
                     BurstRadius = 9f,
                     BurstDamage = 60f,
                     ProjectileCount = 10,
                     SpreadDeg = 60f,
                     ProjectileDamage = 8f,
                     ProjectileLife = 4f
                  }
               }
            },

            // 冰：控制型。E=聚怪后接小型冰爆（挂冻结减速）；Q=大范围聚怪 + 冰爆。
            {
               DamageElement.Ice,
               new BattleRoleSkillProfile
               {
                  Element = DamageElement.Ice,
                  Elemental = new BattleSkillSlotData
                  {
                     Cooldown = 4f,
                     PullRadius = 9f,
                     PullKeepRatio = 0.3f,
                     BurstRadius = 3f,
                     BurstDamage = 10f
                  },
                  Ultimate = new BattleSkillSlotData
                  {
                     Cooldown = 15f,
                     EnergyCost = UltimateEnergyCost,
                     PullRadius = 11f,
                     PullKeepRatio = 0.22f,
                     BurstRadius = 8f,
                     BurstDamage = 55f
                  }
               }
            },

            // 风：弹幕/清屏型。E=清弹幕 + 风刃扇面；Q=清弹幕 + 大扇面风刃 + 风爆。
            {
               DamageElement.Wind,
               new BattleRoleSkillProfile
               {
                  Element = DamageElement.Wind,
                  Elemental = new BattleSkillSlotData
                  {
                     Cooldown = 4f,
                     ProjectileCount = 10,
                     SpreadDeg = 70f,
                     ProjectileDamage = 7f,
                     ProjectileLife = 4f
                  },
                  Ultimate = new BattleSkillSlotData
                  {
                     Cooldown = 15f,
                     EnergyCost = UltimateEnergyCost,
                     ProjectileCount = 16,
                     SpreadDeg = 100f,
                     ProjectileDamage = 9f,
                     ProjectileLife = 5f,
                     BurstRadius = 6f,
                     BurstDamage = 30f
                  }
               }
            },

            // 电：速射/链式。E=窄扇面高频弹幕（单点压制）；Q=大范围落雷 + 宽雷弹幕。
            {
               DamageElement.Electric,
               new BattleRoleSkillProfile
               {
                  Element = DamageElement.Electric,
                  Elemental = new BattleSkillSlotData
                  {
                     Cooldown = 3f,
                     ProjectileCount = 6,
                     SpreadDeg = 12f,
                     ProjectileDamage = 9f,
                     ProjectileLife = 3f,
                     BurstRadius = 2.5f,
                     BurstDamage = 8f
                  },
                  Ultimate = new BattleSkillSlotData
                  {
                     Cooldown = 15f,
                     EnergyCost = UltimateEnergyCost,
                     ProjectileCount = 14,
                     SpreadDeg = 40f,
                     ProjectileDamage = 10f,
                     ProjectileLife = 4f,
                     BurstRadius = 8f,
                     BurstDamage = 60f
                  }
               }
            }
         };

      /// <summary>
      /// 按角色元素取技能档案。
      /// 无元素 / 未收录的角色返回 null，技能系统将禁用其 E/Q。
      /// </summary>
      public static BattleRoleSkillProfile Get(DamageElement element)
      {
         return sProfiles.TryGetValue(element, out var profile) ? profile : null;
      }
   }
}
