using DG.Tweening;
using TeaFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// 新版敌人实体。
   /// 该组件负责敌人的可见性检测、受击、血量、开火以及注册到新版锁敌系统。
   /// </summary>
   public class BattleEnemyEntity : MonoBehaviour, IBattleEnemyTarget
   {
      [Header("基础配置")]
      [SerializeField] private DamageElement element = DamageElement.none;
      [SerializeField] private Transform lockPoint;
      [SerializeField] private EnemyUIData uiData;
      [SerializeField] private float uiYOffset = 1f;

      [Header("生命配置")]
      [SerializeField] private float baseHealth = 100f;

      [Header("攻击配置")]
      [SerializeField] private float shotCooldownMin = 3f;
      [SerializeField] private float shotCooldownMax = 5f;
      [SerializeField] private GameObject enemyBulletPrefab;
      [SerializeField] private float enemyBulletSpeed = 3f;

      /// <summary>
      /// 当前生命值。
      /// </summary>
      private float mCurrentHealth;

      /// <summary>
      /// 下一次攻击倒计时。
      /// </summary>
      private float mShotCooldown;

      /// <summary>
      /// 敌人头顶 UI 实例。
      /// </summary>
      private BattleEnemyUI mUiInstance;

      /// <summary>
      /// 当前是否在摄像机视野中。
      /// 锁敌系统会读取该值过滤掉镜头外目标。
      /// </summary>
      private bool mOnCamera;

      /// <inheritdoc />
      public Transform RootTransform => transform;

      /// <inheritdoc />
      public Transform LockPoint => lockPoint ? lockPoint : transform;

      /// <inheritdoc />
      public bool CanBeLocked => isActiveAndEnabled && gameObject.activeInHierarchy && mOnCamera;

      /// <summary>
      /// 初始化敌人状态。
      /// </summary>
      private void Awake()
      {
         mCurrentHealth = Mathf.Max(1f, baseHealth);
         mShotCooldown = Random.Range(shotCooldownMin, shotCooldownMax);
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
      /// 每帧刷新视野检测和攻击冷却。
      /// </summary>
      private void Update()
      {
         mOnCamera = IsVisibleInBattleCamera();
         TickShoot();
      }

      /// <inheritdoc />
      public void ApplyDamage(float damage)
      {
         mCurrentHealth -= damage;
         mCurrentHealth = Mathf.Max(0f, mCurrentHealth);

         if (BattleRuntimeBridge.EnvironmentEffectController)
         {
            BattleRuntimeBridge.EnvironmentEffectController.CreateDamageText(transform.position, Mathf.RoundToInt(damage));
         }

         if (mUiInstance)
         {
            mUiInstance.RefreshHealth(mCurrentHealth / Mathf.Max(1f, baseHealth));
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
      /// 刷新敌人射击逻辑。
      /// 子弹走对象池（T1.3），命中/超时由 BattleEnemyBullet 自回收。
      /// </summary>
      private void TickShoot()
      {
         if (!enemyBulletPrefab || !BattleRuntimeBridge.PlayerTransform) return;

         if (mShotCooldown > 0f)
         {
            mShotCooldown -= Time.deltaTime;
            return;
         }

         mShotCooldown = Random.Range(shotCooldownMin, shotCooldownMax);

         var bulletObject = BattleObjectPool.Spawn(enemyBulletPrefab, transform.position, Quaternion.identity);
         if (!bulletObject) return;

         bulletObject.transform.LookAt(BattleRuntimeBridge.PlayerTransform);

         if (bulletObject.TryGetComponent(out Rigidbody rigidbody))
         {
            rigidbody.velocity = bulletObject.transform.forward * enemyBulletSpeed;
         }
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
      /// 死亡处理。
      /// 当前先直接销毁对象，后续可以扩展为掉落、死亡动画或回收池逻辑。
      /// </summary>
      private void Die()
      {
         Destroy(gameObject);
      }
   }


}
