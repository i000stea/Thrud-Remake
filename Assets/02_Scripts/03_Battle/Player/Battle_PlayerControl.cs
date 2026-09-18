using System;
using System.Collections.Generic;
using QFramework;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗玩家控制器（rig，原 PlayerControl，T3.1 改名）。
   ///
   /// **架构定位（2026-09-08）：纯 View / 表现载体**——
   /// - 只提供"战斗表现所需引用 + Designer 字段 + 纯数据访问/换算工具"，
   ///   以及自身生命周期内的清理与 View 侧事件绑定；
   /// - **不再承担**：进入战斗的数据解析、系统初始化、每帧驱动（这些已上移
   ///   到 battle_runtime（<see cref="BattleSceneRuntimeHost"/>，场景初始化 + 驱动节点）。
   ///   各 System 通过 State.Controller（=本对象）读取移动轴/相机/HUD 等表现引用。
   /// </summary>
   public partial class Battle_PlayerControl : ViewController, IController, IUnRegisterList
   {
      [Header("运行时")]
      [SerializeField][Range(0.01f, 4f)] private float scale = 1f;
      [SerializeField] private bool controlTakeOff;
      [SerializeField] private int initialRoleIndex;

      [Header("场景引用（Designer 绑定）")]
      [SerializeField] private BattleEnvironmentEffectController environmentEffectController;

      [Header("配置（ScriptableObject）")]
      [SerializeField] private BattlePlayerConfig playerConfig;

      [Header("弹匣调试 UI（T3.3 正式 HUD 实现后移除）")]
      [SerializeField] private UnityEngine.UI.Text weaponAmmoText;

      [Header("元素子弹 prefab（T2.3.1：按当前角色元素打出对应子弹，由编辑器向导统一装配）")]
      [SerializeField] private GameObject bulletIcePrefab;
      [SerializeField] private GameObject bulletFirePrefab;
      [SerializeField] private GameObject bulletWindPrefab;
      [SerializeField] private GameObject bulletElectricPrefab;

      /// <summary>
      /// 灰盒阶段弹匣/换弹调试文本。
      /// 由 BattleViewSystem 驱动显示"子弹 x/y"与"装填中… z%"；T3.3 正式 HUD 实现后移除。
      /// </summary>
      public UnityEngine.UI.Text WeaponAmmoText => weaponAmmoText;

      /// <summary> 当前控制器注册的事件反注册列表。 </summary>
      public List<IUnRegister> UnregisterList { get; } = new();

      /// <summary>
      /// 当前是否允许玩家控制。
      /// 外部系统可以通过该属性临时接管控制权。
      /// </summary>
      public bool ControlTakeOff
      {
         get => controlTakeOff;
         set => controlTakeOff = value;
      }

      /// <summary> 初始选中的角色索引。</summary>
      public int InitialRoleIndex => initialRoleIndex;

      /// <summary>战斗环境特效控制器。</summary>
      public BattleEnvironmentEffectController EnvironmentEffectController => environmentEffectController;

      /// <summary>
      /// 运行时默认玩家配置兜底。
      /// 当场景/预制体未配置 <see cref="playerConfig"/> 时创建一次，避免空引用导致战斗初始化崩溃。
      /// </summary>
      private static BattlePlayerConfig sFallbackPlayerConfig;

      /// <summary>战斗玩家配置。</summary>
      public BattlePlayerConfig PlayerConfig
      {
         get
         {
            if (playerConfig) return playerConfig;

            if (sFallbackPlayerConfig == null)
            {
               sFallbackPlayerConfig = ScriptableObject.CreateInstance<BattlePlayerConfig>();
               sFallbackPlayerConfig.name = "DefaultBattlePlayerConfig(RuntimeFallback)";
               Debug.LogWarning($"[{name}] 未配置 BattlePlayerConfig，已创建运行时默认配置兜底。请在场景/预制体上指定配置资产。");
            }

            return sFallbackPlayerConfig;
         }
      }

      /// <summary>
      /// 当前战斗架构实例。
      /// 这里不直接依赖 `GetSystem/GetModel` 扩展方法，
      /// 这样可以规避部分 IDE 对扩展方法识别不稳定导致的"方法丢失"提示。
      /// </summary>
      private IArchitecture Architecture => BattleArchitecture.Interface;

      /// <summary>
      /// 初始化（View 只做最轻的时间缩放设置；战斗装配/驱动由 battle_runtime 负责）。
      /// </summary>
      private void Awake()
      {
         Time.timeScale = scale;
      }

      // [2026-09-18] 技能 HUD 已迁出本类：文本渲染与订阅改由 Battle_MainPanel 通过
      // BattleHudState 的 BindableProperty 反向绑定驱动（见 Battle_MainPanel / BattleHudState）。
      // 本类不再持有任何技能 HUD 字段与反注册句柄。
      // Designer 上的 SkillHud* 字段属 CodeGen 产物，随 rig prefab 上的 HUD 对象一并移除后再重新生成。

      /// <summary>
      /// 暴露给 battle_runtime 的 View 表现引用：元素子弹 prefab（开火按当前角色元素取弹）。
      /// </summary>
      public GameObject BulletIcePrefab => bulletIcePrefab;
      public GameObject BulletFirePrefab => bulletFirePrefab;
      public GameObject BulletWindPrefab => bulletWindPrefab;
      public GameObject BulletElectricPrefab => bulletElectricPrefab;

      /// <summary>控制器销毁时清理桥接层与事件注册。</summary>
      private void OnDestroy()
      {
         // 池根为 DontDestroyOnLoad：战斗离场/重开前把仍在场的子弹/弹道清回对象池，防弹幕滞留跨场景
         BattleObjectPool.DespawnAllActive();

         BattleRuntimeBridge.Clear(this);
         BattleElementBulletLibrary.Clear();
         this.UnRegisterAll();
      }

      /// <summary>获取武器实例的父节点。</summary>
      public Transform GetWeaponParent()
      {
         return WeaponAxis;
      }

      /// <summary>将输入方向转换为世界空间移动向量。</summary>
      public Vector3 TransformMoveDirection(float sideForce, float depthForce, float forwardForce)
      {
         var yawAxis = ControlXAxis ? ControlXAxis : transform;

         Vector3 forwardDir;
         if (PlayerConfig.MoveForwardFollowView && Control_Camera)
         {
            forwardDir = Control_Camera.transform.forward;
         }
         else
         {
            forwardDir = yawAxis.forward;
         }

         var outputForce = yawAxis.right * sideForce + forwardDir * forwardForce;
         var depthDir = PlayerConfig.DepthFromWorld ? Vector3.up : yawAxis.up;
         return outputForce + depthDir * depthForce;
      }

      /// <summary>把刚体速度拆分到前向与侧向分量，供动画系统读取。</summary>
      public void GetRigidVelocitySplit(out float forwardSpeed, out float sideSpeed)
      {
         forwardSpeed = 0f;
         sideSpeed = 0f;

         if (!SelfRigidbody) return;

         var fwd = ControlXAxis ? ControlXAxis : transform;
         var velocity = SelfRigidbody.velocity;
         forwardSpeed = Vector3.Dot(velocity, fwd.forward) / 4f;
         sideSpeed = Vector3.Dot(velocity, fwd.right) / 4f;
      }

      /// <summary>判断当前是否存在有效的平面移动输入。</summary>
      public bool HasForwardMoveInput(BattleInputSnapshot input)
      {
         return Mathf.Abs(input.MoveHorizontal) > 0.01f || Mathf.Abs(input.MoveVertical) > 0.01f;
      }

      /// <summary>计算当前锁敌插值速度。</summary>
      public float GetCurrentLockSpeed(float multiplier)
      {
         return PlayerConfig.LockEnemySpeed * Mathf.Max(1f, multiplier);
      }

      /// <summary>把当前战斗上下文同步到桥接层（旧表现脚本兼容；由 battle_runtime 每帧调用）。</summary>
      public void SyncRuntimeBridge()
      {
         var state = Architecture.GetModel<BattleStateModel>();
         var lockPoint = state.LockEnemy.IsAlive() ? state.LockEnemy.LockPoint : null;

         // T2.3：当前角色元素（子弹携带元素依据）
         var activeElement = DamageElement.none;
         var battleDatas = state.BattleDatas;
         if (battleDatas != null && state.SelectedRoleIndex >= 0 && state.SelectedRoleIndex < battleDatas.Length
             && battleDatas[state.SelectedRoleIndex]?.RoleData != null)
         {
            activeElement = battleDatas[state.SelectedRoleIndex].RoleData.damageElement;
         }

         BattleRuntimeBridge.UpdateState(
            this,
            Control_Camera,
            transform,
            lockPoint,
            GetCurrentLockSpeed(state.LockSpeedMultiplier),
            environmentEffectController,
            activeElement);
      }
   }
}
