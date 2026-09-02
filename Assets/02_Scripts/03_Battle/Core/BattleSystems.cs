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
      public void Initialize(PlayerControl controller)
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
      /// </summary>
      public void SelectRole(int index)
      {
         if (State.BattleDatas == null || State.BattleDatas.Length == 0) return;
         if (index < 0 || index >= State.BattleDatas.Length) return;
         if (State.SelectedRoleIndex == index) return;

         State.SelectedRoleIndex = index;
         Architecture.SendEvent(new BattleRoleSwitchedEvent { Index = index });
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

         if (State.ControlTakenOver) return;

         var roleIndex = GetRoleIndexFromKeyboard();
         if (roleIndex >= 0)
         {
            Architecture.GetSystem<BattleRoleSystem>().SelectRole(roleIndex);
         }
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
      private bool CanRoll(PlayerControl controller)
      {
         return Time.time > State.Movement.LastRollTime + controller.PlayerConfig.GetRollCooldown();
      }

      /// <summary>
      /// 执行翻滚。
      /// </summary>
      private void PerformRoll(PlayerControl controller)
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
   /// 负责角色实例装配、角色显隐、动画参数、相机 FOV、展示模型跟随、武器朝向刷新与玩家血条表现。
   /// </summary>
   public sealed class BattleViewSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();
      private IUnRegister mRoleSwitchUnregister;
      private IUnRegister mRollUnregister;
      private IUnRegister mPlayerDamagedUnregister;
      private IUnRegister mPlayerDiedUnregister;

      /// <summary>
      /// 血条满血时的宽度缓存。
      /// Health_Main 为 Sliced 类型左锚点血条，受击时按比例改宽度（沿用旧版 P_VisualEffect 方案）。
      /// </summary>
      private float mHealthFullWidth;

      /// <summary>弹匣调试文本缓存（变化时才重建字符串，避免每帧 GC）。</summary>
      private int mAmmoLastMagazine = int.MinValue;
      private int mAmmoLastMaxMagazine = int.MinValue;
      private bool mAmmoLastReloading;
      private int mAmmoLastProgressStep = int.MinValue;

      protected override void OnInit()
      {
         mRoleSwitchUnregister = Architecture.RegisterEvent<BattleRoleSwitchedEvent>(_ => ApplyRoleSwitch());
         mRollUnregister = Architecture.RegisterEvent<BattleRollTriggeredEvent>(e => PlayRoll(e.RollForce));
         mPlayerDamagedUnregister = Architecture.RegisterEvent<BattlePlayerDamagedEvent>(_ => RefreshPlayerHealth());
         mPlayerDiedUnregister = Architecture.RegisterEvent<BattlePlayerDiedEvent>(_ => RefreshPlayerHealth());
      }

      protected override void OnDeinit()
      {
         mRoleSwitchUnregister?.UnRegister();
         mRoleSwitchUnregister = null;
         mRollUnregister?.UnRegister();
         mRollUnregister = null;
         mPlayerDamagedUnregister?.UnRegister();
         mPlayerDamagedUnregister = null;
         mPlayerDiedUnregister?.UnRegister();
         mPlayerDiedUnregister = null;
      }

      /// <summary>
      /// 初始化角色与武器的场景挂点。
      /// </summary>
      public void InitializeView()
      {
         var controller = State.Controller;
         if (!controller || State.BattleDatas == null) return;

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

         var currentAnimator = State.CurrentBattleData?.RoleAnimator;
         if (currentAnimator)
         {
            currentAnimator.SetTrigger("GameStart");
         }
      }

      /// <summary>
      /// 刷新玩家血条表现（满血宽度 → 按当前血量比例缩放宽度）。
      /// 由受击/死亡事件驱动，避免每帧刷新产生字符串拼接分配。
      /// </summary>
      private void RefreshPlayerHealth()
      {
         var controller = State.Controller;
         if (!controller) return;

         var maxHealth = Mathf.Max(1f, State.PlayerMaxHealth);
         var ratio = Mathf.Clamp01(State.PlayerCurrentHealth / maxHealth);

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
            controller.Health_Value.text = $"{Mathf.RoundToInt(State.PlayerCurrentHealth)}/{Mathf.RoundToInt(maxHealth)}";
         }
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
      /// 文本控件序列化在 PlayerControl.prefab 中（WeaponAmmoUI）；
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
      private void UpdateMovementAnimator(PlayerControl controller)
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
      private void UpdateCameraFov(PlayerControl controller)
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
      /// </summary>
      private void UpdateShowView(PlayerControl controller)
      {
         var showViewAxis = controller.ViewRotateAxis;
         var fwd = controller.ControlXAxis;
         if (!showViewAxis || !fwd) return;

         var targetRotation = Quaternion.Lerp(
            showViewAxis.rotation,
            fwd.rotation,
            Time.deltaTime * controller.PlayerConfig.RotationSpeed);

         var eulerAngles = targetRotation.eulerAngles;
         eulerAngles.z = 0f;
         showViewAxis.rotation = Quaternion.Euler(eulerAngles);
      }

      /// <summary>
      /// 刷新用于调试和表现的武器观察目标点。
      /// 锁定时指向锁定目标；未锁定时指向视角正前方（相机 forward 10m），
      /// 使武器朝向与射击方向跟随鼠标俯仰（与移动"视角正前"模式一致）。
      /// </summary>
      private void UpdateLookTarget(PlayerControl controller)
      {
         if (!controller.WepLookatTarget) return;

         if (State.LockEnemy != null && State.LockEnemy.LockPoint)
         {
            controller.WepLookatTarget.position = State.LockEnemy.LockPoint.position;
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
      /// 锁定时指向锁定目标；未锁定时指向视角正前方的武器观察目标点（见 UpdateLookTarget），
      /// 保证射击方向与视角朝向一致。新武器组件由这里显式驱动，旧武器组件则通过适配器内部保持兼容。
      /// </summary>
      private void UpdateWeaponAim(PlayerControl controller)
      {
         var weaponView = State.CurrentBattleData?.WeaponView;
         if (weaponView == null) return;

         var lookTarget = State.LockEnemy?.LockPoint;
         if (!lookTarget)
         {
            lookTarget = controller.WepLookatTarget ? controller.WepLookatTarget : null;
         }

         weaponView.RefreshAim(lookTarget, controller.GetCurrentLockSpeed(State.LockSpeedMultiplier));
      }

      /// <summary>
      /// 根据当前角色索引刷新角色与武器显隐。
      /// </summary>
      private void ApplyRoleSwitch()
      {
         if (State.BattleDatas == null) return;

         for (var i = 0; i < State.BattleDatas.Length; i++)
         {
            var enabled = i == State.SelectedRoleIndex;
            if (State.BattleDatas[i]?.RoleInstance)
            {
               State.BattleDatas[i].RoleInstance.gameObject.SetActive(enabled);
            }

            if (State.BattleDatas[i]?.WeaponInstance)
            {
               State.BattleDatas[i].WeaponInstance.gameObject.SetActive(enabled);
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

         if (Input.GetKeyDown(KeyCode.Escape))
         {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
         }
         else if (Input.GetMouseButtonDown(0))
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
      private System.Collections.Generic.List<IBattleEnemyTarget> CollectLockCandidates(PlayerControl controller)
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
      private bool IsValidLockTarget(IBattleEnemyTarget enemy, PlayerControl controller)
      {
         if (enemy == null || !enemy.CanBeLocked || !enemy.LockPoint) return false;
         return ProjectEnemyToCanvas(enemy, controller).magnitude < controller.PlayerConfig.LockAssistDistance;
      }

      /// <summary>
      /// 把敌人的锁定点投影到 Canvas 本地坐标。
      /// </summary>
      private Vector2 ProjectEnemyToCanvas(IBattleEnemyTarget enemy, PlayerControl controller)
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
      private IBattleEnemyTarget FindLockEnemy(PlayerControl controller, out Vector2 screenTarget)
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
