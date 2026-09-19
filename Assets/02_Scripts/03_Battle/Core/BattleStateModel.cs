using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗状态模型（战斗架构唯一的 Model）。
   ///
   /// **组织原则（2026-09-19 平铺重构，用户评审意见）**：
   /// - **不为任何 View 单独开容器**：原 `BattleHudState Hud` 已删除；数据**平铺**在本类上，
   ///   View 自行绑定它需要的那几个字段（原话："模型中不需要为 hud 单独考虑…view 显示内容由 view 层自己负责"）。
   /// - `BindableProperty` 只用于**需要通知 UI 的仿真事实（标量）**；其余保持普通字段。
   /// - **只为"当前值"建标量**：QFramework 的 `BindableProperty` 没有集合变更通知，
   ///   不要为"每个成员 / 每个槽位"建属性。需要"每成员"的 View（如队伍血条）自行订阅
   ///   `BattleEvent_Role*` 后按索引拉取。
   /// - 类型纪律：`BindableProperty<T>.Comparer` 是**按 T 共享的 static 字段**，默认比较 `(a,b)=>a.Equals(b)`
   ///   对值类型会退化为 `object.Equals` → 装箱。框架 `ComparerAutoRegister`（`QFramework.cs:754`）已在
   ///   `BeforeSceneLoad` 注册：`int/long/float/double/string` + `Vector2/Vector3/Vector4/Quaternion/Color/Color32/
   ///   Bounds/Rect/Vector2Int/Vector3Int/BoundsInt/RangeInt/RectInt`，**这些类型可安全直接用**。
   ///   ⚠️ `WithComparer` 改的是**静态**字段（按 T 全局生效），不要用它做局部约定。
   /// - **通道纪律**：`BindableProperty` 是"**状态观察通道**"，**不是每帧命令通道**——
   ///   其 setter 对"值未变"直接 return（`QFramework.cs:717`），所以对每帧都要消费的命令流会静默丢帧。
   ///   本类只承载"会被 UI 观察、且值不变就无需通知"的状态（血量/弹药/技能/选中位）。
   /// - ⚠️ `AimPosition` / `AimLerpSpeed` 是**视图空间量**（Canvas 参考坐标 + 缓动参数），
   ///   是本类仅存的"越界"项，待下沉到 UI 层（见 `19_表现层分层_View与Actor_设计.md` §7）。
   ///
   /// **为何仍是单一 Model（2026-09-19 评估结论）**：一个模型 = 一个生命周期 + 一组不变量。
   /// "关卡 / 玩家 / 敌人"三个域共享同一生命周期（都是"每场战斗"），且 `LevelState` / `ControlTakenOver` / `Paused`
   /// 是**横切字段**被多域同时读写；按域拆只会把交叉藏进跨模型的读写里，比单一 Model 更难追踪。
   /// 且"敌人"目前根本没有模型（血量/行为由 `BattleEnemyEntity` 自持，经 `BattleEnemyRegistry` 暴露）。
   /// 拆分的收益要等到"多场战斗并行/回放""模型需独立序列化""跨程序集共享"三者之一成立时才出现。
   /// 届时**优先只拆关卡**（`BattleLevelModel`：每关独立、由 BattleLevelFlow/BattleStageDirector 独占读写）。
   ///
   /// **写入方约定**：每个字段在注释里写明唯一写入口；View 只读。
   /// </summary>
   public sealed class BattleStateModel : AbstractModel
   {
      #region 装配期引用（战斗开始时写一次）

      /// <summary>当前战斗 rig（场景装配入口）。写入：battle_runtime 装配。</summary>
      public Battle_PlayerControl Controller;

      /// <summary>出战成员运行时数据（槽位序 = 切人序）。写入：BattleRoleSystem.Initialize。</summary>
      public BattleRoleRuntimeData[] BattleDatas = System.Array.Empty<BattleRoleRuntimeData>();

      #endregion

      #region 每帧输入

      /// <summary>每帧输入快照。写入：BattleInputSystem.Tick（唯一）。其余系统只读。</summary>
      public readonly BattleInputSnapshot Input = new();

      #endregion

      #region 移动

      /// <summary>移动运行时状态（缓存推力 / 翻滚方向 / 冲刺标记）。写入：BattleMovementSystem。</summary>
      public readonly BattleMovementState Movement = new();

      #endregion

      #region 视角与相机

      /// <summary>相机俯仰角缓存。写入：BattleCameraSystem。</summary>
      public float CameraPitch;

      #endregion

      #region 锁敌

      /// <summary>当前锁定目标。判空必须用 <see cref="BattleCombatUtility.IsAlive"/>（接口判空不触发 Unity 重载）。</summary>
      public IBattleEnemyTarget LockEnemy;

      /// <summary>锁敌插值速度倍率（按准星距目标远近动态调整）。写入：BattleAimSystem。</summary>
      public float LockSpeedMultiplier = 1f;

      #endregion

      #region 角色与生命（可绑定；View 直接消费）

      /// <summary>当前出战角色槽位。写入：BattleRoleSystem 选角 / 自动切人。</summary>
      public readonly BindableProperty<int> SelectedRoleIndex = new(0);

      /// <summary>当前出战成员的当前血量。写入：扣血 / 治疗 / 切人时（PublishHealth）。</summary>
      public readonly BindableProperty<float> CurrentMemberHealth = new(0f);

      /// <summary>当前出战成员的最大血量。写入：同上。</summary>
      public readonly BindableProperty<float> CurrentMemberMaxHealth = new(1f);

      #endregion

      #region 技能（可绑定；View 直接消费）

      /// <summary>技能能量上限。</summary>
      public const float RoleEnergyMax = 100f;

      /// <summary>当前技能能量（0~<see cref="RoleEnergyMax"/>）。写入：BattleSkillSystem 能量结算。</summary>
      public readonly BindableProperty<float> RoleEnergy = new(0f);

      /// <summary>E（元素战技）剩余冷却秒数；&gt;0 表示冷却中。写入：BattleSkillSystem 广播。</summary>
      public readonly BindableProperty<float> SkillERemain = new(0f);

      /// <summary>Q（极奏）剩余冷却秒数；&gt;0 表示冷却中。写入：同上。</summary>
      public readonly BindableProperty<float> SkillQRemain = new(0f);

      /// <summary>Q 的能量缺口（冷却已到但能量不足时 &gt;0）。写入：同上。</summary>
      public readonly BindableProperty<float> SkillQDeficit = new(0f);

      #endregion

      #region 武器（可绑定；View 直接消费）

      /// <summary>当前武器弹匣余弹；&lt;0 表示当前没有武器。写入：PublishAmmo。</summary>
      public readonly BindableProperty<int> CurrentWeaponMagazine = new(-1);

      /// <summary>当前武器弹匣容量。写入：PublishAmmo。</summary>
      public readonly BindableProperty<int> CurrentWeaponMaxMagazine = new(0);

      /// <summary>是否正在装填（0/1；用 int 以走框架的快速比较器）。写入：PublishAmmo。</summary>
      public readonly BindableProperty<int> CurrentWeaponReloading = new(0);

      /// <summary>
      /// 装填进度 **原始 0..1**。
      /// 刻意不存"百分比 / 显示步数"——那是 View 的显示决定（见 19 文档 §3.1）。
      /// </summary>
      public readonly BindableProperty<float> CurrentWeaponReloadProgress = new(0f);

      #endregion

      #region 瞄准（⚠️ 视图空间量，待下沉到 UI 层）

      /// <summary>准星目标位置（**Canvas/UI 参考空间**，非世界坐标）。写入：BattleAimSystem。</summary>
      public readonly BindableProperty<Vector2> AimPosition = new(Vector2.zero);

      /// <summary>准星插值速度（视图缓动参数）。写入：BattleAimSystem。</summary>
      public readonly BindableProperty<float> AimLerpSpeed = new(10f);

      #endregion

      #region 关卡流程与控制权

      /// <summary>关卡流程状态（Prepare/Wave/Victory/Defeat/Exit）。写入：BattleLevelFlow 状态机。</summary>
      public BattleLevelState LevelState = BattleLevelState.Exit;

      /// <summary>暂停标记（Time.timeScale=0，进程不暂停）。写入：BattleUiSystem。</summary>
      public bool Paused;

      /// <summary>控制权是否被接管（终局 / 演出）。</summary>
      public bool ControlTakenOver;

      #endregion

      #region 派生只读

      /// <summary>当前正在操控的成员数据；无有效成员时返回 null。</summary>
      public BattleRoleRuntimeData CurrentBattleData
      {
         get
         {
            if (BattleDatas == null || BattleDatas.Length == 0) return null;
            var safeIndex = Mathf.Clamp(SelectedRoleIndex.Value, 0, BattleDatas.Length - 1);
            return BattleDatas[safeIndex];
         }
      }

      /// <summary>
      /// 当前操控角色的元素（子弹携带依据）。
      /// 派生只读；与 <see cref="CurrentBattleData"/> 同源，取不到时回落 `none`。
      /// </summary>
      public DamageElement CurrentDamageElement => CurrentBattleData?.RoleData?.damageElement ?? DamageElement.none;

      /// <summary>
      /// 战斗相机。**派生只读**：由装配期写入的 <see cref="Controller"/>（rig 上的序列化引用）推出，
      /// 取不到时返回 null。原先相机被镜像到静态的 `BattleRuntimeBridge.Camera`，本属性取代那条路径。
      /// </summary>
      public Camera BattleCamera => Controller ? Controller.Control_Camera : null;

      /// <summary>
      /// 环境特效控制器（伤害飘字等）。**派生只读**，同 <see cref="BattleCamera"/>，取不到时返回 null。
      /// </summary>
      public BattleEnvironmentEffectController EnvironmentEffects
         => Controller ? Controller.EnvironmentEffectController : null;

      #endregion

      #region 写入辅助（生产者调用；View 不调用）

      /// <summary>把某成员的当前血量同步到"当前出战成员"读数（扣血 / 治疗 / 切人时）。</summary>
      public void PublishHealth(BattleRoleRuntimeData member)
      {
         CurrentMemberMaxHealth.Value = member != null ? Mathf.Max(1f, member.MaxHealth) : 1f;
         CurrentMemberHealth.Value = member != null && !member.IsDowned ? member.CurrentHealth : 0f;
      }

      /// <summary>把某成员的武器状态同步到"当前武器"读数（每帧 / 换弹时）。</summary>
      public void PublishAmmo(BattleRoleRuntimeData member)
      {
         var mode = member?.WeaponMode;
         CurrentWeaponMagazine.Value = mode?.CurrentMagazine ?? -1;
         CurrentWeaponMaxMagazine.Value = mode?.MaxMagazine ?? 0;
         CurrentWeaponReloading.Value = mode != null && mode.IsReloading ? 1 : 0;
         CurrentWeaponReloadProgress.Value = mode != null ? Mathf.Clamp01(mode.ReloadProgress) : 0f;
      }

      #endregion

      protected override void OnInit() { }
   }

   /// <summary>
   /// 每帧输入快照。
   /// 该对象将 Unity 输入系统和战斗逻辑解耦，使后续更容易切换到新输入系统。
   /// </summary>
   public sealed class BattleInputSnapshot
   {
      /// <summary>鼠标水平移动量。</summary>
      public float MouseX;

      /// <summary>鼠标垂直移动量。</summary>
      public float MouseY;

      /// <summary>水平移动输入。</summary>
      public float MoveHorizontal;

      /// <summary>前后移动输入。</summary>
      public float MoveVertical;

      /// <summary>上下移动输入。</summary>
      public float MoveDepth;

      /// <summary>是否存在有效移动输入。</summary>
      public bool HasMoveInput;

      /// <summary>是否持续按住冲刺键。</summary>
      public bool SprintHeld;

      /// <summary>本帧是否按下冲刺键。</summary>
      public bool SprintPressed;

      /// <summary>本帧是否按下开火键。</summary>
      public bool FireHeld;

      /// <summary>本帧是否按下换弹键。</summary>
      public bool ReloadPressed;

      /// <summary>T2.4 本帧是否按下元素战技键（E）。</summary>
      public bool SkillEPressed;

      /// <summary>T2.4 本帧是否按下极奏键（Q）。</summary>
      public bool SkillQPressed;
   }

   /// <summary> 移动系统运行时状态。 </summary>
   public sealed class BattleMovementState
   {
      /// <summary> 当前帧缓存的世界空间推力。 </summary>
      public Vector3 CachedForce;

      /// <summary> 翻滚方向。 </summary>
      public Vector2 RollForce = Vector2.up;

      /// <summary> 是否处于"翻滚后允许进入冲刺"的准备状态。 </summary>
      public bool ReadySprint;

      /// <summary> 当前是否已经进入冲刺状态。</summary>
      public bool Sprinting;

      /// <summary> 上一次执行翻滚的时间。 </summary>
      public float LastRollTime = -999f;
   }
}
