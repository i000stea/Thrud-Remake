using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 玩家健康系统（T3.1B 成员级血量版）。
   /// 职责：
   /// - 每名出战成员独立血量（<see cref="BattleRoleRuntimeData.CurrentHealth"/>，MaxHp 取自角色 SO）；
   /// - 受击只扣「当前出战角色」（<see cref="BattleStateModel.SelectedRoleIndex"/>）；
   /// - 当前角色血量归零 → IsDowned + 派发 <see cref="BattleEvent_RoleDowned"/> → 自动切下一存活；
   /// - 无存活成员才派发 <see cref="BattleEvent_PlayerDied"/>（全灭 → 关卡 Defeat），单成员阵亡不接管控制。
   /// </summary>
   // [调试提醒 2026-09-06] 伤害构成存在待查怀疑（玩家侧受击结算/成员归属/扣血与血条联动），
   // 改动或调试本区域前请先读 Docs/12_伤害构成_怀疑存档.md 并向用户确认复现与期望值。
   public sealed class BattleHealthSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      /// <summary> 受击事件反注册句柄。 </summary>
      private IUnRegister mDamagedUnregister;

      protected override void OnInit()
      {
         mDamagedUnregister = Architecture.RegisterEvent<BattleEvent_PlayerDamaged>(OnPlayerDamaged);
      }

      protected override void OnDeinit()
      {
         mDamagedUnregister?.UnRegister();
         mDamagedUnregister = null;
      }

      /// <summary> 初始化成员级血量：全体成员回满并清除阵亡态。 </summary>
      public void Initialize()
      {
         if (State.BattleDatas == null) return;

         for (var i = 0; i < State.BattleDatas.Length; i++)
         {
            State.BattleDatas[i]?.ResetHealth();
         }
      }

      /// <summary>
      /// 治疗当前出战成员（T2.4 技能效果调用；T3.1B 目标=当前成员）。
      /// 当前生命值不越过该成员最大生命值。
      /// </summary>
      /// <param name="amount">治疗量（内部截断为 ≥0）。</param>
      public void HealPlayer(float amount)
      {
         var index = State.SelectedRoleIndex;
         var data = State.CurrentBattleData;
         if (data == null || data.IsDowned) return;

         data.CurrentHealth = Mathf.Min(
            data.MaxHealth,
            data.CurrentHealth + Mathf.Max(0f, amount));
         Architecture.SendEvent(new BattleEvent_RoleHealthChanged { RoleIndex = index });
      }

      /// <summary>
      /// 处理玩家受击（结算到当前出战成员）。
      /// - 控制接管（终局/全灭演出）或关卡已终局时忽略伤害；
      /// - 当前成员归零 → 阵亡流程（自动切人 / 全灭 Defeat）。
      /// </summary>
      private void OnPlayerDamaged(BattleEvent_PlayerDamaged e)
      {
         if (State.ControlTakenOver) return;
         if (State.LevelState == BattleLevelState.Victory
             || State.LevelState == BattleLevelState.Defeat
             || State.LevelState == BattleLevelState.Exit)
         {
            return;
         }

         var index = State.SelectedRoleIndex;
         var data = State.CurrentBattleData;
         if (data == null || data.IsDowned) return;

         data.CurrentHealth = Mathf.Max(0f, data.CurrentHealth - e.Damage);
         Architecture.SendEvent(new BattleEvent_RoleHealthChanged { RoleIndex = index });

         if (data.CurrentHealth > 0f) return;

         // 当前成员阵亡：置阵亡态 → 广播阵亡事件 → 自动切下一存活
         data.IsDowned = true;
         Debug.Log($"[T3.1B] {data.RoleName} 阵亡（槽 {index + 1}）。");

         Architecture.SendEvent(new BattleEvent_RoleDowned { RoleIndex = index });

         var switched = Architecture.GetSystem<BattleRoleSystem>().HandleRoleDowned(index);
         if (switched) return;

         // 无存活成员 → 全灭：接管控制并触发 Defeat（BattleLevelFlow 消费）
         // 先同步控制器序列化字段，避免 PlayerControl.Update 每帧把接管状态写回 false
         if (State.Controller)
         {
            State.Controller.ControlTakeOff = true;
         }

         Debug.Log("[T3.1B] 全队阵亡 → 失败判定。");
         Architecture.SendEvent(new BattleEvent_PlayerDied());
      }
   }
}
