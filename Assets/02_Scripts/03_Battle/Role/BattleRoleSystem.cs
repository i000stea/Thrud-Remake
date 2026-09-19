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

      private IUnRegister downed, health;

      protected override void OnInit()
      {
         // 阵亡 / 生命变化 → 只更新"当前出战成员"的 HUD 读数。
         // 角色显隐与翻滚演出**不再由本系统驱动**：BattleRoleActor 自己订阅
         // BattleEvent_RoleSwitched / RoleDowned / RollTriggered（19 文档第 1 步）。
         // 死亡事件
         downed = Architecture.RegisterEvent<BattleEvent_RoleDowned>(_ => PublishCurrentMemberHealth());
         // 生命
         health = Architecture.RegisterEvent<BattleEvent_RoleHealthChanged>(_ => PublishCurrentMemberHealth());
      }

      protected override void OnDeinit()
      { downed?.UnRegister(); health?.UnRegister(); }

      /// <summary>
      /// 把"当前出战成员"的血量读数同步给 HUD 视图状态。
      /// 写入点与血量变更同点（本系统负责选角，BattleHealthSystem 负责扣血/治疗）。
      /// </summary>
      private void PublishCurrentMemberHealth()
      {
         State.Hud.PublishHealth(State.CurrentBattleData);
      }

      /// <summary>
      /// 初始化角色系统。
      ///
      /// ⚠️ 仍存在一处 View 耦合待处理（19 文档 §3.2「最小引用注入」）：
      /// 入参是具体类型 <see cref="Battle_PlayerControl"/>，而本方法只用到它的
      /// `InitialRoleIndex` / `ControlTakeOff` / `PlayerConfig` 等少量引用。
      /// 目标形态是注入最小引用（或一个只读 `BattleRigRefs`）而非整台 rig。
      /// </summary>
      public void Initialize(Battle_PlayerControl controller, BattleRoleRuntimeData[] battleDatas)
      {
         State.Controller = controller;
         State.ControlTakenOver = controller != null && controller.ControlTakeOff;
         State.BattleDatas = battleDatas ?? System.Array.Empty<BattleRoleRuntimeData>();
         State.SelectedRoleIndex = Mathf.Clamp(controller != null ? controller.InitialRoleIndex : 0,
            0, Mathf.Max(0, State.BattleDatas.Length - 1));
         State.CameraPitch = 0f;
         State.LockEnemy = null;
         State.Hud.AimPosition.Value = Vector2.zero;
         State.LockSpeedMultiplier = 1f;
         State.Movement.LastRollTime = -Mathf.Max(0.01f, controller != null ? controller.PlayerConfig.GetRollCooldown() : 1f);
         State.Movement.ReadySprint = false;
         State.Movement.Sprinting = false;
         State.Movement.CachedForce = Vector3.zero;
         State.Movement.RollForce = Vector2.up;
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
         // 角色已死亡或数量为0
         if (State.BattleDatas == null || State.BattleDatas.Length == 0) return;

         // 切换目标溢出当前队伍序号
         if (index < 0 || index >= State.BattleDatas.Length) return;

         // 切换目标和当前显示一致，不需要切换，打回
         if (State.SelectedRoleIndex == index) return;

         // 切换目标
         var target = State.BattleDatas[index];

         // 目标为空或目标已死亡，无法切换，打回
         if (target == null || target.IsDowned) return;

         State.SelectedRoleIndex = index;
         PublishCurrentMemberHealth();
         // 主 HUD 视图状态（反向绑定数据源，见 BattleHudState）
         State.Hud.SelectedRoleIndex.Value = index;
         Architecture.SendEvent(new BattleEvent_RoleSwitched { Index = index });
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
            PublishCurrentMemberHealth();
            // 主 HUD 视图状态（反向绑定数据源，见 BattleHudState）
            State.Hud.SelectedRoleIndex.Value = index;
            Debug.Log($"[T3.1B] 自动切至存活成员：{candidate.RoleName}（槽 {index + 1}）。");
            Architecture.SendEvent(new BattleEvent_RoleSwitched { Index = index });
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
}