using System.Collections.Generic;
using System.Threading.Tasks;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 武器视图工厂。
   /// 该工厂负责根据 prefab 上实际挂载的组件，返回统一的武器视图适配器。
   /// 2026-09-19：旧版 `Weapon_ObjectComponent` 已退役（全部武器 prefab 都是 `Wep-Base` 的变体，
   /// 统一挂新版 <see cref="BattleWeaponObjectComponent"/>），故**不再保留回退分支**。
   /// </summary>
   public static class BattleWeaponViewFactory
   {
      /// <summary>
      /// 为武器实例创建视图适配器。
      /// 未挂 <see cref="BattleWeaponObjectComponent"/> 时返回 null 并告警——
      /// 此时 `BattleWeaponModeBase.WeaponView` 为空，开火会被 `PressTrigger` 首行守卫静默跳过。
      /// </summary>
      public static IBattleWeaponView Create(Transform weaponInstance)
      {
         if (!weaponInstance) return null;

         if (weaponInstance.TryGetComponent(out BattleWeaponObjectComponent battleComponent))
         {
            return new BattleWeaponViewAdapter(battleComponent);
         }

         Debug.LogWarning($"武器实例 {weaponInstance.name} 没有找到可用的武器表现组件（BattleWeaponObjectComponent）。");
         return null;
      }
   }

   /// <summary>
   /// 新版武器组件适配器。
   /// </summary>
   public sealed class BattleWeaponViewAdapter : IBattleWeaponView
   {
      private readonly BattleWeaponObjectComponent mComponent;

      public BattleWeaponViewAdapter(BattleWeaponObjectComponent component)
      {
         mComponent = component;
      }

      public Transform RootTransform => mComponent ? mComponent.transform : null;
      public Transform FirePoint => mComponent ? mComponent.ShowPoint : null;
      public GameObject BulletPrefab => mComponent ? mComponent.BulletPrefab : null;
      public GameObject BulletLinePrefab => mComponent ? mComponent.BulletLinePrefab : null;

      public void PlayMuzzleFire()
      {
         mComponent?.PlayMuzzleFire();
      }

      public void RefreshAim(Transform lookTarget, float lockSpeed)
      {
         mComponent?.RefreshAim(lookTarget, lockSpeed);
      }
   }

   /// <summary>
   /// 武器模式工厂。
   /// 根据武器子类型创建对应的开火逻辑对象。
   /// </summary>
   public static class BattleWeaponModeFactory
   {
      /// <summary>
      /// 创建武器模式实例。
      /// </summary>
      public static BattleWeaponModeBase Create(WeaponSubType weaponSubType)
      {
         switch (weaponSubType)
         {
            case WeaponSubType.assault_PreciseFrame: return new BattleWeaponMode_AssaultPreciseFrame();
            case WeaponSubType.assault_EnergyFrame: return new BattleWeaponMode_AssaultEnergyFrame();
            case WeaponSubType.assault_RapidFireFrame: return new BattleWeaponMode_AssaultRapidFireFrame();
            case WeaponSubType.assault_FocusFrame: return new BattleWeaponMode_AssaultFocusFrame();
            case WeaponSubType.assault_StormFrame: return new BattleWeaponMode_AssaultStormFrame();
            case WeaponSubType.heavy_TrackingMissile: return new BattleWeaponMode_HeavyTrackingMissile();
            case WeaponSubType.heavy_PowerfulMissile: return new BattleWeaponMode_HeavyPowerfulMissile();
            case WeaponSubType.heavy_LightweightMissile: return new BattleWeaponMode_HeavyLightweightMissile();
            case WeaponSubType.heavy_MultiLoadedMissile: return new BattleWeaponMode_HeavyMultiLoadedMissile();
            case WeaponSubType.scatter_SmashShot: return new BattleWeaponMode_ScatterSmashShot();
            case WeaponSubType.scatter_PrecisionShot: return new BattleWeaponMode_ScatterPrecisionShot();
            case WeaponSubType.sniper_Penetrating: return new BattleWeaponMode_SniperPenetrating();
            case WeaponSubType.sniper_Tandem: return new BattleWeaponMode_SniperTandem();
            case WeaponSubType.sniper_Swift: return new BattleWeaponMode_SniperSwift();
            case WeaponSubType.sniper_Charged: return new BattleWeaponMode_SniperCharged();
            default: return null;
         }
      }
   }

   /// <summary>
   /// 武器模式基类。
   /// 所有武器开火逻辑都从这里继承，以便统一管理射速、伤害、弹道生成与特效播放。
   /// </summary>
   [System.Serializable]
   public abstract class BattleWeaponModeBase
   {
      /// <summary>
      /// 武器表现层。
      /// </summary>
      protected IBattleWeaponView WeaponView;

      /// <summary>
      /// 武器原始配置。
      /// </summary>
      protected WeaponItem_Data WeaponData;

      /// <summary>
      /// 下一次允许开火的时间点。
      /// </summary>
      protected float CanFireTime;

      /// <summary>
      /// 自由瞄准发射基准旋转（未锁定时由武器系统写入相机旋转）。
      /// 为空（锁定吸附）时子弹按武器实际朝向（FirePoint）发射。
      /// 武器模型本身在未锁定时保持挂点回正（0,0,0），弹道方向由此基准旋转决定，
      /// 从而保证"子弹打向准星/视角正前"而武器视觉不歪向远处点。
      /// </summary>
      public Quaternion? FreeAimRotation { get; set; }

      /// <summary>
      /// 当前操控角色的元素，供子弹 prefab 与命中表现按元素取用。
      /// **由 <see cref="BattleWeaponSystem"/> 每帧写入**（同 <see cref="FreeAimRotation"/> 的写法），
      /// 取代原先的静态元素镜像——元素本来就能从
      /// `BattleStateModel.CurrentDamageElement` 推出，不需要再维护一份全局镜像。
      /// </summary>
      public DamageElement ActiveElement { get; set; } = DamageElement.none;

      /// <summary>
      /// 子弹飞行速度。
      /// 子类可以根据武器类型覆盖不同的默认值。
      /// </summary>
      protected virtual float BulletVelocity => 50f;

      /// <summary>
      /// 当前弹匣剩余子弹数。
      /// 每次开火消耗 1 发；空仓后进入换弹，换弹完成补满。
      /// </summary>
      public int CurrentMagazine { get; protected set; }

      /// <summary>
      /// 是否正在换弹。
      /// 换弹期间不响应开火；由武器系统每帧驱动进度。
      /// </summary>
      public bool IsReloading { get; protected set; }

      /// <summary>
      /// 换弹所需时长（秒）。
      /// 由武器系统从配置同步；T1.2 统一换弹时长，不做武器差异。
      /// </summary>
      public float ReloadDuration { get; set; } = 1.5f;

      /// <summary>
      /// 换弹完成的时间戳。
      /// </summary>
      private float mReloadEndTime;

      /// <summary>
      /// 弹匣容量上限（读武器配置）。
      /// </summary>
      public int MaxMagazine => Mathf.Max(0, WeaponData?.magazineSize ?? 0);

      /// <summary>
      /// 将武器模式与表现层及数据配置关联起来。
      /// 关联时同步把弹匣补满。
      /// </summary>
      public void Setup(IBattleWeaponView weaponView, WeaponItem_Data weaponData)
      {
         WeaponView = weaponView;
         WeaponData = weaponData;
         CurrentMagazine = MaxMagazine;
         IsReloading = false;
         mReloadEndTime = 0f;
      }

      /// <summary>
      /// 尝试执行一次开火。
      /// 该方法会先检查换弹状态、射速冷却与弹匣余量，
      /// 再触发枪口火焰和具体开火逻辑；空仓时自动进入换弹。
      /// </summary>
      public virtual void PressTrigger()
      {
         if (WeaponView == null || WeaponData == null) return;
         if (IsReloading) return;
         if (Time.time <= CanFireTime) return;

         if (CurrentMagazine <= 0)
         {
            StartReload();
            return;
         }

         var fireInterval = WeaponData.firingRate > 0 ? 60f / WeaponData.firingRate : 0.1f;
         CanFireTime = Time.time + fireInterval;

         CurrentMagazine--;
         WeaponView.PlayMuzzleFire();
         OnShootEvent();
      }

      /// <summary>
      /// 开始换弹（空仓自动或手动触发）。
      /// 弹匣已满或已在换弹时不重复触发。
      /// </summary>
      public virtual void StartReload()
      {
         if (IsReloading) return;
         if (CurrentMagazine >= MaxMagazine) return;

         IsReloading = true;
         mReloadEndTime = Time.time + Mathf.Max(0.05f, ReloadDuration);
      }

      /// <summary>
      /// 取消进行中的换弹，保持当前弹匣不变。
      /// 用于角色切换：切走时正在换弹的角色保持空弹匣（切回时仍为空）。
      /// </summary>
      public void CancelReload()
      {
         IsReloading = false;
         mReloadEndTime = 0f;
      }

      /// <summary>
      /// 切换角色时补满弹匣（原版核心规则）。
      /// 同时清除换弹残留状态。
      /// </summary>
      public virtual void OnRoleSwitched()
      {
         CancelReload();
         CurrentMagazine = MaxMagazine;
      }

      /// <summary>
      /// 每帧驱动换弹进度。
      /// 计时结束后补满弹匣并返回 true，由武器系统派发完成事件。
      /// </summary>
      public bool TickReload()
      {
         if (!IsReloading) return false;
         if (Time.time < mReloadEndTime) return false;

         IsReloading = false;
         CurrentMagazine = MaxMagazine;
         return true;
      }

      /// <summary>
      /// 换弹进度（0-1）。
      /// 未在换弹时返回 1；换弹中按剩余时间计算，供调试 UI 与后续 HUD 使用。
      /// </summary>
      public float ReloadProgress
      {
         get
         {
            if (!IsReloading) return 1f;
            var duration = Mathf.Max(0.05f, ReloadDuration);
            return Mathf.Clamp01(1f - (mReloadEndTime - Time.time) / duration);
         }
      }

      /// <summary>
      /// 由子类实现的具体开火逻辑。
      /// 默认行为为发射一发子弹。
      /// </summary>
      protected virtual void OnShootEvent()
      {
         FireSingleShot(0);
      }

      /// <summary>
      /// 生成一发物理子弹（T1.3：走对象池，无 Instantiate/Destroy 分配）。
      /// T2.3.1：子弹 prefab 按当前角色元素从 BattleElementBulletLibrary 解析（元素版子弹）。
      /// </summary>
      protected virtual void FireSingleShot(int index)
      {
         if (WeaponView?.FirePoint == null) return;

         var bulletPrefab = ResolvePlayerBulletPrefab();
         if (bulletPrefab == null) return;

         var baseRotation = FreeAimRotation ?? WeaponView.FirePoint.rotation;

         var bulletObject = SpawnBulletPrefab(
            bulletPrefab,
            WeaponView.FirePoint.position,
            baseRotation,
            WeaponData.damage,
            ActiveElement);

         if (!bulletObject) return;

         ApplyBulletSpread(bulletObject.transform, index);
         ApplyBulletVelocity(bulletObject.transform);
      }

      /// <summary>
      /// 解析本发子弹 prefab：当前角色元素对应的元素子弹优先，否则回退武器默认子弹。
      /// </summary>
      protected GameObject ResolvePlayerBulletPrefab()
      {
         var elementPrefab = BattleElementBulletLibrary.Resolve(ActiveElement);
         return elementPrefab ? elementPrefab : WeaponView.BulletPrefab;
      }

      /// <summary>
      /// 子弹预制体是否可池化（带 BattleBullet 组件）。
      /// 结果按 prefab 缓存，避免每次开火重复 GetComponent。
      /// </summary>
      private static readonly Dictionary<GameObject, bool> sPoolablePrefabCache =
         new Dictionary<GameObject, bool>(8);

      private static bool IsPoolableBulletPrefab(GameObject prefab)
      {
         if (sPoolablePrefabCache.TryGetValue(prefab, out var poolable))
         {
            return poolable;
         }

         poolable = prefab.GetComponent<BattleBullet>() != null;
         sPoolablePrefabCache[prefab] = poolable;
         return poolable;
      }

      /// <summary>
      /// 生成一发子弹并写入伤害。
      /// 可池化子弹（带 BattleBullet）走对象池并武装为新版结算；
      /// 纯旧版子弹（仅 Bullet_Base）保持原 Instantiate/Destroy 语义，保证旧路径零回归。
      /// </summary>
      protected GameObject SpawnBulletPrefab(
         GameObject bulletPrefab,
         Vector3 position,
         Quaternion rotation,
         float damage,
         TeaGame.DamageElement element,
         float lifeSeconds = 5f)
      {
         if (!bulletPrefab) return null;

         if (IsPoolableBulletPrefab(bulletPrefab))
         {
            var bulletObject = BattleObjectPool.Spawn(bulletPrefab, position, rotation);
            if (!bulletObject) return null;

            if (bulletObject.TryGetComponent(out BattleBullet battleBullet))
            {
               // 池化新路径只走 BattleBullet 结算：武装 + 超时自回（命中即回池）
               battleBullet.ResetBullet(damage, lifeSeconds, element);
            }

            //// 禁用同挂载的旧版 Bullet_Base，避免命中时双重结算/销毁
            //if (bulletObject.TryGetComponent(out Bullet_Base legacyBullet))
            //{
            //   legacyBullet.enabled = false;
            //}

            return bulletObject;
         }

         // 旧版子弹（无 BattleBullet）：保持原本的 Instantiate + Destroy 行为
         var legacyObject = Object.Instantiate(bulletPrefab, position, rotation);
         ApplyBulletDamage(legacyObject, damage);
         Object.Destroy(legacyObject, lifeSeconds);
         return legacyObject;
      }

      /// <summary>
      /// 为子弹施加散布。
      /// 默认实现使用稳定性数值决定随机偏转角度。
      /// </summary>
      protected virtual void ApplyBulletSpread(Transform bulletTransform, int index)
      {
         var clampedStability = Mathf.Clamp(WeaponData.stability, 0, 100);
         var maxDeviationAngle = (100f - clampedStability) * 0.05f;

         float RandomPowerDistribution(float maxValue)
         {
            var randomValue = Random.value;
            var power = 1 + clampedStability / 25f;
            var poweredRandom = Mathf.Pow(randomValue, power);
            return (Random.value > 0.5f ? 1f : -1f) * poweredRandom * maxValue;
         }

         var horizontalDeviation = RandomPowerDistribution(maxDeviationAngle);
         var verticalDeviation = RandomPowerDistribution(maxDeviationAngle);
         bulletTransform.Rotate(new Vector3(verticalDeviation, horizontalDeviation, 0f));
      }

      /// <summary>
      /// 为子弹设置刚体速度。
      /// </summary>
      protected virtual void ApplyBulletVelocity(Transform bulletTransform)
      {
         if (bulletTransform.TryGetComponent(out Rigidbody rigidbody))
         {
            rigidbody.velocity = bulletTransform.forward * BulletVelocity;
         }
      }

      /// <summary>
      /// 把伤害值写入新旧两套子弹脚本。
      /// 这样即使 prefab 还没有完全替换为新版组件，也能保持开火功能正常。
      /// </summary>
      protected virtual void ApplyBulletDamage(GameObject bulletObject, float damage)
      {
         if (bulletObject.TryGetComponent(out BattleBullet battleBullet))
         {
            battleBullet.Damage = damage;
         }

         //if (bulletObject.TryGetComponent(out Bullet_Base legacyBullet))
         //{
         //   legacyBullet.damage = Mathf.RoundToInt(damage);
         //}
      }
   }

   /// <summary>
   /// 突击型武器基类。
   /// </summary>
   public abstract class BattleWeaponMode_Assault : BattleWeaponModeBase
   {
   }

   /// <summary>
   /// 突击型武器：精确框架。
   /// 该模式会按照扩展参数执行多次延迟点射。
   /// </summary>
   public sealed class BattleWeaponMode_AssaultPreciseFrame : BattleWeaponMode_Assault
   {
      protected override void OnShootEvent()
      {
         ExecuteBurstAsync();
      }

      /// <summary>
      /// 异步执行连发点射。
      /// 这里沿用了旧版的简单异步实现方式，便于保持原本手感。
      /// </summary>
      private async void ExecuteBurstAsync()
      {
         var shotCount = Mathf.Max(1, WeaponData.expandValue01);
         var shotDelay = Mathf.Max(1, WeaponData.expandValue02);

         for (var i = 0; i < shotCount; i++)
         {
            FireSingleShot(i);
            await Task.Delay(shotDelay);
         }
      }
   }

   /// <summary>
   /// 突击型武器：能量框架。
   /// 当前沿用基础单发逻辑，后续可在此扩展蓄能或持续火力行为。
   /// </summary>
   public sealed class BattleWeaponMode_AssaultEnergyFrame : BattleWeaponMode_Assault
   {
   }

   /// <summary>
   /// 突击型武器：速射框架。
   /// 当前复用基础单发逻辑。
   /// </summary>
   public sealed class BattleWeaponMode_AssaultRapidFireFrame : BattleWeaponMode_Assault
   {
   }

   /// <summary>
   /// 突击型武器：专注框架。
   /// 当前复用基础单发逻辑。
   /// </summary>
   public sealed class BattleWeaponMode_AssaultFocusFrame : BattleWeaponMode_Assault
   {
   }

   /// <summary>
   /// 突击型武器：强攻框架。
   /// 当前复用基础单发逻辑。
   /// </summary>
   public sealed class BattleWeaponMode_AssaultStormFrame : BattleWeaponMode_Assault
   {
   }

   /// <summary>
   /// 重装型武器基类。
   /// 重装弹体通常更慢，因此覆盖了默认弹速。
   /// </summary>
   public abstract class BattleWeaponMode_Heavy : BattleWeaponModeBase
   {
      protected override float BulletVelocity => 20f;
   }

   /// <summary>重装型武器：追踪飞弹。</summary>
   public sealed class BattleWeaponMode_HeavyTrackingMissile : BattleWeaponMode_Heavy { }

   /// <summary>重装型武器：强力飞弹。</summary>
   public sealed class BattleWeaponMode_HeavyPowerfulMissile : BattleWeaponMode_Heavy { }

   /// <summary>重装型武器：轻质飞弹。</summary>
   public sealed class BattleWeaponMode_HeavyLightweightMissile : BattleWeaponMode_Heavy { }

   /// <summary>重装型武器：多联装飞弹。</summary>
   public sealed class BattleWeaponMode_HeavyMultiLoadedMissile : BattleWeaponMode_Heavy { }

   /// <summary>
   /// 散射型武器基类。
   /// 默认会根据扩展参数同时发射多发子弹。
   /// </summary>
   public abstract class BattleWeaponMode_Scatter : BattleWeaponModeBase
   {
      protected override void OnShootEvent()
      {
         var bulletCount = Mathf.Max(1, WeaponData.expandValue01);
         for (var i = 0; i < bulletCount; i++)
         {
            FireSingleShot(i);
         }
      }
   }

   /// <summary>散射型武器：重击霰弹。</summary>
   public sealed class BattleWeaponMode_ScatterSmashShot : BattleWeaponMode_Scatter { }

   /// <summary>
   /// 散射型武器：精确霰弹。
   /// 该模式不会随机散布，而是把子弹均匀铺在一个水平扇面内。
   /// </summary>
   public sealed class BattleWeaponMode_ScatterPrecisionShot : BattleWeaponMode_Scatter
   {
      private const float StabilityMultiplier = 0.25f;

      protected override void ApplyBulletSpread(Transform bulletTransform, int index)
      {
         var bulletCount = Mathf.Max(1, WeaponData.expandValue01);
         var totalAngle = WeaponData.stability * StabilityMultiplier;
         var anglePerBullet = bulletCount > 1 ? totalAngle / (bulletCount - 1) : 0f;
         var startAngle = -totalAngle / 2f;

         bulletTransform.Rotate(0f, startAngle + anglePerBullet * index, 0f);
      }
   }

   /// <summary>
   /// 狙击型武器基类。
   /// 狙击模式使用射线判定落点，并额外生成线性轨迹表现。
   /// </summary>
   public abstract class BattleWeaponMode_Sniper : BattleWeaponModeBase
   {
      protected override void FireSingleShot(int index)
      {
         if (WeaponView?.FirePoint == null) return;

         var fireForward = FreeAimRotation.HasValue
            ? FreeAimRotation.Value * Vector3.forward
            : WeaponView.FirePoint.forward;
         var ray = new Ray(WeaponView.FirePoint.position, fireForward);
         const float maxDistance = 100f;

         var hitPoint = ray.origin + ray.direction * maxDistance;
         if (Physics.Raycast(ray, out var hitInfo, maxDistance))
         {
            hitPoint = hitInfo.point;
         }

         // 命中点表现走对象池（T1.3）；T2.3.1 命中点子弹按角色元素取元素版表现
         SpawnBulletPrefab(
            ResolvePlayerBulletPrefab(),
            hitPoint,
            Quaternion.identity,
            WeaponData.damage,
            ActiveElement,
            5f);

         if (!WeaponView.BulletLinePrefab) return;

         var lineObject = BattleObjectPool.Spawn(
            WeaponView.BulletLinePrefab,
            WeaponView.FirePoint.position,
            WeaponView.FirePoint.rotation);

         if (lineObject && lineObject.TryGetComponent(out LineRenderer lineRenderer))
         {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, WeaponView.FirePoint.position);
            lineRenderer.SetPosition(1, hitPoint);
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;
         }

         if (lineObject)
         {
            BattleObjectPool.ReturnAfter(lineObject, 0.2f);
         }
      }
   }

   /// <summary>狙击型武器：穿透型。</summary>
   public sealed class BattleWeaponMode_SniperPenetrating : BattleWeaponMode_Sniper { }

   /// <summary>狙击型武器：连发型。</summary>
   public sealed class BattleWeaponMode_SniperTandem : BattleWeaponMode_Sniper { }

   /// <summary>狙击型武器：迅捷型。</summary>
   public sealed class BattleWeaponMode_SniperSwift : BattleWeaponMode_Sniper { }

   /// <summary>狙击型武器：充能型。</summary>
   public sealed class BattleWeaponMode_SniperCharged : BattleWeaponMode_Sniper { }
}
