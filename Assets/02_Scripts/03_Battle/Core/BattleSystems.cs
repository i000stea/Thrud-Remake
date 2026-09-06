using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 角色系统。
   /// 负责初始化战斗角色、管理当前选角以及处理控制权接管等全局角色状态。
   /// </summary>
   public sealed class BattleRoleSystem : AbstractSystem
   {
      /// <summary>
      /// 当前战斗架构实例。
      /// 使用显式架构调用可以规避部分 IDE 对扩展方法识别不稳定的问题。
      /// </summary>
      private IArchitecture Architecture => BattleArchitecture.Interface;

      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      protected override void OnInit()
      {
      }

      /// <summary>
      /// 初始化角色系统。
      /// </summary>
      public void Initialize(Battle_PlayerControl controller)
      {
         State.Controller = controller;
         State.ControlTakenOver = controller.ControlTakeOff;
         State.BattleDatas = controller.ResolveBattleDatas();
         State.SelectedRoleIndex = Mathf.Clamp(controller.InitialRoleIndex, 0, Mathf.Max(0, State.BattleDatas.Length - 1));
         State.CurrentFov = controller.PlayerConfig.MainFov;
         State.CameraPitch = 0f;
         State.LockEnemy = null;
         State.ScreenTarget = Vector2.zero;
         State.LockSpeedMultiplier = 1f;
         State.Movement.LastRollTime = -controller.PlayerConfig.GetRollCooldown();
         State.Movement.ReadySprint = false;
         State.Movement.Sprinting = false;
      }

      /// <summary>
      /// 设置是否接管控制权。
      /// 若控制被接管，则会强制打断冲刺。
      /// </summary>
      public void SetControlTakenOver(bool controlTakenOver)
      {
         State.ControlTakenOver = controlTakenOver;
         if (controlTakenOver)
         {
            StopSprint();
         }
      }

      /// <summary>
      /// 切换当前角色。
      /// T3.1B：阵亡槽不可切（数字键/自动切人跳过）。
      /// </summary>
      public void SelectRole(int index)
      {
         if (State.BattleDatas == null || State.BattleDatas.Length == 0) return;
         if (index < 0 || index >= State.BattleDatas.Length) return;
         if (State.SelectedRoleIndex == index) return;

         var target = State.BattleDatas[index];
         if (target == null || target.IsDowned) return;

         State.SelectedRoleIndex = index;
         Architecture.SendEvent(new BattleRoleSwitchedEvent { Index = index });
      }

      /// <summary>
      /// 处理成员阵亡后的自动切人（T3.1B）：从 <paramref name="fromIndex"/> 起按槽位序向后
      /// 找到下一个存活成员并切过去；无存活成员返回 false（由健康系统触发全灭判定）。
      /// </summary>
      public bool HandleRoleDowned(int fromIndex)
      {
         if (State.BattleDatas == null || State.BattleDatas.Length == 0) return false;

         var count = State.BattleDatas.Length;
         for (var step = 1; step <= count; step++)
         {
            var index = (fromIndex + step) % count;
            var candidate = State.BattleDatas[index];
            if (candidate == null || candidate.IsDowned) continue;

            State.SelectedRoleIndex = index;
            Debug.Log($"[T3.1B] 自动切至存活成员：{candidate.RoleName}（槽 {index + 1}）。");
            Architecture.SendEvent(new BattleRoleSwitchedEvent { Index = index });
            return true;
         }

         return false;
      }

      /// <summary>
      /// 停止当前冲刺状态。
      /// </summary>
      public void StopSprint()
      {
         State.Movement.ReadySprint = false;
         State.Movement.Sprinting = false;
      }
   }

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
      /// </summary>
      public void Tick()
      {
         var controller = State.Controller;
         if (!controller) return;

         if (State.Paused)
         {
            Clear(); // 暂停中不采集战斗输入
            return;
         }

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

         if (State.ControlTakenOver) return;

         var roleIndex = GetRoleIndexFromKeyboard();
         if (roleIndex >= 0)
         {
            Architecture.GetSystem<BattleRoleSystem>().SelectRole(roleIndex);
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

      protected override void OnInit()
      {
      }

      /// <summary>
      /// 每帧处理翻滚与冲刺状态机。
      /// </summary>
      public void TickUpdate()
      {
         var controller = State.Controller;
         if (!controller || State.ControlTakenOver) return;

         if (State.Input.SprintPressed && CanRoll(controller))
         {
            State.Movement.ReadySprint = true;
            State.Movement.LastRollTime = Time.time;
            PerformRoll(controller);
         }

         if (!State.Input.SprintHeld || !controller.HasForwardMoveInput(State.Input))
         {
            Architecture.GetSystem<BattleRoleSystem>().StopSprint();
         }
         else if (!State.Movement.Sprinting
                  && State.Movement.ReadySprint
                  && Time.time > State.Movement.LastRollTime + controller.PlayerConfig.RollAfterSprintDelay)
         {
            State.Movement.Sprinting = true;
         }
      }

      /// <summary>
      /// 在固定帧中计算推力并施加到刚体。
      /// </summary>
      public void TickFixedUpdate()
      {
         var controller = State.Controller;
         if (!controller || State.ControlTakenOver) return;

         State.Movement.CachedForce = controller.TransformMoveDirection(
            State.Input.MoveHorizontal,
            State.Input.MoveDepth,
            State.Input.MoveVertical);

         var playerRigidbody = controller.SelfRigidbody;
         if (!playerRigidbody) return;

         var force = State.Movement.CachedForce * controller.PlayerConfig.MoveForce;
         if (State.Movement.Sprinting)
         {
            force *= controller.PlayerConfig.SprintMultiForce;
         }

         playerRigidbody.AddForce(force);
      }

      /// <summary>
      /// 判断翻滚冷却是否结束。
      /// </summary>
      private bool CanRoll(Battle_PlayerControl controller)
      {
         return Time.time > State.Movement.LastRollTime + controller.PlayerConfig.GetRollCooldown();
      }

      /// <summary>
      /// 执行翻滚。
      /// </summary>
      private void PerformRoll(Battle_PlayerControl controller)
      {
         var playerRigidbody = controller.SelfRigidbody;
         if (!playerRigidbody) return;

         var force = State.Movement.CachedForce;
         if (force.sqrMagnitude < 0.01f)
         {
            force = controller.ControlXAxis ? controller.ControlXAxis.forward : controller.transform.forward;
         }
         else
         {
            force = force.normalized;
         }

         force *= controller.PlayerConfig.MoveForce * controller.PlayerConfig.RollMultiForce;
         playerRigidbody.AddForce(force);

         var rollForce = new Vector2(State.Input.MoveHorizontal, State.Input.MoveVertical);
         State.Movement.RollForce = rollForce == Vector2.zero ? Vector2.up : rollForce.normalized;
         Architecture.SendEvent(new BattleRollTriggeredEvent { RollForce = State.Movement.RollForce });
      }
   }

   /// <summary>
   /// 表现系统。
   /// 负责角色实例装配、角色显隐、动画参数、相机 FOV、展示模型跟随、武器朝向刷新、
   /// 玩家（当前出战成员）血条与 T3.1B 队伍血条列表表现。
   /// </summary>
   public sealed class BattleViewSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();
      private IUnRegister mRoleSwitchUnregister;
      private IUnRegister mRollUnregister;
      private IUnRegister mRoleHealthChangedUnregister;
      private IUnRegister mRoleDownedUnregister;

      /// <summary>
      /// 血条满血时的宽度缓存。
      /// Health_Main 为 Sliced 类型左锚点血条，受击时按比例改宽度（沿用旧版 P_VisualEffect 方案）。
      /// </summary>
      private float mHealthFullWidth;

      /// <summary>T3.1B 队伍血条列表 HUD（无则跳过：灰盒场景未挂 Canvas 时静默）。</summary>
      private BattleTeamHudView mTeamHud;

      /// <summary>弹匣调试文本缓存（变化时才重建字符串，避免每帧 GC）。</summary>
      private int mAmmoLastMagazine = int.MinValue;
      private int mAmmoLastMaxMagazine = int.MinValue;
      private bool mAmmoLastReloading;
      private int mAmmoLastProgressStep = int.MinValue;

      protected override void OnInit()
      {
         mRoleSwitchUnregister = Architecture.RegisterEvent<BattleRoleSwitchedEvent>(_ =>
         {
            ApplyRoleSwitch();
            RefreshPlayerHealth();
            RefreshTeamHud();
         });
         mRollUnregister = Architecture.RegisterEvent<BattleRollTriggeredEvent>(e => PlayRoll(e.RollForce));
         mRoleHealthChangedUnregister = Architecture.RegisterEvent<BattleRoleHealthChangedEvent>(_ =>
         {
            RefreshPlayerHealth();
            RefreshTeamHud();
         });
         mRoleDownedUnregister = Architecture.RegisterEvent<BattleRoleDownedEvent>(_ =>
         {
            ApplyRoleSwitch();
            RefreshPlayerHealth();
            RefreshTeamHud();
         });
      }

      protected override void OnDeinit()
      {
         mRoleSwitchUnregister?.UnRegister();
         mRoleSwitchUnregister = null;
         mRollUnregister?.UnRegister();
         mRollUnregister = null;
         mRoleHealthChangedUnregister?.UnRegister();
         mRoleHealthChangedUnregister = null;
         mRoleDownedUnregister?.UnRegister();
         mRoleDownedUnregister = null;
         DisposeTeamHud();
      }

      /// <summary>
      /// 初始化角色与武器的场景挂点。
      /// </summary>
      public void InitializeView()
      {
         var controller = State.Controller;
         if (!controller || State.BattleDatas == null) return;

         mHealthFullWidth = 0f; // 新场景血条宽度重新缓存
         DisposeTeamHud();

         var roleParent = controller.Roles;
         var weaponParent = controller.GetWeaponParent();

         for (var i = 0; i < State.BattleDatas.Length; i++)
         {
            var data = State.BattleDatas[i];
            if (data?.RoleInstance)
            {
               if (roleParent) data.RoleInstance.SetParent(roleParent, false);
               data.RoleInstance.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            if (data?.WeaponInstance)
            {
               if (weaponParent) data.WeaponInstance.SetParent(weaponParent, false);
               data.WeaponInstance.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
         }

         ApplyRoleSwitch();
         RefreshPlayerHealth();
         BuildTeamHud(controller);

         var currentAnimator = State.CurrentBattleData?.RoleAnimator;
         if (currentAnimator)
         {
            currentAnimator.SetTrigger("GameStart");
         }
      }

      /// <summary>
      /// 刷新玩家血条表现（显示当前出战成员血量；T3.1B 成员级）。
      /// 由血量变化/切换/阵亡事件驱动，避免每帧刷新产生字符串拼接分配。
      /// </summary>
      private void RefreshPlayerHealth()
      {
         var controller = State.Controller;
         if (!controller) return;

         var data = State.CurrentBattleData;
         var maxHealth = data != null ? Mathf.Max(1f, data.MaxHealth) : 1f;
         var current = data != null && !data.IsDowned ? data.CurrentHealth : 0f;
         var ratio = Mathf.Clamp01(current / maxHealth);

         var healthImage = controller.Health_Main;
         if (healthImage)
         {
            if (mHealthFullWidth <= 0f)
            {
               mHealthFullWidth = healthImage.rectTransform.sizeDelta.x;
            }

            var size = healthImage.rectTransform.sizeDelta;
            size.x = mHealthFullWidth * ratio;
            healthImage.rectTransform.sizeDelta = size;
         }

         if (controller.Health_Value)
         {
            controller.Health_Value.text = data != null
               ? $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(maxHealth)}"
               : "--/--";
         }
      }

      /// <summary>
      /// 构建队伍血条列表（T3.1B 灰盒 HUD）：运行时生成在战斗 Canvas 左上角。
      /// </summary>
      private void BuildTeamHud(Battle_PlayerControl controller)
      {
         if (!controller.MainCanvas || State.BattleDatas == null || State.BattleDatas.Length == 0) return;

         mTeamHud = BattleTeamHudView.Create(controller.MainCanvas, State.BattleDatas.Length);
         mTeamHud?.Refresh(State.BattleDatas);
      }

      /// <summary>刷新队伍血条列表（事件驱动）。</summary>
      private void RefreshTeamHud()
      {
         if (mTeamHud == null) return;
         mTeamHud.Refresh(State.BattleDatas);
      }

      /// <summary>销毁队伍血条列表（场景重建/系统销毁时调用）。</summary>
      private void DisposeTeamHud()
      {
         if (mTeamHud == null) return;
         mTeamHud.Dispose();
         mTeamHud = null;
      }

      /// <summary>
      /// 每帧刷新表现。
      /// </summary>
      public void TickUpdate()
      {
         var controller = State.Controller;
         if (!controller) return;

         UpdateMovementAnimator(controller);
         UpdateCameraFov(controller);
         UpdateShowView(controller);
         UpdateLookTarget(controller);
         UpdateWeaponAim(controller);
         RefreshWeaponAmmo();
      }

      /// <summary>
      /// 刷新弹匣/换弹调试文本（T3.3 正式 HUD 实现后移除）。
      /// 文本控件序列化在 Battle_PlayerControl.prefab 中（WeaponAmmoUI）；
      /// 仅在数值变化时重建字符串，避免每帧字符串拼接产生 GC。
      /// </summary>
      private void RefreshWeaponAmmo()
      {
         var text = State.Controller?.WeaponAmmoText;
         if (!text) return;

         var mode = State.CurrentBattleData?.WeaponMode;
         if (mode == null)
         {
            SetAmmoText(text, "");
            return;
         }

         var magazine = mode.CurrentMagazine;
         var maxMagazine = mode.MaxMagazine;
         var reloading = mode.IsReloading;
         var progressStep = Mathf.RoundToInt(mode.ReloadProgress * 20f);

         if (magazine == mAmmoLastMagazine
             && maxMagazine == mAmmoLastMaxMagazine
             && reloading == mAmmoLastReloading
             && progressStep == mAmmoLastProgressStep)
         {
            return;
         }

         mAmmoLastMagazine = magazine;
         mAmmoLastMaxMagazine = maxMagazine;
         mAmmoLastReloading = reloading;
         mAmmoLastProgressStep = progressStep;

         SetAmmoText(text, reloading
            ? $"装填中… {progressStep * 5}%"
            : $"子弹 {magazine}/{maxMagazine}");
      }

      /// <summary>
      /// 仅在内容变化时写入调试文本。
      /// </summary>
      private static void SetAmmoText(UnityEngine.UI.Text text, string content)
      {
         if (text.text != content)
         {
            text.text = content;
         }
      }

      /// <summary>
      /// 刷新移动动画参数。
      /// </summary>
      private void UpdateMovementAnimator(Battle_PlayerControl controller)
      {
         var animator = State.CurrentBattleData?.RoleAnimator;
         if (!animator) return;

         controller.GetRigidVelocitySplit(out var forwardSpeed, out var sideSpeed);
         animator.SetFloat("MoveFront", forwardSpeed);
         animator.SetFloat("MoveSide", sideSpeed);
         animator.SetBool("OnSprint", State.Movement.Sprinting);
      }

      /// <summary>
      /// 刷新相机视场角。
      /// </summary>
      private void UpdateCameraFov(Battle_PlayerControl controller)
      {
         var camera = controller.Control_Camera;
         if (!camera) return;

         var targetFov = State.Movement.Sprinting ? controller.PlayerConfig.SprintFov : controller.PlayerConfig.MainFov;
         State.CurrentFov = Mathf.Lerp(
            State.CurrentFov,
            targetFov,
            Time.deltaTime * controller.PlayerConfig.FovLerpSpeed);

         camera.fieldOfView = State.CurrentFov;
      }

      /// <summary>
      /// 刷新展示模型旋转。
      /// 模型整体朝向跟随视角（含俯仰，ClampLookRotation 限制最大俯仰避免穿地/倒立）；
      /// 无相机时退回水平 yaw 跟随。
      /// </summary>
      private void UpdateShowView(Battle_PlayerControl controller)
      {
         var showViewAxis = controller.ViewRotateAxis;
         if (!showViewAxis) return;

         Quaternion viewRotation;
         if (controller.Control_Camera)
         {
            viewRotation = ClampLookRotation(controller.Control_Camera.transform.forward, 80f);
         }
         else if (controller.ControlXAxis)
         {
            viewRotation = controller.ControlXAxis.rotation;
         }
         else
         {
            return;
         }

         var targetRotation = Quaternion.Lerp(
            showViewAxis.rotation,
            viewRotation,
            Time.deltaTime * controller.PlayerConfig.RotationSpeed);

         var eulerAngles = targetRotation.eulerAngles;
         eulerAngles.z = 0f;
         showViewAxis.rotation = Quaternion.Euler(eulerAngles);
      }

      /// <summary>
      /// 由任意方向构造"只含 yaw+pitch、无 roll"的旋转，并把俯仰限制在 [-maxPitch, maxPitch]。
      /// 用于模型/武器朝向跟随视角；避免 LookRotation 在垂直看向时退化。
      /// </summary>
      private static Quaternion ClampLookRotation(Vector3 direction, float maxPitch)
      {
         var normalized = direction.normalized;
         var flat = new Vector3(normalized.x, 0f, normalized.z);
         if (flat.sqrMagnitude < 0.0001f)
         {
            return Quaternion.identity;
         }

         var pitchRad = Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f));
         var pitchDeg = Mathf.Clamp(pitchRad * Mathf.Rad2Deg, -maxPitch, maxPitch);
         var yaw = Quaternion.LookRotation(flat.normalized, Vector3.up);
         return yaw * Quaternion.Euler(-pitchDeg, 0f, 0f);
      }

      /// <summary>
      /// 刷新用于调试和表现的武器观察目标点（未锁定观察点距离 10，仅表现/调试用，
      /// 不再参与武器朝向与弹道——未锁定弹道方向由 FreeAimRotation=相机旋转承担）。
      /// </summary>
      private void UpdateLookTarget(Battle_PlayerControl controller)
      {
         if (!controller.WepLookatTarget) return;

         var lockEnemy = State.LockEnemy;
         if (!lockEnemy.IsAlive()) lockEnemy = null;

         if (lockEnemy != null && lockEnemy.LockPoint)
         {
            controller.WepLookatTarget.position = lockEnemy.LockPoint.position;
         }
         else if (controller.Control_Camera)
         {
            var cameraTransform = controller.Control_Camera.transform;
            controller.WepLookatTarget.position =
               cameraTransform.position + cameraTransform.forward * 10f;
         }
         else if (controller.ControlXAxis)
         {
            controller.WepLookatTarget.position =
               controller.ControlXAxis.position
               + controller.ControlXAxis.forward * 10f;
         }
      }

      /// <summary>
      /// 刷新当前武器的朝向。
      /// 锁定时：legacy 武器由自身 Update 平滑吸附锁定目标（经 Bridge 提供 LockPoint），
      /// 新版组件走 RefreshAim 平滑吸附。
      /// 未锁定时：武器姿态回正（legacy 自朝向把 localRotation 收敛到 0,0,0；
      /// 新版组件 RefreshAim(null) 同理）——未锁定弹道方向由 FreeAimRotation（=相机旋转）承担，
      /// 因此子弹仍打向准星/视角正前，而武器模型不指向远处点。
      /// </summary>
      private void UpdateWeaponAim(Battle_PlayerControl controller)
      {
         var weaponView = State.CurrentBattleData?.WeaponView;
         if (weaponView == null) return;

         var lockEnemy = State.LockEnemy;
         var lookTarget = lockEnemy.IsAlive() ? lockEnemy.LockPoint : null;
         weaponView.RefreshAim(lookTarget, controller.GetCurrentLockSpeed(State.LockSpeedMultiplier));
      }

      /// <summary>
      /// 根据当前角色索引与阵亡态刷新角色/武器显隐（T3.1B：阵亡成员恒隐藏）。
      /// </summary>
      private void ApplyRoleSwitch()
      {
         if (State.BattleDatas == null) return;

         for (var i = 0; i < State.BattleDatas.Length; i++)
         {
            var data = State.BattleDatas[i];
            var enabled = i == State.SelectedRoleIndex && data != null && !data.IsDowned;

            if (data != null && data.RoleInstance)
            {
               data.RoleInstance.gameObject.SetActive(enabled);
            }

            if (data != null && data.WeaponInstance)
            {
               data.WeaponInstance.gameObject.SetActive(enabled);
            }
         }
      }

      /// <summary>
      /// 播放翻滚动画。
      /// </summary>
      private void PlayRoll(Vector2 rollForce)
      {
         var animator = State.CurrentBattleData?.RoleAnimator;
         if (!animator) return;

         animator.SetFloat("RollFront", rollForce.y);
         animator.SetFloat("RollSide", rollForce.x);
         animator.Play("RollBlend", 0, 0f);
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

         // ESC 改由 Battle_PlayerControl 统一处理暂停菜单；此处仅保留"点击画面锁定视角"
         if (Input.GetMouseButtonDown(0))
         {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
         }

         if (State.ControlTakenOver || Cursor.lockState != CursorLockMode.Locked) return;

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
   /// 负责在所有已注册敌人中搜索合适的锁定目标，并驱动准星插值。
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
         if (Input.GetKeyDown(controller.PlayerConfig.LockSwitchKey)
             && Time.time >= mSwitchCooldownUntil)
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
               State.ScreenTarget = ProjectEnemyToCanvas(State.LockEnemy, controller);
            }
         }

         if (!mManualLocked)
         {
            State.LockEnemy = FindLockEnemy(controller, out var screenTarget);
            State.ScreenTarget = screenTarget;
         }

         if (previousEnemy != State.LockEnemy)
         {
            Architecture.SendEvent(new BattleLockEnemyChangedEvent
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
         State.ScreenTarget = ProjectEnemyToCanvas(State.LockEnemy, controller);
      }

      /// <summary>
      /// 收集当前可见且在吸附距离内的候选目标（Tab 切换用，事件驱动非每帧）。
      /// </summary>
      private System.Collections.Generic.List<IBattleEnemyTarget> CollectLockCandidates(Battle_PlayerControl controller)
      {
         var candidates = new System.Collections.Generic.List<IBattleEnemyTarget>(4);
         var camera = controller.Control_Camera;
         var canvas = controller.MainCanvas;
         if (!camera || !canvas) return candidates;

         foreach (var enemy in BattleEnemyRegistry.EnumerateTargets())
         {
            if (enemy == null || !enemy.CanBeLocked || !enemy.LockPoint) continue;

            if (ProjectEnemyToCanvas(enemy, controller).magnitude >= controller.PlayerConfig.LockAssistDistance) continue;

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
         return ProjectEnemyToCanvas(enemy, controller).magnitude < controller.PlayerConfig.LockAssistDistance;
      }

      /// <summary>
      /// 把敌人的锁定点投影到 Canvas 本地坐标。
      /// </summary>
      private Vector2 ProjectEnemyToCanvas(IBattleEnemyTarget enemy, Battle_PlayerControl controller)
      {
         var camera = controller.Control_Camera;
         var canvas = controller.MainCanvas;
         if (!camera || !canvas || enemy?.LockPoint == null) return Vector2.zero;

         var screenPosition = camera.WorldToScreenPoint(enemy.LockPoint.position);
         RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPosition,
            camera,
            out var canvasPosition);
         return canvasPosition;
      }

      /// <summary>
      /// 在帧结束阶段平滑移动锁敌准星。
      /// </summary>
      public void TickLateUpdate()
      {
         var controller = State.Controller;
         if (!controller) return;

         var lockEnemyArm = controller.Cross_LockEnemy;
         if (!lockEnemyArm) return;

         var distance = Vector2.Distance(lockEnemyArm.anchoredPosition, State.ScreenTarget);
         if (distance > 50f || distance <= 0f)
         {
            State.LockSpeedMultiplier = 1f;
         }
         else if (distance > 15f)
         {
            State.LockSpeedMultiplier = 4f;
         }
         else
         {
            State.LockSpeedMultiplier = 12f;
         }

         lockEnemyArm.anchoredPosition = Vector2.Lerp(
            lockEnemyArm.anchoredPosition,
            State.ScreenTarget,
            controller.GetCurrentLockSpeed(State.LockSpeedMultiplier) * Time.deltaTime);
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
         var canvas = controller.MainCanvas;
         if (!camera || !canvas) return null;

         IBattleEnemyTarget selectedEnemy = null;
         var selectedDistance = float.MaxValue;

         foreach (var enemy in BattleEnemyRegistry.EnumerateTargets())
         {
            if (enemy == null || !enemy.CanBeLocked || !enemy.LockPoint) continue;

            var screenPosition = camera.WorldToScreenPoint(enemy.LockPoint.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
               canvas.transform as RectTransform,
               screenPosition,
               camera,
               out var canvasPosition);

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
         mRoleSwitchedUnregister = Architecture.RegisterEvent<BattleRoleSwitchedEvent>(_ => OnRoleSwitched());
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
            Architecture.SendEvent(new BattleReloadCompletedEvent());
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
