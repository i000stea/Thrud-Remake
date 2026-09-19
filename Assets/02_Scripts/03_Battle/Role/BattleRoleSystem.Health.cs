using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// <see cref="BattleRoleSystem"/> 的**成员生命部分**（partial；2026-09-19 由 BattleHealthSystem 合并进来）。
   ///
   /// **为什么合并**：两者共享同一领域不变量——「槽位 i 的成员是否可操控」= `!IsDowned` + 血量；
   /// 且原 `BattleHealthSystem` 要 `GetSystem&lt;BattleRoleSystem&gt;().HandleRoleDowned(index)` 回调选角，
   /// 形成系统间双向耦合。合并后：
   /// - 「扣血 → 阵亡 → 自动切人 → 全灭」这条链路在**一个类内闭环**，不再跨系统 `GetSystem`；
   /// - 事件订阅统一收口在 `BattleRoleSystem.OnInit`（本文件**不再有** OnInit/OnDeinit，避免 partial 冲突）；
   /// - 成员血量复位并入 `BattleRoleSystem.Initialize`。
   ///
   /// **同类的两个文件分工**：
   /// - `BattleRoleSystem.cs` —— 选角 / 阵亡切人 / 控制权接管 / 初始化；
   /// - 本文件 —— 受击结算 / 治疗 / 阵亡判定与全灭触发。
   ///
   /// 设计背景见 `Docs/03_专项设计与重构/19_表现层分层_View与Actor_设计.md`。
   /// </summary>
   // [调试提醒 2026-09-06] 伤害构成存在待查怀疑（玩家侧受击结算/成员归属/扣血与血条联动），
   // 改动或调试本区域前请先读 Docs/12_伤害构成_怀疑存档.md 并向用户确认复现与期望值。
   public sealed partial class BattleRoleSystem
   {
      /// <summary>
      /// 治疗当前出战成员（T2.4 技能效果调用；T3.1B 目标=当前成员）。
      /// 当前生命值不越过该成员最大生命值。
      /// </summary>
      /// <param name="amount">治疗量（内部截断为 ≥0）。</param>
      public void HealPlayer(float amount)
      {
         var index = State.SelectedRoleIndex.Value;
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

         var index = State.SelectedRoleIndex.Value;
         var data = State.CurrentBattleData;
         if (data == null || data.IsDowned) return;

         data.CurrentHealth = Mathf.Max(0f, data.CurrentHealth - e.Damage);
         Architecture.SendEvent(new BattleEvent_RoleHealthChanged { RoleIndex = index });

         if (data.CurrentHealth > 0f) return;

         // 当前成员阵亡：置阵亡态 → 广播阵亡事件 → 自动切下一存活
         data.IsDowned = true;
         Debug.Log($"[T3.1B] {data.RoleName} 阵亡（槽 {index + 1}）。");

         Architecture.SendEvent(new BattleEvent_RoleDowned { RoleIndex = index });

         // 合并前这里是跨系统调用（GetSystem<BattleRoleSystem>().HandleRoleDowned），现为同类内部调用
         if (HandleRoleDowned(index)) return;

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
