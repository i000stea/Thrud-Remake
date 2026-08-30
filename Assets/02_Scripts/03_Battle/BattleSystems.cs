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
   /// 负责角色实例装配、角色显隐、动画参数、相机 FOV、展示模型跟随和武器朝向刷新。
   /// </summary>
   public sealed class BattleViewSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();
      private IUnRegister mRoleSwitchUnregister;
      private IUnRegister mRollUnregister;

      protected override void OnInit()
      {
         mRoleSwitchUnregister = Architecture.RegisterEvent<BattleRoleSwitchedEvent>(_ => ApplyRoleSwitch());
         mRollUnregister = Architecture.RegisterEvent<BattleRollTriggeredEvent>(e => PlayRoll(e.RollForce));
      }

      protected override void OnDeinit()
      {
         mRoleSwitchUnregister?.UnRegister();
         mRoleSwitchUnregister = null;
         mRollUnregister?.UnRegister();
         mRollUnregister = null;
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

         var currentAnimator = State.CurrentBattleData?.RoleAnimator;
         if (currentAnimator)
         {
            currentAnimator.SetTrigger("GameStart");
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
      /// </summary>
      private void UpdateLookTarget(PlayerControl controller)
      {
         if (!controller.WepLookatTarget) return;

         if (State.LockEnemy != null && State.LockEnemy.LockPoint)
         {
            controller.WepLookatTarget.position = State.LockEnemy.LockPoint.position;
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
      /// 新武器组件由这里显式驱动，旧武器组件则通过适配器内部保持兼容。
      /// </summary>
      private void UpdateWeaponAim(PlayerControl controller)
      {
         var weaponView = State.CurrentBattleData?.WeaponView;
         if (weaponView == null) return;

         var lookTarget = State.LockEnemy?.LockPoint;
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
   /// </summary>
   public sealed class BattleAimSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      protected override void OnInit()
      {
      }

      /// <summary>
      /// 在固定帧中搜索锁敌目标。
      /// </summary>
      public void TickFixedUpdate()
      {
         var controller = State.Controller;
         if (!controller) return;

         var previousEnemy = State.LockEnemy;
         State.LockEnemy = FindLockEnemy(controller, out var screenTarget);
         State.ScreenTarget = screenTarget;

         if (previousEnemy != State.LockEnemy)
         {
            Architecture.SendEvent(new BattleLockEnemyChangedEvent
            {
               EnemyTransform = State.LockEnemy?.LockPoint
            });
         }
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
            if (canvasDistance >= controller.PlayerConfig.EnemyLockDistance) continue;

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
   /// 负责把输入转换为当前角色武器模式的开火行为。
   /// </summary>
   public sealed class BattleWeaponSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      protected override void OnInit()
      {
      }

      /// <summary>
      /// 每帧检测是否需要触发开火。
      /// </summary>
      public void Tick()
      {
         if (State.ControlTakenOver || !State.Input.FireHeld) return;

         var weaponMode = State.CurrentBattleData?.WeaponMode;
         weaponMode?.PressTrigger();
      }
   }
}
