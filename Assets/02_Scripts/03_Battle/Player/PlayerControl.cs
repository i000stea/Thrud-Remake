using System;
using System.Collections.Generic;
using QFramework;
using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗玩家控制器。
   /// 该类在新版结构中只承担“场景装配器”和“生命周期入口”的职责：
   /// 1. 提供场景对象与配置引用；
   /// 2. 在 Unity 生命周期中驱动各个 QFramework System；
   /// 3. 把当前战斗上下文同步到桥接层，兼容尚未完全迁移的旧表现脚本。
   /// </summary>
   public partial class PlayerControl : ViewController, IController, IUnRegisterList
   {
      [Header("运行时")]
      [SerializeField][Range(0.01f, 4f)] private float scale = 1f;
      [SerializeField] private bool controlTakeOff;
      [SerializeField] private int initialRoleIndex;

      [Header("数据仓库")]
      [SerializeField] private BattleRoleRepository dataRepository;
      [SerializeField] private StoreBattleRoleData legacyDataStore;

      [Header("场景引用（Designer 绑定）")]
      [SerializeField] private BattleEnvironmentEffectController environmentEffectController;

      [Header("配置（ScriptableObject）")]
      [SerializeField] private BattlePlayerConfig playerConfig;

      [Header("弹匣调试 UI（T3.3 正式 HUD 实现后移除）")]
      [SerializeField] private UnityEngine.UI.Text weaponAmmoText;

      /// <summary>
      /// 灰盒阶段弹匣/换弹调试文本。
      /// 由 BattleViewSystem 驱动显示"子弹 x/y"与"装填中… z%"；T3.3 正式 HUD 实现后移除。
      /// </summary>
      public UnityEngine.UI.Text WeaponAmmoText => weaponAmmoText;

      /// <summary>
      /// 当前控制器注册的事件反注册列表。
      /// </summary>
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

      /// <summary>初始选中的角色索引。</summary>
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
      /// QFramework 归属架构。
      /// </summary>
      IArchitecture IBelongToArchitecture.GetArchitecture() => BattleArchitecture.Interface;

      /// <summary>
      /// 当前战斗架构实例。
      /// 这里不直接依赖 `GetSystem/GetModel` 扩展方法，
      /// 这样可以规避部分 IDE 对扩展方法识别不稳定导致的“方法丢失”提示。
      /// </summary>
      private IArchitecture Architecture => BattleArchitecture.Interface;

      /// <summary>
      /// 初始化控制器。
      /// 这里会设置时间缩放并确保战斗架构被创建。
      /// </summary>
      private void Awake()
      {
         Time.timeScale = scale;
         _ = BattleArchitecture.Interface;
      }

      /// <summary>
      /// 初始化战斗系统。
      /// 角色数据、视图装配和桥接层状态都会在这里完成第一次同步。
      /// </summary>
      private void Start()
      {
         Architecture.GetSystem<BattleRoleSystem>().Initialize(this);
         Architecture.GetSystem<BattleHealthSystem>().Initialize();
         Architecture.GetSystem<BattleViewSystem>().InitializeView();
         SyncRuntimeBridge();
      }

      /// <summary>
      /// 每帧驱动输入、移动前置状态、视角、表现和开火逻辑。
      /// </summary>
      private void Update()
      {
         Architecture.GetSystem<BattleRoleSystem>().SetControlTakenOver(controlTakeOff);
         Architecture.GetSystem<BattleInputSystem>().Tick();
         Architecture.GetSystem<BattleMovementSystem>().TickUpdate();
         Architecture.GetSystem<BattleCameraSystem>().Tick();
         Architecture.GetSystem<BattleViewSystem>().TickUpdate();
         Architecture.GetSystem<BattleWeaponSystem>().Tick();

         if (Input.GetKeyDown(KeyCode.Escape))
         {
            TogglePause();
         }

         SyncRuntimeBridge();
      }

      /// <summary>
      /// 固定帧驱动物理移动与锁敌目标搜索。
      /// </summary>
      private void FixedUpdate()
      {
         Architecture.GetSystem<BattleMovementSystem>().TickFixedUpdate();
         Architecture.GetSystem<BattleAimSystem>().TickFixedUpdate();
         SyncRuntimeBridge();
      }

      /// <summary>
      /// 帧结束后刷新锁敌准星等需要在 LateUpdate 进行插值的表现。
      /// </summary>
      private void LateUpdate()
      {
         Architecture.GetSystem<BattleAimSystem>().TickLateUpdate();
         SyncRuntimeBridge();
      }

      /// <summary>
      /// 控制器销毁时清理桥接层与事件注册。
      /// </summary>
      private void OnDestroy()
      {
         BattleRuntimeBridge.Clear(this);
         this.UnRegisterAll();
      }

      /// <summary>
      /// 解析战斗运行时角色数据。
      /// 优先使用新版数据仓库；若场景尚未切换到新版配置，则回退到旧版数据仓库以保证过渡期可运行。
      /// </summary>
      public BattleRoleRuntimeData[] ResolveBattleDatas()
      {
         if (dataRepository)
         {
            return dataRepository.BuildRuntimeDatas();
         }

         if (legacyDataStore && legacyDataStore.battleDatas != null)
         {
            return ConvertLegacyDatas(legacyDataStore.battleDatas);
         }

         if (StoreBattleRoleData.I != null && StoreBattleRoleData.I.battleDatas != null)
         {
            return ConvertLegacyDatas(StoreBattleRoleData.I.battleDatas);
         }

         return Array.Empty<BattleRoleRuntimeData>();
      }

      /// <summary>
      /// 获取武器实例的父节点。
      /// 若场景中没有显式配置武器父节点，则退回到 Designer 生成的 `WeaponAxis`。
      /// </summary>
      public Transform GetWeaponParent()
      {
         return WeaponAxis;
      }

      /// <summary>
      /// 将输入方向转换为世界空间移动向量。
      /// 前进方向默认跟随视角正前方（含俯仰）；侧向与 Q/E 上下始终以水平 yaw 轴为基准，
      /// 保证鼠标上下（俯仰）不影响左右平移与升降（开关见 BattlePlayerConfig.MoveForwardFollowView）。
      /// </summary>
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

      /// <summary>
      /// 把刚体速度拆分到前向与侧向分量，供动画系统读取。
      /// </summary>
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

      /// <summary>
      /// 判断当前是否存在有效的平面移动输入。
      /// </summary>
      public bool HasForwardMoveInput(BattleInputSnapshot input)
      {
         return Mathf.Abs(input.MoveHorizontal) > 0.01f || Mathf.Abs(input.MoveVertical) > 0.01f;
      }

      /// <summary>
      /// 计算当前锁敌插值速度。
      /// 基础速度与动态倍率会在这里统一相乘。
      /// </summary>
      public float GetCurrentLockSpeed(float multiplier)
      {
         return PlayerConfig.LockEnemySpeed * Mathf.Max(1f, multiplier);
      }

      /// <summary>
      /// 把当前战斗上下文同步到桥接层。
      /// 旧武器、旧敌人 UI、伤害飘字等临时兼容逻辑会从这里读取战斗状态。
      /// 仅锁定时提供 LockPoint：legacy 武器平滑吸附锁定目标；
      /// 未锁定保持 null，legacy 武器姿态自动回正（localRotation → 0,0,0），
      /// 未锁定弹道方向由 WeaponSystem 的 FreeAimRotation（=相机旋转）承担。
      /// </summary>
      private void SyncRuntimeBridge()
      {
         var state = Architecture.GetModel<BattleStateModel>();
         BattleRuntimeBridge.UpdateState(
            this,
            Control_Camera,
            transform,
            state.LockEnemy?.LockPoint,
            GetCurrentLockSpeed(state.LockSpeedMultiplier),
            environmentEffectController);
      }

      /// <summary>
      /// 把旧版运行时数据转换成新版运行时结构。
      /// 该方法只用于迁移过渡，目的是在场景尚未全部替换时保持新版系统可运行。
      /// </summary>
      private static BattleRoleRuntimeData[] ConvertLegacyDatas(EntityBattleData[] legacyDatas)
      {
         if (legacyDatas == null || legacyDatas.Length == 0)
         {
            return Array.Empty<BattleRoleRuntimeData>();
         }

         var results = new BattleRoleRuntimeData[legacyDatas.Length];
         for (var i = 0; i < legacyDatas.Length; i++)
         {
            results[i] = BattleLegacyRuntimeDataAdapter.Convert(legacyDatas[i]);
         }

         return results;
      }

      /// <summary>
      /// 在编辑器中按下 Escape 时暂停运行。
      /// 该逻辑保留了旧版调试习惯，便于快速冻结现场状态。
      /// </summary>
      [System.Diagnostics.Conditional("UNITY_EDITOR")]
      private static void TogglePause()
      {
#if UNITY_EDITOR
         if (!UnityEditor.EditorApplication.isPaused)
         {
            UnityEditor.EditorApplication.isPaused = true;
         }
#endif
      }
   }
}
