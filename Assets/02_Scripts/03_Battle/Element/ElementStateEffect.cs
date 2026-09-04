using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.3 元素状态效果组件（挂在受击敌人身上，独立文件）。
   /// 四元素状态可并行叠加：冰=减速、火=DoT、电=麻痹+溅射、风=易伤+一次性聚怪。
   /// 移动减速/麻痹由宿主 BattleEnemyEntity 每帧读取；DoT/溅射经宿主 TakeDirectDamage 结算（不再递归施加元素）。
   /// </summary>
   public class ElementStateEffect : MonoBehaviour
   {
      /// <summary>宿主敌人（Awake/Apply 时绑定）。</summary>
      private BattleEnemyEntity mHost;

      // ---- 状态槽（数值见 BattleElementBalance） ----
      private float mIceRemain;
      private int mIceStacks;

      private float mFireRemain;
      private float mFireTickTimer;

      private float mElectricRemain;
      private float mWindRemain;

      /// <summary>当前被麻痹（电）期间禁止移动与攻击。</summary>
      public bool IsStunned => mElectricRemain > 0f;

      /// <summary>冰减速系数。</summary>
      public float SpeedFactor
      {
         get
         {
            if (mIceRemain <= 0f) return 1f;
            return mIceStacks >= BattleElementBalance.IceMaxStack
               ? BattleElementBalance.IceSlowFactorStack
               : BattleElementBalance.IceSlowFactor;
         }
      }

      /// <summary>风易伤系数（对宿主造成的伤害额外放大）。</summary>
      public float IncomingMultiplier => mWindRemain > 0f ? 1f + BattleElementBalance.WindVulnerableRatio : 1f;

      /// <summary>是否有任意状态激活。</summary>
      public bool HasAnyState => mIceRemain > 0f || mFireRemain > 0f || mElectricRemain > 0f || mWindRemain > 0f;

      /// <summary>当前激活状态掩码（冰1/火2/电4/风8；宿主身体着色用）。</summary>
      public int ActiveMask =>
         (mIceRemain > 0f ? 1 : 0)
         | (mFireRemain > 0f ? 2 : 0)
         | (mElectricRemain > 0f ? 4 : 0)
         | (mWindRemain > 0f ? 8 : 0);

      /// <summary>
      /// 取或创建宿主身上的状态组件并绑定宿主。
      /// </summary>
      public static ElementStateEffect GetOrAdd(BattleEnemyEntity host)
      {
         var effect = host.GetComponent<ElementStateEffect>();
         if (effect == null)
         {
            effect = host.gameObject.AddComponent<ElementStateEffect>();
         }

         if (effect.mHost == null)
         {
            effect.mHost = host;
            host.AttachElementState(effect);
         }

         return effect;
      }

      /// <summary>
      /// 施加一次元素命中（由宿主 ApplyElementalDamage 调用）。
      /// </summary>
      /// <param name="element">攻击元素（none 不入此函数）。</param>
      /// <param name="hitDamage">本次实际命中伤害（电溅射/风聚怪使用）。</param>
      public void Apply(DamageElement element, float hitDamage)
      {
         if (mHost == null)
         {
            mHost = GetComponent<BattleEnemyEntity>();
            if (mHost) mHost.AttachElementState(this);
         }

         switch (element)
         {
            case DamageElement.Ice:
               mIceStacks = Mathf.Min(BattleElementBalance.IceMaxStack, mIceStacks + 1);
               mIceRemain = BattleElementBalance.IceDuration;
               break;

            case DamageElement.Fire:
               mFireRemain = BattleElementBalance.FireDuration;
               break;

            case DamageElement.Electric:
               mElectricRemain = BattleElementBalance.ElectricStunDuration;
               ChainSplash(hitDamage);
               break;

            case DamageElement.Wind:
               mWindRemain = BattleElementBalance.WindDuration;
               PullNeighbors();
               break;
         }

         mHost?.NotifyElementVisualChanged();
      }

      private void Update()
      {
         if (mHost == null) return;

         var dirty = false;
         var dt = Time.deltaTime;

         if (mIceRemain > 0f)
         {
            mIceRemain -= dt;
            if (mIceRemain <= 0f)
            {
               mIceRemain = 0f;
               mIceStacks = 0;
               dirty = true;
            }
         }

         if (mFireRemain > 0f)
         {
            mFireRemain -= dt;
            mFireTickTimer -= dt;
            if (mFireTickTimer <= 0f)
            {
               mFireTickTimer = BattleElementBalance.FireDotInterval;
               if (mFireRemain > 0f && mHost)
               {
                  mHost.TakeDirectDamage(BattleElementBalance.FireDotDamage,
                     BattleElementBalance.GetElementColor(DamageElement.Fire));
               }
            }

            if (mFireRemain <= 0f)
            {
               mFireRemain = 0f;
               dirty = true;
            }
         }

         if (mElectricRemain > 0f)
         {
            mElectricRemain -= dt;
            if (mElectricRemain <= 0f)
            {
               mElectricRemain = 0f;
               dirty = true;
            }
         }

         if (mWindRemain > 0f)
         {
            mWindRemain -= dt;
            if (mWindRemain <= 0f)
            {
               mWindRemain = 0f;
               dirty = true;
            }
         }

         if (dirty)
         {
            mHost.NotifyElementVisualChanged();
         }
      }

      /// <summary>
      /// 电溅射：把本次命中的一部分伤害传导给周围其它敌人（不重复施加状态）。
      /// </summary>
      private void ChainSplash(float hitDamage)
      {
         if (mHost == null || hitDamage <= 0f) return;

         var splashDamage = hitDamage * BattleElementBalance.ElectricChainRatio;
         var others = Object.FindObjectsOfType<BattleEnemyEntity>();
         for (var i = 0; i < others.Length; i++)
         {
            var other = others[i];
            if (other == null || other == mHost) continue;

            var sqr = (other.transform.position - mHost.transform.position).sqrMagnitude;
            if (sqr > BattleElementBalance.ElectricChainRadius * BattleElementBalance.ElectricChainRadius) continue;

            other.TakeDirectDamage(splashDamage, BattleElementBalance.GetElementColor(DamageElement.Electric));
         }
      }

      /// <summary>
      /// 风聚怪：把周围其它敌人向被命中宿主拉近一小段（一次性视觉表现）。
      /// </summary>
      private void PullNeighbors()
      {
         if (mHost == null) return;

         var radius = BattleElementBalance.WindPullRadius;
         var pull = BattleElementBalance.WindPullDistance;
         var others = Object.FindObjectsOfType<BattleEnemyEntity>();
         for (var i = 0; i < others.Length; i++)
         {
            var other = others[i];
            if (other == null || other == mHost) continue;

            var toHost = mHost.transform.position - other.transform.position;
            var distance = toHost.magnitude;
            if (distance <= 0.01f || distance > radius) continue;

            other.transform.position += toHost / distance * pull;
         }
      }
   }
}
