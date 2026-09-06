using System;
using System.Collections.Generic;
using QFramework;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗玩家控制器（原 PlayerControl，T3.1 改名）。
   /// 该类只承担"场景装配器"与"生命周期入口"职责：
   /// 1. 提供场景对象与配置引用（不含任何 UI 面板——结算/暂停面板由 BattleUiSystem 经 UIKit UIRoot 添加/移除）；
   /// 2. 在 Unity 生命周期中驱动各个 QFramework System；
   /// 3. 把当前战斗上下文同步到桥接层，兼容尚未完全迁移的旧表现脚本。
   /// 暂停（ESC 菜单）仅在此处派发 <see cref="BattlePauseToggleRequestedEvent"/>，
   /// timeScale / 菜单显隐 / 鼠标由 BattleUiSystem 统一处理（与控制器解耦）。
   /// </summary>
   public partial class Battle_PlayerControl : ViewController, IController, IUnRegisterList
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
      /// 这样可以规避部分 IDE 对扩展方法识别不稳定导致的"方法丢失"提示。
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
         Architecture.GetModel<BattleStateModel>().Paused = false; // 复位暂停态（架构常驻跨场景）

         Architecture.GetSystem<BattleRoleSystem>().Initialize(this);
         Architecture.GetSystem<BattleHealthSystem>().Initialize();
         Architecture.GetSystem<BattleViewSystem>().InitializeView();
         SyncRuntimeBridge();

         // T2.3.1：注册元素子弹（开火按角色元素取弹）
         BattleElementBulletLibrary.Register(bulletIcePrefab, bulletFirePrefab, bulletWindPrefab, bulletElectricPrefab);

         // T2.4：技能系统按本场角色数量重建冷却表、清零能量并广播首帧状态
         Architecture.GetSystem<BattleSkillSystem>().ResetForBattle();

         // T3.1：关卡流程无条件复位进入 Prepare（打印首个状态日志；终局去向由结算弹窗按钮触发）
         Architecture.GetSystem<BattleLevelFlow>().StartLevel();

         // T2.4 里程碑1：技能 HUD 事件注册（反向 bind——状态写入方广播，UI 订阅刷新）
         RegisterSkillHudEvents();
      }

      /// <summary>
      /// 每帧驱动输入、移动前置状态、视角、表现和开火逻辑。
      /// ESC 只派发暂停请求（状态/菜单由 BattleUiSystem 处理）；暂停期间战斗逻辑不推进。
      /// </summary>
      private void Update()
      {
         var state = Architecture.GetModel<BattleStateModel>();

         Architecture.GetSystem<BattleRoleSystem>().SetControlTakenOver(controlTakeOff);
         Architecture.GetSystem<BattleInputSystem>().Tick();

         // T3.1：ESC = 暂停/恢复请求（终局由结算弹窗接管，不再响应）
         if (Input.GetKeyDown(KeyCode.Escape)
             && state.LevelState != BattleLevelState.Victory
             && state.LevelState != BattleLevelState.Defeat)
         {
            Architecture.SendEvent(new BattlePauseToggleRequestedEvent());
         }

         if (state.Paused)
         {
            SyncRuntimeBridge();
            return;
         }

         Architecture.GetSystem<BattleMovementSystem>().TickUpdate();
         Architecture.GetSystem<BattleCameraSystem>().Tick();
         Architecture.GetSystem<BattleViewSystem>().TickUpdate();
         Architecture.GetSystem<BattleWeaponSystem>().Tick();

         // T2.4：技能管道（输入快照已含 E/Q，冷却读秒 + 施放校验在本系统内收口）
         Architecture.GetSystem<BattleSkillSystem>().Tick();

         SyncRuntimeBridge();
      }

      /// <summary>角色切换事件反注册句柄（T2.4 技能 HUD）。</summary>
      private IUnRegister mSkillHudSwitchUnregister;

      /// <summary>技能能量变化事件反注册句柄（T2.4）。</summary>
      private IUnRegister mSkillHudEnergyUnregister;

      /// <summary>技能槽冷却/就绪事件反注册句柄（T2.4）。</summary>
      private IUnRegister mSkillHudCooldownUnregister;

      /// <summary>
      /// T2.4 技能 HUD：以"反向 bind（事件注册）"方式驱动，而非每帧轮询 StateModel——
      /// 状态写入方（角色切换 / 能量结算 / 冷却推进）在写模型后广播事件，
      /// UI 订阅事件、事件到达才刷新文本；注册后立即初始化一次首帧数据。
      /// </summary>
      private void RegisterSkillHudEvents()
      {
         mSkillHudSwitchUnregister = Architecture.RegisterEvent<BattleRoleSwitchedEvent>(_ => RefreshSkillHudUi());
         mSkillHudEnergyUnregister = Architecture.RegisterEvent<BattleSkillEnergyChangedEvent>(_ => RefreshSkillHudUi());
         mSkillHudCooldownUnregister = Architecture.RegisterEvent<BattleSkillCooldownChangedEvent>(_ => RefreshSkillHudUi());
         RefreshSkillHudUi();
      }

      /// <summary>根据当前状态刷新右下角技能/切人 HUD 文本（事件驱动）。</summary>
      private void RefreshSkillHudUi()
      {
         if (SkillHudRoleLine == null || SkillHudELine == null || SkillHudQLine == null || SkillHudSwitchLine == null)
         {
            return; // 未装配 SkillHud 的场景静默跳过
         }

         var model = Architecture.GetModel<BattleStateModel>();

         var roleIndex = model.SelectedRoleIndex;
         var data = roleIndex >= 0 && roleIndex < model.BattleDatas.Length ? model.BattleDatas[roleIndex] : null;
         var roleName = data?.RoleName ?? "--";
         var element = data?.RoleData != null ? data.RoleData.damageElement : DamageElement.none;
         SkillHudRoleLine.text = roleName + " · " + ElementChineseName(element);

         var skillSystem = Architecture.GetSystem<BattleSkillSystem>();
         var hasSkill = element != DamageElement.none && BattleSkillCatalog.Get(element) != null;
         SkillHudELine.text = hasSkill ? "[E] 元素战技 · " + FormatSlotState(skillSystem, BattleSkillSlot.Elemental) : "[E] —";
         SkillHudQLine.text = hasSkill
            ? $"[Q] 极奏 " + FormatSlotState(skillSystem, BattleSkillSlot.Ultimate) + $" · 能量 {(int)model.RoleEnergy}/{BattleStateModel.RoleEnergyMax:0}"
            : $"[Q] — · 能量 {(int)model.RoleEnergy}/{BattleStateModel.RoleEnergyMax:0}";
         SkillHudSwitchLine.text = "[1-4] 切换角色";
      }

      /// <summary>把槽位就绪/冷却/能量不足格式化为 HUD 文案。</summary>
      private static string FormatSlotState(BattleSkillSystem skillSystem, BattleSkillSlot slot)
      {
         if (skillSystem.IsSlotReady(slot))
         {
            return "就绪";
         }

         var remain = skillSystem.GetSlotRemainSeconds(slot);
         if (slot == BattleSkillSlot.Ultimate && remain <= 0.05f)
         {
            return $"能量不足({skillSystem.GetSlotEnergyDeficit(slot):0})";
         }

         return $"冷却 {remain:0.0}s";
      }

      /// <summary>元素枚举 → 中文（仅用于临时 HUD 文案）。</summary>
      private static string ElementChineseName(DamageElement element)
      {
         switch (element)
         {
            case DamageElement.Ice: return "冰";
            case DamageElement.Fire: return "火";
            case DamageElement.Wind: return "风";
            case DamageElement.Electric: return "电";
            default: return "无";
         }
      }

      /// <summary>固定帧驱动物理移动与锁敌目标搜索（暂停中不推进）。</summary>
      private void FixedUpdate()
      {
         if (Architecture.GetModel<BattleStateModel>().Paused) return;

         Architecture.GetSystem<BattleMovementSystem>().TickFixedUpdate();
         Architecture.GetSystem<BattleAimSystem>().TickFixedUpdate();
         SyncRuntimeBridge();
      }

      /// <summary>帧结束后刷新锁敌准星（暂停中不推进）。</summary>
      private void LateUpdate()
      {
         if (Architecture.GetModel<BattleStateModel>().Paused) return;

         Architecture.GetSystem<BattleAimSystem>().TickLateUpdate();
         SyncRuntimeBridge();
      }

      /// <summary>控制器销毁时清理桥接层与事件注册。</summary>
      private void OnDestroy()
      {
         BattleRuntimeBridge.Clear(this);
         BattleElementBulletLibrary.Clear();
         mSkillHudSwitchUnregister?.UnRegister();
         mSkillHudSwitchUnregister = null;
         mSkillHudEnergyUnregister?.UnRegister();
         mSkillHudEnergyUnregister = null;
         mSkillHudCooldownUnregister?.UnRegister();
         mSkillHudCooldownUnregister = null;
         this.UnRegisterAll();
      }

      /// <summary>
      /// 解析战斗运行时角色数据（T3.1A 数据管道）。
      /// 优先级：
      /// 1. <see cref="BattleTeamSession"/> 有选队 → 实体化本次出战编队（角色 SO + 武器 SO，任意搭配）；
      /// 2. 编辑模式直连（无会话）→ 默认编队（仓库成对数组）/ 旧版仓库过渡兜底，与改造前行为一致；
      /// 3. 正式构建且无会话 → 视为流程错误：收集日志后退回主场景（不崩溃、不开空队）。
      /// </summary>
      public BattleRoleRuntimeData[] ResolveBattleDatas()
      {
         if (BattleTeamSession.HasSelection)
         {
            var members = BattleTeamSession.Members;
            return dataRepository
               ? dataRepository.BuildFromSession(members)
               : BattleTeamRuntimeBuilder.Build(members);
         }

         if (Application.isEditor)
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

         Debug.LogError(
            $"[T3.1A] 正式环境进入战斗但缺少出战编队会话——请从「{BattleTeamSession.TeamSelectSceneName}」进入。已退回 {BattleTeamSession.ReturnSceneName}。");
         QFramework.UIKit.CloseAllPanel(); // 清面板表防跨场景残留已销毁面板
         BattleSceneGate.TransitionTo(BattleTeamSession.ReturnSceneName);
         return Array.Empty<BattleRoleRuntimeData>();
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

      /// <summary>把当前战斗上下文同步到桥接层（旧表现脚本兼容）。</summary>
      private void SyncRuntimeBridge()
      {
         var state = Architecture.GetModel<BattleStateModel>();
         var lockPoint = state.LockEnemy.IsAlive() ? state.LockEnemy.LockPoint : null;

         // T2.3：当前角色元素（子弹携带元素依据）
         var activeElement = TeaGame.DamageElement.none;
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

      /// <summary>把旧版运行时数据转换成新版运行时结构（过渡期兼容）。</summary>
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
   }
}
