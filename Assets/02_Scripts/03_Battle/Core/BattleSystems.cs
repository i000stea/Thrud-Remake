using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 输入系统。
   /// 负责把 Unity 输入采样写入共享输入快照。
   /// </summary>
   public sealed class BattleInputSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      protected override void OnInit()
      {
      }

      /// <summary>
      /// 采集当前帧输入。
      /// T1（触屏，2026-09-08 用户调整）：**二选一**——
      /// 触屏层启用时仅采集虚拟输入（键鼠战斗输入不生效）；未启用时仅采集键鼠（原逻辑零回归）。
      /// 战斗逻辑只读快照，零改动。
      /// </summary>
      public void Tick()
      {
         var controller = State.Controller;
         if (!controller) return;

         // 暂停键脉冲在暂停中也要能消费（移动端通过暂停按钮恢复），故放在暂停早退之前。
         if (BattleVirtualInput.Enabled && BattleVirtualInput.ConsumePauseToggle())
         {
            RequestPauseIfPlayable();
         }

         if (State.Paused)
         {
            Clear(); // 暂停中不采集战斗输入
            return;
         }

         if (BattleVirtualInput.Enabled)
         {
            SampleVirtual(controller);
         }
         else
         {
            SampleKeyboard(controller);
         }
      }

      /// <summary>
      /// 触屏独占采样（T1，2026-09-08 用户调整：编辑器下"仅触屏 UI"或"仅键鼠"二选一，不做合并）。
      /// 虚拟摇杆/拖拽/按钮 → 输入快照；此路径不读取键鼠战斗输入。
      /// </summary>
      private void SampleVirtual(Battle_PlayerControl controller)
      {
         var input = State.Input;
         input.MoveHorizontal = BattleVirtualInput.MoveX;
         input.MoveVertical = BattleVirtualInput.MoveY;
         input.MoveDepth = 0f; // 触屏首版无升降键（13 §3.1 布局未列）
         input.HasMoveInput = Mathf.Abs(input.MoveHorizontal) > 0.01f
                              || Mathf.Abs(input.MoveVertical) > 0.01f;

         // 镜头：拖拽像素 → 等效鼠标轴（相机系统再乘 MouseSensitivity×dt）
         input.MouseX = BattleVirtualInput.LookDeltaX * BattleVirtualInput.VirtualLookAxisScale;
         input.MouseY = BattleVirtualInput.LookDeltaY * BattleVirtualInput.VirtualLookAxisScale;

         input.SprintHeld = BattleVirtualInput.SprintHeld;
         input.SprintPressed = BattleVirtualInput.ConsumeRollOrSprint();
         input.FireHeld = BattleVirtualInput.FireHeld;
         input.ReloadPressed = BattleVirtualInput.ConsumeReload();
         input.SkillEPressed = BattleVirtualInput.ConsumeSkillE();
         input.SkillQPressed = BattleVirtualInput.ConsumeSkillQ();

         BattleVirtualInput.EndFrame();

         if (State.ControlTakenOver) return;

         // 触屏切人：←/→ 循环（跳过阵亡）
         var direction = BattleVirtualInput.ConsumeCycleRole();
         if (direction != 0)
         {
            CycleRole(direction);
         }
      }

      /// <summary>键鼠独占采样（触屏层未启用时的原逻辑，零回归）。</summary>
      private void SampleKeyboard(Battle_PlayerControl controller)
      {
         var input = State.Input;
         input.MouseX = Input.GetAxis("Mouse X");
         input.MouseY = Input.GetAxis("Mouse Y");
         input.MoveHorizontal = Input.GetAxis("Horizontal");
         input.MoveVertical = Input.GetAxis("Vertical");
         input.MoveDepth = Input.GetAxis("Depth");
         input.HasMoveInput = Mathf.Abs(input.MoveHorizontal) > 0.01f || Mathf.Abs(input.MoveVertical) > 0.01f;
         input.SprintHeld = Input.GetKey(KeyCode.LeftShift);
         input.SprintPressed = Input.GetKeyDown(KeyCode.LeftShift);
         input.FireHeld = Input.GetMouseButton(0);
         input.ReloadPressed = Input.GetKeyDown(controller.PlayerConfig.ReloadKey);
         input.SkillEPressed = Input.GetKeyDown(KeyCode.E);
         input.SkillQPressed = Input.GetKeyDown(KeyCode.Q);

         var roleIndex = GetRoleIndexFromKeyboard();
         if (roleIndex >= 0)
         {
            Architecture.GetSystem<BattleRoleSystem>().SelectRole(roleIndex);
         }
      }

      /// <summary>非终局时发送暂停切换请求（与 PlayerControl 的 ESC 守卫语义一致）。</summary>
      private void RequestPauseIfPlayable()
      {
         if (State.LevelState == BattleLevelState.Victory || State.LevelState == BattleLevelState.Defeat)
         {
            return;
         }

         Architecture.SendEvent(new BattleEvent_PauseToggleRequested());
      }

      /// <summary>
      /// 触屏循环切人：从当前槽沿方向找下一个存活角色（跳过阵亡），无存活则保持。
      /// T3.1B 语义：阵亡槽不可切（与 HandleRoleDowned/SelectRole 同规则）。
      /// </summary>
      private void CycleRole(int direction)
      {
         var datas = State.BattleDatas;
         if (datas == null || datas.Length <= 1) return;

         var count = datas.Length;
         var start = State.SelectedRoleIndex.Value;
         for (var step = 1; step <= count; step++)
         {
            var index = ((start + direction * step) % count + count) % count;
            var candidate = datas[index];
            if (candidate == null || candidate.IsDowned) continue;

            Architecture.GetSystem<BattleRoleSystem>().SelectRole(index);
            return;
         }
      }

      /// <summary>
      /// 清空输入快照（暂停/恢复过渡调用：防止残留的按下/按住状态在恢复瞬间误触发）。
      /// </summary>
      public void Clear()
      {
         var input = State.Input;
         input.MouseX = 0f;
         input.MouseY = 0f;
         input.MoveHorizontal = 0f;
         input.MoveVertical = 0f;
         input.MoveDepth = 0f;
         input.HasMoveInput = false;
         input.SprintHeld = false;
         input.SprintPressed = false;
         input.FireHeld = false;
         input.ReloadPressed = false;
         input.SkillEPressed = false;
         input.SkillQPressed = false;
      }

      /// <summary>
      /// 从数字键读取切人索引。
      /// </summary>
      private static int GetRoleIndexFromKeyboard()
      {
         if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
         if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
         if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
         if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;
         return -1;
      }
   }

   /// <summary>
   /// 移动系统。
   /// 负责翻滚、冲刺状态切换以及刚体施力。
   /// </summary>
   public sealed class BattleMovementSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      /// <summary>
      /// 本系统**不持有 View**（2026-09-19 与 Role 同批调整）：只做"翻滚/冲刺状态机 + 推力计算"，
      /// 结果写入 <see cref="BattleMovementState"/> / 由 Tick 返回；
      /// 世界方向解析与力的施加由宿主交给 <see cref="BattleMovementActor"/> 执行。
      /// 此前本系统持有 `BattleMovementActor` 并直接 AddForce，属 System → View 的错误耦合。
      /// </summary>
      private BattlePlayerConfig config;
      protected override void OnInit() { }
      public void Initialize(BattlePlayerConfig config) { this.config = config; }

      /// <summary>
      /// 每帧处理翻滚与冲刺状态机。
      /// </summary>
      /// <param name="basisForward">无移动输入时翻滚的基准方向（宿主传入 Actor 的世界前方）。</param>
      /// <returns>本次翻滚需施加的冲量；无需施力时为 <see cref="Vector3.zero"/>。</returns>
      public Vector3 TickUpdate(Vector3 basisForward)
      {
         if (State.ControlTakenOver) return Vector3.zero;

         var pending = Vector3.zero;

         if (State.Input.SprintPressed && CanRoll())
         {
            State.Movement.ReadySprint = true;
            State.Movement.LastRollTime = Time.time;
            pending = PerformRoll(basisForward);
         }

         if (!State.Input.SprintHeld || !(Mathf.Abs(State.Input.MoveHorizontal) > 0.01f || Mathf.Abs(State.Input.MoveVertical) > 0.01f))
         {
            Architecture.GetSystem<BattleRoleSystem>().StopSprint();
         }
         else if (!State.Movement.Sprinting
                  && State.Movement.ReadySprint
                  && Time.time > State.Movement.LastRollTime + config.RollAfterSprintDelay)
         {
            State.Movement.Sprinting = true;
         }

         return pending;
      }

      /// <summary>
      /// 在固定帧中计算推力（**不施力**；力由宿主交给 <see cref="BattleMovementActor"/> 施加）。
      /// </summary>
      /// <param name="moveDirection">输入解析出的世界空间方向（宿主经 Actor 的基向量解析）。</param>
      /// <returns>本帧应施加的力；无需施力时为 <see cref="Vector3.zero"/>。</returns>
      public Vector3 TickFixedUpdate(Vector3 moveDirection)
      {
         if (State.ControlTakenOver) return Vector3.zero;

         State.Movement.CachedForce = moveDirection;

         var force = State.Movement.CachedForce * config.MoveForce;
         if (State.Movement.Sprinting)
         {
            force *= config.SprintMultiForce;
         }

         return force;
      }

      /// <summary>
      /// 判断翻滚冷却是否结束。
      /// </summary>
      private bool CanRoll()
      {
         return Time.time > State.Movement.LastRollTime + config.GetRollCooldown();
      }

      /// <summary>执行翻滚：返回需施加的冲量，并广播翻滚事件（表现层/Actor 自行订阅）。</summary>
      private Vector3 PerformRoll(Vector3 basisForward)
      {
         var force = State.Movement.CachedForce;
         if (force.sqrMagnitude < 0.01f)
         {
            force = basisForward;
         }
         else
         {
            force = force.normalized;
         }

         force *= config.MoveForce * config.RollMultiForce;

         var rollForce = new Vector2(State.Input.MoveHorizontal, State.Input.MoveVertical);
         State.Movement.RollForce = rollForce == Vector2.zero ? Vector2.up : rollForce.normalized;
         Architecture.SendEvent(new BattleEvent_RollTriggered { RollForce = State.Movement.RollForce });

         return force;
      }
   }

   /// <summary>
   /// 相机系统。
   /// 负责鼠标锁定与视角旋转。
   /// </summary>
   public sealed class BattleCameraSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      protected override void OnInit()
      {
      }

      /// <summary>
      /// 每帧刷新相机状态。
      /// </summary>
      public void Tick()
      {
         var controller = State.Controller;
         if (!controller) return;

         // T3.1 终局：保持鼠标解锁（结算弹窗点击用），不响应 Escape/左键锁定
         if (State.LevelState == BattleLevelState.Victory || State.LevelState == BattleLevelState.Defeat)
         {
            return;
         }

         var touchMode = BattleVirtualInput.Enabled;

         // ESC 改由 Battle_PlayerControl 统一处理暂停菜单；此处仅保留"点击画面锁定视角"
         // T1（触屏）：移动端无鼠标锁定语义（拖拽转镜头），跳过锁定流程
         if (!touchMode && Input.GetMouseButtonDown(0))
         {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
         }

         if (State.ControlTakenOver || (!touchMode && Cursor.lockState != CursorLockMode.Locked)) return;

         var horizontalAxis = controller.ControlXAxis;
         var pitchingAxis = controller.ControlYAxis;
         if (!horizontalAxis || !pitchingAxis) return;

         var mouseX = State.Input.MouseX * controller.PlayerConfig.MouseSensitivity * Time.deltaTime;
         var mouseY = State.Input.MouseY * controller.PlayerConfig.MouseSensitivity * Time.deltaTime;

         State.CameraPitch = Mathf.Clamp(State.CameraPitch - mouseY, -90f, 90f);
         pitchingAxis.localRotation = Quaternion.Euler(State.CameraPitch, 0f, 0f);
         horizontalAxis.Rotate(Vector3.up * mouseX);
      }
   }

   /// <summary>
   /// 锁敌系统。
   /// 负责在所有已注册敌人中搜索合适的锁定目标，并发布准星目标位置。
   /// T1.1 半自动：准星接近自动吸附（FindLockEnemy）+ Tab 在可见目标间循环切换/取消（TrySwitchLockedTarget）。
   /// </summary>
   public sealed class BattleAimSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      /// <summary>
      /// 是否处于 Tab 手动锁定状态。
      /// 手动锁定期间不重新自动吸附，直到目标失效或再次切换。
      /// </summary>
      private bool mManualLocked;

      /// <summary>
      /// 下一次允许切换锁定的时间点。
      /// </summary>
      private float mSwitchCooldownUntil;

      protected override void OnInit()
      {
      }

      /// <summary>
      /// 在固定帧中搜索锁敌目标，并处理 Tab 切换/取消。
      /// </summary>
      public void TickFixedUpdate()
      {
         var controller = State.Controller;
         if (!controller) return;

         // 锁定目标已被销毁（接口引用判空无效）：清空锁定状态，避免访问已销毁对象
         if (State.LockEnemy != null && !State.LockEnemy.IsAlive())
         {
            mManualLocked = false;
            State.LockEnemy = null;
         }

         // T1.1：Tab 循环切换/取消锁定（半自动）
         // T1（触屏）：虚拟"切换锁敌"脉冲等价 Tab
         var lockSwitchRequested = Input.GetKeyDown(controller.PlayerConfig.LockSwitchKey)
                                   || (BattleVirtualInput.Enabled && BattleVirtualInput.ConsumeLockSwitch());
         if (lockSwitchRequested && Time.time >= mSwitchCooldownUntil)
         {
            mSwitchCooldownUntil = Time.time + controller.PlayerConfig.LockSwitchCooldown;
            TrySwitchLockedTarget(1);
         }

         var previousEnemy = State.LockEnemy;

         if (mManualLocked)
         {
            // 手动锁定：保持当前目标，直到目标失效（死亡/出屏/超距）后回到自动吸附
            if (!IsValidLockTarget(State.LockEnemy, controller))
            {
               mManualLocked = false;
               State.LockEnemy = null;
            }
            else
            {
               State.AimPosition.Value = ProjectEnemyToAimSpace(State.LockEnemy, controller);
            }
         }

         if (!mManualLocked)
         {
            State.LockEnemy = FindLockEnemy(controller, out var screenTarget);
            State.AimPosition.Value = screenTarget;
         }

         if (previousEnemy != State.LockEnemy)
         {
            Architecture.SendEvent(new BattleEvent_LockEnemyChanged
            {
               EnemyTransform = State.LockEnemy?.LockPoint
            });
         }
      }

      /// <summary>
      /// Tab 循环切换锁定目标。
      /// direction=1 切换到下一个，-1 切换到上一个。
      /// 无候选目标时取消锁定；只有一个候选且已是当前目标时也视为取消。
      /// </summary>
      public void TrySwitchLockedTarget(int direction)
      {
         if (State.ControlTakenOver) return;

         var controller = State.Controller;
         if (!controller) return;

         // 锁定目标已被销毁：先清空再处理切换
         if (State.LockEnemy != null && !State.LockEnemy.IsAlive())
         {
            mManualLocked = false;
            State.LockEnemy = null;
         }

         var candidates = CollectLockCandidates(controller);
         if (candidates.Count == 0)
         {
            mManualLocked = false;
            State.LockEnemy = null;
            return;
         }

         var currentIndex = -1;
         if (State.LockEnemy != null)
         {
            for (var i = 0; i < candidates.Count; i++)
            {
               if (candidates[i] == State.LockEnemy)
               {
                  currentIndex = i;
                  break;
               }
            }
         }

         var nextIndex = currentIndex < 0
            ? (direction > 0 ? 0 : candidates.Count - 1)
            : (currentIndex + direction + candidates.Count) % candidates.Count;

         if (currentIndex >= 0 && nextIndex == currentIndex)
         {
            // 只有当前一个候选 → 取消锁定
            mManualLocked = false;
            State.LockEnemy = null;
            return;
         }

         mManualLocked = true;
         State.LockEnemy = candidates[nextIndex];
         State.AimPosition.Value = ProjectEnemyToAimSpace(State.LockEnemy, controller);
      }

      /// <summary>
      /// 收集当前可见且在吸附距离内的候选目标（Tab 切换用，事件驱动非每帧）。
      /// </summary>
      private System.Collections.Generic.List<IBattleEnemyTarget> CollectLockCandidates(Battle_PlayerControl controller)
      {
         var candidates = new System.Collections.Generic.List<IBattleEnemyTarget>(4);
         var camera = controller.Control_Camera;
         if (!camera) return candidates;

         foreach (var enemy in BattleEnemyRegistry.EnumerateTargets())
         {
            if (enemy == null || !enemy.CanBeLocked || !enemy.LockPoint) continue;

            if (ProjectEnemyToAimSpace(enemy, controller).magnitude >= controller.PlayerConfig.LockAssistDistance) continue;

            candidates.Add(enemy);
         }

         return candidates;
      }

      /// <summary>
      /// 判断目标是否仍可作为有效锁定对象（存在、可锁定、在吸附距离内）。
      /// </summary>
      private bool IsValidLockTarget(IBattleEnemyTarget enemy, Battle_PlayerControl controller)
      {
         if (!enemy.IsAlive() || !enemy.CanBeLocked || !enemy.LockPoint) return false;
         return ProjectEnemyToAimSpace(enemy, controller).magnitude < controller.PlayerConfig.LockAssistDistance;
      }

      /// <summary>
      /// 把敌人的锁定点投影到 1920 宽参考坐标（中心为原点）。
      /// </summary>
      private Vector2 ProjectEnemyToAimSpace(IBattleEnemyTarget enemy, Battle_PlayerControl controller)
      {
         var camera = controller.Control_Camera;
         if (!camera || enemy?.LockPoint == null) return Vector2.zero;
         return ProjectToAimSpace(camera, enemy.LockPoint.position);
      }

      private static Vector2 ProjectToAimSpace(Camera camera, Vector3 position)
      {
         var pixel = camera.WorldToScreenPoint(position);
         return new Vector2(pixel.x - Screen.width * 0.5f, pixel.y - Screen.height * 0.5f)
            * (AimReferenceWidth / Mathf.Max(1, Screen.width));
      }

      /// <summary>
      /// 准星偏移的参考屏宽（与 <see cref="Battle_MainPanel.AimReferenceWidth"/> **必须一致**）。
      /// 为何在此重复一份：本常量属"视图空间"参数，而 Aim 的 Canvas 投影尚未下沉到 UI 层
      /// （见 19 文档 §7 待办）。让 System 反向引用 UI 类型会立刻违反刚确立的"System 不认识 View"规则，
      /// 因此宁可暂时重复一个数字；待投影下沉到 `Battle_MainPanel` 后，这份重复随之删除。
      /// </summary>
      private const float AimReferenceWidth = 1920f;

      // 吸附倍率属于武器规则，使用独立逻辑游标，不读取 View 的 RectTransform。
      private Vector2 trackingPosition;
      
      /// <summary> 重置瞄准 </summary>
      public void ResetForBattle()
      {
         mManualLocked = false;
         mSwitchCooldownUntil = 0f;
         trackingPosition = Vector2.zero;
         State.AimPosition.Value = Vector2.zero;
         State.LockSpeedMultiplier = 1f;
         State.AimLerpSpeed.Value = State.Controller ? State.Controller.PlayerConfig.LockEnemySpeed : 10f;
      }

      public void TickLateUpdate()
      {
         var controller = State.Controller;
         if (!controller) return;
         var target = State.AimPosition.Value;
         var distance = Vector2.Distance(trackingPosition, target);
         State.LockSpeedMultiplier = distance > 50f || distance <= 0f ? 1f : distance > 15f ? 4f : 12f;
         State.AimLerpSpeed.Value = controller.GetCurrentLockSpeed(State.LockSpeedMultiplier);
         trackingPosition = Vector2.Lerp(trackingPosition, target, State.AimLerpSpeed.Value * Time.deltaTime);
      }

      /// <summary>
      /// 搜索当前最适合作为锁敌对象的敌人。
      /// 算法仍然延续旧版思路：优先综合考虑屏幕距离与世界距离。
      /// </summary>
      private IBattleEnemyTarget FindLockEnemy(Battle_PlayerControl controller, out Vector2 screenTarget)
      {
         screenTarget = Vector2.zero;
         if (State.ControlTakenOver) return null;

         var camera = controller.Control_Camera;
         if (!camera) return null;

         IBattleEnemyTarget selectedEnemy = null;
         var selectedDistance = float.MaxValue;

         foreach (var enemy in BattleEnemyRegistry.EnumerateTargets())
         {
            if (enemy == null || !enemy.CanBeLocked || !enemy.LockPoint) continue;

            var canvasPosition = ProjectToAimSpace(camera, enemy.LockPoint.position);

            var canvasDistance = canvasPosition.magnitude;
            if (canvasDistance >= controller.PlayerConfig.LockAssistDistance) continue;

            var worldDistance = Vector3.Distance(controller.transform.position, enemy.RootTransform.position);
            if (worldDistance <= selectedDistance / 2f
                || (worldDistance < selectedDistance && canvasDistance < selectedDistance))
            {
               selectedDistance = worldDistance;
               selectedEnemy = enemy;
               screenTarget = canvasPosition;
            }
         }

         return selectedEnemy;
      }
   }

   /// <summary>
   /// 武器系统。
   /// 每帧驱动当前角色的武器开火与换弹进度；
   /// 角色切换时取消旧角色换弹、为新角色补满弹匣。
   /// </summary>
   public sealed class BattleWeaponSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();
      private IUnRegister mRoleSwitchedUnregister;

      protected override void OnInit()
      {
         mRoleSwitchedUnregister = Architecture.RegisterEvent<BattleEvent_RoleSwitched>(_ => OnRoleSwitched());
      }

      protected override void OnDeinit()
      {
         mRoleSwitchedUnregister?.UnRegister();
         mRoleSwitchedUnregister = null;
      }

      /// <summary>
      /// 每帧驱动换弹进度并检测开火。
      /// 顺序：同步换弹时长 → 推进换弹 → 手动换弹 → 开火。
      /// </summary>
      public void Tick()
      {
         if (State.ControlTakenOver) return;

         var weaponMode = State.CurrentBattleData?.WeaponMode;
         if (weaponMode == null) return;

         var config = State.Controller?.PlayerConfig;
         if (config)
         {
            weaponMode.ReloadDuration = config.WeaponReloadTime;
         }

         // 自由瞄准发射基准：未锁定时取相机旋转（武器姿态回正 0,0,0，子弹仍打向准星/视角正前）；
         // 锁定时置空，子弹按武器实际朝向（吸附锁定目标）发射
         var controller = State.Controller;
         var aimCamera = controller ? controller.Control_Camera : null;
         var hasLockTarget = State.LockEnemy.IsAlive();
         weaponMode.FreeAimRotation = !hasLockTarget && aimCamera
            ? aimCamera.transform.rotation
            : (Quaternion?)null;

         // 推进换弹进度，计时结束补满弹匣并派发完成事件（占位：控制台日志）
         if (weaponMode.TickReload())
         {
            Architecture.SendEvent(new BattleEvent_ReloadCompleted());
            Debug.Log($"[T1.2] {State.CurrentBattleData?.RoleName} 换弹完成，弹匣补满。");
         }

         // 手动换弹
         if (State.Input.ReloadPressed)
         {
            weaponMode.StartReload();
         }

         // 开火（换弹期间或空仓时由 PressTrigger 内部处理）
         if (State.Input.FireHeld)
         {
            weaponMode.PressTrigger();
         }

         // T1.3 调试：F8 打印对象池回收统计（验证无 Instantiate/Destroy 分配用，后续可移除）
         if (Input.GetKeyDown(KeyCode.F8))
         {
            Debug.Log($"[T1.3] 对象池统计 -> {BattleObjectPool.DumpStats()}");
         }
      }

      /// <summary>
      /// 角色切换处理（切人补满弹匣，原版规则）。
      /// 先取消所有角色进行中的换弹——切走时正在换弹的角色保持空弹匣，
      /// 切回时仍为空；随后为新角色补满弹匣。
      /// </summary>
      private void OnRoleSwitched()
      {
         foreach (var data in State.BattleDatas)
         {
            data?.WeaponMode?.CancelReload();
         }

         State.CurrentBattleData?.WeaponMode?.OnRoleSwitched();
      }
   }
}
