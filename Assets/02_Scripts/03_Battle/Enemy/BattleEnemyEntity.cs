using DG.Tweening;
using TeaGame;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// 新版敌人实体（T2.1 2026-09-03 需求变更后；B1 2026-09-21 职责收口）。
   ///
   /// **本类只保留战斗事实与规则**：血量、元素与状态、受击结算、死亡、目标接口（锁敌）、生命周期，
   /// 以及把「移动 / 攻击 / 销毁前回调」委托给 <see cref="IEnemyBehaviour"/> 策略
   /// （策略由 <see cref="kind"/> 在 Awake 时经 <see cref="EnemyBehaviourFactory"/> 创建）。
   ///
   /// **表现已外移**（B1）：
   /// - 头顶 UI 创建与屏幕可见性投影 → <see cref="BattleEnemyViewBinder"/>；
   /// - 身体着色 / 伤害飘字 / 自爆范围环 → <see cref="BattleEnemyActor"/>。
   /// 两者均为纯 C# 类，实体只在 Awake 组装、在 Update/Notify 处转发；本类不再直接创建 GameObject、
   /// 不直接做相机投影、也不直接取 <see cref="BattleStateModel"/> 的相机与特效控制器。
   ///
   /// 规格：纯弹幕/爆炸伤害、固定姿态移动、无护盾/近战/召唤。
   /// </summary>
   public class BattleEnemyEntity : MonoBehaviour, IBattleEnemyTarget
   {
      [Header("基础配置")]
      [SerializeField] private DamageElement element = DamageElement.none;
      [SerializeField] private Transform lockPoint;
      [Tooltip("敌人头顶 UI prefab（EnemyEntity_UI，T2.3 QFramework View）。为空则不创建。")]
      [SerializeField] private GameObject uiPrefab;
      [SerializeField] private float uiYOffset = 1f;

      [Header("生命配置")]
      [SerializeField] private float baseHealth = 60f;

      [Header("弹幕攻击配置")]
      [Tooltip("攻击冷却随机区间（弹幕射击用）。")]
      [SerializeField] private float shotCooldownMin = 2f;
      [SerializeField] private float shotCooldownMax = 3.5f;
      [SerializeField] private GameObject enemyBulletPrefab;
      [SerializeField] private float enemyBulletSpeed = 6f;

      [Header("T2.1 敌人类型与行为参数（2026-09-03）")]
      [Tooltip("敌人类型：决定 Awake 创建的行为策略。")]
      [SerializeField] private EnemyKind kind = EnemyKind.Patrol;

      [Tooltip("移动速度（巡弋游走 / 导弹追踪用；炮台与地雷为 0）。")]
      [SerializeField] private float moveSpeed = 0f;

      [Tooltip("巡弋游走半径（以生成点为圆心）。")]
      [SerializeField] private float wanderRadius = 4f;

      [Tooltip("一次齐射弹幕数量（站桩炮台；1=单发）。")]
      [SerializeField] private int barrageCount = 1;

      [Tooltip("弹幕总散布角（度）。")]
      [SerializeField] private float barrageSpreadDeg = 0f;

      [Tooltip("爆炸相关半径：导弹=引爆距离(小)；地雷=感应与爆炸半径(大)。")]
      [SerializeField] private float blastRadius = 2f;

      [Tooltip("爆炸对玩家的伤害（导弹小爆/地雷大爆）。")]
      [SerializeField] private float blastDamage = 14f;

      [Header("自爆范围可视化（灰盒调试）")]
      [Tooltip("部署后常驻的感应/爆炸范围环（Mine prefab 子对象，无碰撞体）。")]
      [SerializeField] private Transform blastRangeRing;

      [Tooltip("爆炸瞬间范围提示环 prefab（对象池，Enemy_BlastRange）。")]
      [SerializeField] private GameObject blastRingPrefab;

      // ---------- T2.3 元素（敌人自身元素 + 状态表现） ----------

      /// <summary>当前挂载的元素状态组件（Apply 时创建）。</summary>
      private ElementStateEffect mElementState;

      /// <summary>当前生命值。</summary>
      private float mCurrentHealth;

      /// <summary>当前行为策略（Awake 依据 kind 创建；纯 C#，不序列化）。</summary>
      private IEnemyBehaviour mBehaviour;

      /// <summary>表现 Actor：身体着色 / 伤害飘字 / 自爆范围环（B1 外移）。</summary>
      private BattleEnemyActor mActor;

      /// <summary>头顶 UI 与可见性绑定器（B1 外移）。</summary>
      private BattleEnemyViewBinder mViewBinder;

      /// <summary>是否已进入销毁流程（防重复结算/二次爆炸）。</summary>
      private bool mDead;

      /// <summary>
      /// 是否由玩家击杀（T2.4）。
      /// 仅在受到外部伤害致死时置位，用于区分"被玩家击毁"与
      /// "地雷踩爆 / 导弹贴身引爆 / 导弹超时自毁"等非玩家击杀场景，
      /// 保证击杀能量只结算玩家击毁的敌人。
      /// </summary>
      private bool mKilledByPlayer;

      /// <inheritdoc />
      public Transform RootTransform => transform;

      /// <inheritdoc />
      public Transform LockPoint => lockPoint ? lockPoint : transform;

      /// <inheritdoc />
      /// <remarks>
      /// B1：视口可见性改由 <see cref="BattleEnemyViewBinder.IsOnCamera"/> 提供（每帧投影结果），
      /// 「组件启用 + GameObject 激活 + 在视口内」这条合成规则仍留在本类。
      /// </remarks>
      public bool CanBeLocked => isActiveAndEnabled && gameObject.activeInHierarchy
                                 && mViewBinder != null && mViewBinder.IsOnCamera;

      // ---------- 行为只读参数 ----------

      public EnemyKind Kind => kind;
      public float MoveSpeed => moveSpeed;
      public float WanderRadius => Mathf.Max(0.5f, wanderRadius);
      public float MaxHealth => Mathf.Max(1f, baseHealth);
      public float CurrentHealth => mCurrentHealth;
      public float HealthRatio => mCurrentHealth / MaxHealth;
      public float AttackCooldownMin => shotCooldownMin;
      public float AttackCooldownMax => shotCooldownMax;
      public int BarrageCount => barrageCount;
      public float BarrageSpreadDeg => barrageSpreadDeg;
      public float BlastRadius => Mathf.Max(0.1f, blastRadius);
      public float BlastDamage => Mathf.Max(0f, blastDamage);

      // ---------- T2.3 元素 / 状态 公开接口 ----------

      /// <summary>
      /// 敌人自身元素（克制判定用；可由生成器/StageConfig 设置）。
      /// </summary>
      public DamageElement Element
      {
         get => element;
         set
         {
            if (element == value) return;
            element = value;
            mActor?.SetElement(value);
            NotifyElementVisualChanged();
         }
      }

      /// <summary>受状态影响后的实际移动速度（冰减速；被麻痹为 0）。</summary>
      public float CurrentMoveSpeed
      {
         get
         {
            if (mElementState == null) return MoveSpeed;
            if (mElementState.IsStunned) return 0f;
            return MoveSpeed * mElementState.SpeedFactor;
         }
      }

      /// <summary>是否被电麻痹（移动与攻击停止）。</summary>
      public bool IsStunned => mElementState != null && mElementState.IsStunned;

      /// <summary>当前激活状态掩码（1冰/2火/4电/8风；0=无状态。头顶 UI 读取用）。</summary>
      public int ActiveStateMask => mElementState != null ? mElementState.ActiveMask : 0;

      /// <summary>状态组件挂载到宿主（ElementStateEffect.GetOrAdd 调用）。</summary>
      public void AttachElementState(ElementStateEffect effect)
      {
         mElementState = effect;
      }

      /// <summary>通知宿主刷新身体着色（状态增/减/到期）。</summary>
      public void NotifyElementVisualChanged()
      {
         if (mActor == null) return;

         if (mElementState != null)
         {
            mActor.RefreshVisual(mElementState.ActiveMask, element);
         }
         else
         {
            mActor.RefreshBaseBodyColor(element);
         }
      }

      /// <summary>
      /// 初始化敌人状态：填充生命并创建行为策略。
      /// </summary>
      private void Awake()
      {
         mCurrentHealth = Mathf.Max(1f, baseHealth);
         mBehaviour = EnemyBehaviourFactory.Create(kind);

         // B1：表现层组装（纯 C#，不新增 MonoBehaviour、不改 prefab 序列化字段）
         mActor = new BattleEnemyActor(this, blastRangeRing, blastRingPrefab);
         mViewBinder = new BattleEnemyViewBinder(this);

         mActor.CacheBodyRenderer();
         mActor.SyncBlastRangeRing(BlastRadius);
      }

      /// <summary>
      /// 注册敌人并创建头顶 UI。
      /// </summary>
      private void Start()
      {
         BattleEnemyRegistry.Register(this);
         mViewBinder.CreateHeadUi(uiPrefab, uiYOffset);
      }

      /// <summary>
      /// 注销敌人，避免锁敌列表中残留无效引用。
      /// </summary>
      private void OnDestroy()
      {
         BattleEnemyRegistry.UnRegister(this);
      }

      /// <summary>
      /// 每帧刷新视野检测、元素状态着色，并把移动/攻击委托给行为策略。
      /// 电麻痹期间不 tick 行为（移动与攻击停止）。
      /// </summary>
      private void Update()
      {
         mViewBinder.TickVisibility();

         if (mBehaviour == null) return;

         if (mElementState != null)
         {
            mActor.RefreshVisual(mElementState.ActiveMask, element);

            if (mElementState.IsStunned) return;
         }

         mBehaviour.TickMove(this);
         mBehaviour.TickAttack(this);
      }

      /// <inheritdoc />
      public void ApplyDamage(float damage)
      {
         ApplyElementalDamage(damage, DamageElement.none);
      }

      /// <summary>
      /// T2.3 元素伤害入口：克制倍率 + 风易伤 → 结算 → 施加元素状态。
      /// </summary>
      /// <param name="damage">原始伤害。</param>
      /// <param name="attackElement">攻击元素（none=无元素）。</param>
      /// <returns>实际结算伤害（含克制/易伤）。</returns>
      // [调试提醒 2026-09-06] 伤害构成存在待查怀疑（克制/易伤/元素状态/新旧子弹兼容），
      // 改动或调试本区域前请先读 Docs/12_伤害构成_怀疑存档.md 并向用户确认复现与期望值。
      public float ApplyElementalDamage(float damage, DamageElement attackElement)
      {
         var multiplier = BattleElementBalance.GetMultiplier(attackElement, Element);
         if (mElementState != null)
         {
            multiplier *= mElementState.IncomingMultiplier;
         }

         var finalDamage = Mathf.Max(0f, damage * multiplier);
         var isCounter = attackElement != DamageElement.none
                         && BattleElementBalance.IsCounter(attackElement, Element);

         var displayColor = attackElement == DamageElement.none
            ? Color.white
            : BattleElementBalance.GetElementColor(attackElement);

         ApplyDamageInternal(finalDamage, isCounter ? BattleElementBalance.CounterGold : displayColor, isCounter);

         // 命中附带元素 → 施加状态（冰/火/电/风）
         if (attackElement != DamageElement.none && mCurrentHealth > 0f)
         {
            ElementStateEffect.GetOrAdd(this).Apply(attackElement, finalDamage);
         }

         return finalDamage;
      }

      /// <summary>
      /// 直接伤害（DoT/电溅射等用，不再递归施加元素）。
      /// </summary>
      public void TakeDirectDamage(float amount, Color color)
      {
         ApplyDamageInternal(Mathf.Max(0f, amount), color, false);
      }

      private void ApplyDamageInternal(float amount, Color displayColor, bool counterBig)
      {
         mCurrentHealth -= amount;
         mCurrentHealth = Mathf.Max(0f, mCurrentHealth);

         ShowDamageText(Mathf.RoundToInt(amount), displayColor, counterBig);
         // B1：头顶 UI 的血条改由 Battle_EnemyUI 自读宿主 HealthRatio，实体不再反向推 View。

         if (mCurrentHealth <= 0f)
         {
            // 因受到外部伤害致死 → 视为玩家击杀（T2.4 能量结算依据）。
            // 自爆/自毁路径直接调用 Die()，不会走到这里，故不会误发击杀能量。
            mKilledByPlayer = true;
            Die();
         }
      }

      /// <summary>
      /// 向玩家方向发射一发敌人子弹（对象池，T1.3）。
      /// 行为按 <paramref name="yawOffsetDeg"/> 做水平偏转以形成弹幕。
      /// </summary>
      public void FireBulletAt(float yawOffsetDeg)
      {
         var player = BattlePlayerRegistry.CurrentTransform;
         if (!enemyBulletPrefab || !player) return;

         var bulletObject = BattleObjectPool.Spawn(enemyBulletPrefab, transform.position, Quaternion.identity);
         if (!bulletObject) return;

         bulletObject.transform.LookAt(player);
         if (Mathf.Abs(yawOffsetDeg) > 0.01f)
         {
            bulletObject.transform.Rotate(Vector3.up, yawOffsetDeg, Space.Self);
         }

         if (bulletObject.TryGetComponent(out Rigidbody rigidbody))
         {
            rigidbody.linearVelocity = bulletObject.transform.forward * enemyBulletSpeed;
         }
      }

      /// <summary>
      /// 若玩家处于自身周围 <paramref name="radius"/> 内，则对其造成 <paramref name="damage"/> 伤害。
      /// 供自爆·导弹 / 自爆·地雷引爆复用。
      /// </summary>
      public void DamagePlayerInRadius(float radius, float damage)
      {
         var player = BattlePlayerRegistry.CurrentTransform;
         if (!player) return;

         var sqrDistance = (player.position - transform.position).sqrMagnitude;
         if (sqrDistance > radius * radius) return;

         BattleArchitecture.Interface.SendEvent(new BattleEvent_PlayerDamaged
         {
            Damage = Mathf.Max(1, Mathf.RoundToInt(damage))
         });
      }

      /// <summary>
      /// 在自身位置生成伤害飘字（默认白字）。
      /// </summary>
      public void ShowDamageText(int amount)
      {
         ShowDamageText(amount, Color.white, false);
      }

      /// <summary>
      /// 生成伤害飘字（T2.3：元素色 / 克制金色放大）。
      /// 实现已外移到 <see cref="BattleEnemyActor"/>（B1）。
      /// </summary>
      public void ShowDamageText(int amount, Color color, bool big)
      {
         mActor?.ShowDamageText(amount, color, big);
      }

      /// <summary>
      /// 爆炸瞬间显示一次范围提示环（对象池，短暂后自动回池），供灰盒确认自爆伤害半径。
      /// 实现已外移到 <see cref="BattleEnemyActor"/>（B1）；本方法保留为对外入口
      /// （<see cref="EnemyBehaviour_Mine"/> / <see cref="EnemyBehaviour_Missile"/> 调用）。
      /// </summary>
      public void ShowBlastRing(float radius)
      {
         mActor?.ShowBlastRing(radius);
      }

      /// <summary>
      /// 死亡处理（唯一销毁入口）。
      /// 销毁前回调行为（地雷被击杀引爆），再销毁对象。
      /// 灰盒阶段不做敌人池（低频 Instantiate，T3.x 池化），已标注。
      /// </summary>
      public void Die()
      {
         if (mDead) return; // 防同帧重复销毁/二次爆炸
         mDead = true;

         if (mBehaviour != null)
         {
            mBehaviour.OnBeforeDestroy(this);
         }

         // 仅玩家击杀派发能量事件（T2.4）：行为自爆/超时自毁路径不置位该标记。
         if (mKilledByPlayer)
         {
            BattleArchitecture.Interface.SendEvent(new BattleEvent_EnemyKilled
            {
               Position = transform.position,
               Element = element
            });
            mKilledByPlayer = false;
         }

         Destroy(gameObject);
      }
   }
}
