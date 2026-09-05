using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.3 元素数值与规则配置（集中调参点）。
   /// 克制环：Electric → Ice → Fire → Wind → Electric（1.25×）。
   /// 状态：冰=减速、火=DoT、电=麻痹+溅射、风=聚怪+易伤（2026-09-03 可视化方案确认）。
   /// </summary>
   public static class BattleElementBalance
   {
      /// <summary>克制伤害倍率。</summary>
      public const float CounterMultiplier = 1.25f;

      // ---- 冰：减速 ----
      public const float IceSlowFactor = 0.7f;      // 1 层减速 30%
      public const float IceSlowFactorStack = 0.5f; // 2 层减速 50%
      public const float IceDuration = 3f;
      public const int IceMaxStack = 2;

      // ---- 火：DoT ----
      public const float FireDotInterval = 0.5f;
      public const int FireDotDamage = 2;
      public const float FireDuration = 3f;

      // ---- 电：麻痹 + 溅射 ----
      public const float ElectricStunDuration = 1.2f;
      public const float ElectricChainRadius = 4f;
      public const float ElectricChainRatio = 0.5f; // 溅射为本次命中伤害的 50%

      // ---- 风：聚怪 + 易伤 ----
      public const float WindPullRadius = 8f;
      public const float WindPullDistance = 2.5f;   // 范围内其它敌人被拉近 2.5m
      public const float WindVulnerableRatio = 0.1f; // 受击 +10%
      public const float WindDuration = 3f;

      /// <summary>攻击元素是否克制防御元素。</summary>
      public static bool IsCounter(DamageElement attack, DamageElement defense)
      {
         switch (defense)
         {
            case DamageElement.Ice: return attack == DamageElement.Electric;
            case DamageElement.Fire: return attack == DamageElement.Ice;
            case DamageElement.Wind: return attack == DamageElement.Fire;
            case DamageElement.Electric: return attack == DamageElement.Wind;
            default: return false;
         }
      }

      /// <summary>克制倍率（无元素/非克制为 1）。</summary>
      public static float GetMultiplier(DamageElement attack, DamageElement defense)
      {
         return IsCounter(attack, defense) ? CounterMultiplier : 1f;
      }

      /// <summary>元素代表色（表现用：飘字/身体着色/UI）。</summary>
      public static Color GetElementColor(DamageElement element)
      {
         switch (element)
         {
            case DamageElement.Ice: return new Color(0.45f, 0.85f, 1f, 1f);
            case DamageElement.Fire: return new Color(1f, 0.45f, 0.2f, 1f);
            case DamageElement.Wind: return new Color(0.4f, 1f, 0.6f, 1f);
            case DamageElement.Electric: return new Color(0.75f, 0.45f, 1f, 1f);
            default: return Color.white;
         }
      }

      /// <summary>克制命中飘字金色。</summary>
      public static readonly Color CounterGold = new Color(1f, 0.78f, 0.12f, 1f);
   }
}
