using DG.Tweening;
using TeaFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// 新版敌人实体（T2.1 2026-09-03 需求变更后）。
   /// 该组件负责敌人的可见性检测、受击、血量、UI、注册到锁敌系统，
   /// 以及把「移动 / 攻击 / 销毁前回调」委托给 <see cref="IEnemyBehaviour"/> 策略。
   /// 策略由 <see cref="kind"/> 在 Awake 时经 <see cref="EnemyBehaviourFactory"/> 创建。
   /// 规格：纯弹幕/爆炸伤害、固定姿态移动、无护盾/近战/召唤。
   /// </summary>
   public class BattleEnemyEntity : MonoBehaviour, IBattleEnemyTarget
   {
      [Header("基础配置")]
      [SerializeField] private DamageElement element = DamageElement.none;
      [SerializeField] private Transform lockPoint;
      [SerializeField] private EnemyUIData uiData;
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

      /// <summary>当前生命值。</summary>
      private float mCurrentHealth;

      /// <summary>当前行为策略（Awake 依据 kind 创建；纯 C#，不序列化）。</summary>
      private IEnemyBehaviour mBehaviour;

      /// <summary>敌人头顶 UI 实例。</summary>
      private BattleEnemyUI mUiInstance;

      /// <summary>当前是否在摄像机视野中（锁敌过滤）。</summary>
      private bool mOnCamera;

      /// <summary>是否已进入销毁流程（防重复结算/二次爆炸）。</summary>
      private bool mDead;

      /// <inheritdoc />
      public Transform RootTransform => transform;

      /// <inheritdoc />
      public Transform LockPoint => lockPoint ? lockPoint : transform;

      /// <inheritdoc />
      public bool CanBeLocked => isActiveAndEnabled && gameObject.activeInHierarchy && mOnCamera;

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

      /// <summary>
      /// 初始化敌人状态：填充生命并创建行为策略。
      /// </summary>
      private void Awake()
      {
         mCurrentHealth = Mathf.Max(1f, baseHealth);
         mBehaviour = EnemyBehaviourFactory.Create(kind);
         SyncBlastRangeRing();
      }

      /// <summary>
      /// 注册敌人并创建 UI。
      /// </summary>
      private void Start()
      {
         BattleEnemyRegistry.Register(this);
         CreateUiIfNeeded();
      }

      /// <summary>
      /// 注销敌人，避免锁敌列表中残留无效引用。
      /// </summary>
      private void OnDestroy()
      {
         BattleEnemyRegistry.UnRegister(this);
      }

      /// <summary>
      /// 每帧刷新视野检测，并把移动/攻击委托给行为策略。
      /// </summary>
      private void Update()
      {
         mOnCamera = IsVisibleInBattleCamera();

         if (mBehaviour == null) return;
         mBehaviour.TickMove(this);
         mBehaviour.TickAttack(this);
      }

      /// <inheritdoc />
      public void ApplyDamage(float damage)
      {
         mCurrentHealth -= damage;
         mCurrentHealth = Mathf.Max(0f, mCurrentHealth);

         ShowDamageText(Mathf.RoundToInt(damage));

         if (mUiInstance)
         {
            mUiInstance.RefreshHealth(HealthRatio);
         }

         if (mCurrentHealth <= 0f)
         {
            Die();
         }
      }

      /// <summary>
      /// 判断敌人是否位于当前战斗相机的可视范围内。
      /// </summary>
      private bool IsVisibleInBattleCamera()
      {
         var camera = BattleRuntimeBridge.Camera;
         if (!camera || !LockPoint) return false;

         var screenPoint = camera.WorldToViewportPoint(LockPoint.position);
         return screenPoint.z > 0f
                && screenPoint.x >= 0f && screenPoint.x <= 1f
                && screenPoint.y >= 0f && screenPoint.y <= 1f;
      }

      /// <summary>
      /// 向玩家方向发射一发敌人子弹（对象池，T1.3）。
      /// 行为按 <paramref name="yawOffsetDeg"/> 做水平偏转以形成弹幕。
      /// </summary>
      public void FireBulletAt(float yawOffsetDeg)
      {
         var player = BattleRuntimeBridge.PlayerTransform;
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
            rigidbody.velocity = bulletObject.transform.forward * enemyBulletSpeed;
         }
      }

      /// <summary>
      /// 若玩家处于自身周围 <paramref name="radius"/> 内，则对其造成 <paramref name="damage"/> 伤害。
      /// 供自爆·导弹 / 自爆·地雷引爆复用。
      /// </summary>
      public void DamagePlayerInRadius(float radius, float damage)
      {
         var player = BattleRuntimeBridge.PlayerTransform;
         if (!player) return;

         var sqrDistance = (player.position - transform.position).sqrMagnitude;
         if (sqrDistance > radius * radius) return;

         BattleArchitecture.Interface.SendEvent(new BattlePlayerDamagedEvent
         {
            Damage = Mathf.Max(1, Mathf.RoundToInt(damage))
         });
      }

      /// <summary>
      /// 在自身位置生成伤害飘字。
      /// </summary>
      public void ShowDamageText(int amount)
      {
         var controller = BattleRuntimeBridge.EnvironmentEffectController;
         if (!controller) return;
         controller.CreateDamageText(transform.position, amount);
      }

      /// <summary>
      /// 爆炸范围环的基准面尺寸约定：内置 Plane 网格原始尺寸为 10×10，
      /// 故半径 <paramref name="radius"/> 对应 x/z 缩放 = radius*2/10。
      /// </summary>
      private const float RangeRingPlaneSize = 10f;

      /// <summary>
      /// 把常驻范围环（子对象，Mine 感应圈）的直径同步为当前 BlastRadius。
      /// 在 Awake 调用一次；Mine prefab 子环使用 Plane 网格。
      /// </summary>
      private void SyncBlastRangeRing()
      {
         if (!blastRangeRing) return;

         var diameter = BlastRadius * 2f;
         blastRangeRing.localScale = new Vector3(diameter / RangeRingPlaneSize, 1f, diameter / RangeRingPlaneSize);
      }

      /// <summary>
      /// 爆炸瞬间显示一次范围提示环（对象池，短暂后自动回池），供灰盒确认自爆伤害半径。
      /// 环贴地面（y=0.02）展开。
      /// </summary>
      public void ShowBlastRing(float radius)
      {
         if (!blastRingPrefab) return;

         var position = new Vector3(transform.position.x, 0.02f, transform.position.z);
         var ring = BattleObjectPool.Spawn(blastRingPrefab, position, Quaternion.identity);
         if (!ring) return;

         var diameter = Mathf.Max(0.2f, radius) * 2f;
         ring.transform.localScale = new Vector3(diameter / RangeRingPlaneSize, 1f, diameter / RangeRingPlaneSize);
         BattleObjectPool.ReturnAfter(ring, 0.55f);
      }

      /// <summary>
      /// 根据配置创建敌人 UI。
      /// 若缺少资源配置则直接跳过，不影响敌人基础行为。
      /// </summary>
      private void CreateUiIfNeeded()
      {
         if (!uiData || !uiData.UIPrefab) return;

         var uiObject = Instantiate(uiData.UIPrefab, transform).transform;
         uiObject.localPosition = Vector3.up * uiYOffset;

         if (!uiObject.TryGetComponent(out mUiInstance))
         {
            Debug.LogWarning($"敌人 {name} 生成的 UI 预制体上缺少 BattleEnemyUI 组件。");
            return;
         }

         mUiInstance.Setup(GetElementData());
      }

      /// <summary>
      /// 根据敌人元素读取对应的 UI 展示资源。
      /// </summary>
      private EnemyUIData_Element GetElementData()
      {
         if (!uiData) return null;

         switch (element)
         {
            case DamageElement.Ice: return uiData.uiIData_Ice;
            case DamageElement.Fire: return uiData.uiIData_Fire;
            case DamageElement.Wind: return uiData.uiIData_Wind;
            case DamageElement.Electric: return uiData.uiIData_Electric;
            default: return uiData.uiIData_None;
         }
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

         Destroy(gameObject);
      }
   }
}
