using QFramework;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗技能系统（T2.4 三层模型第一层：推pipe 管道）。
   ///
   /// 职责收口：
   /// 1. 能量经济：订阅命中/击杀事件积攒共享能量（写入 StateModel.RoleEnergy），
   ///    变化即广播 <see cref="BattleSkillEnergyChangedEvent"/>（HUD 反向 bind，不轮询）；
   /// 2. 冷却管理：按角色 × 槽维护冷却结束时间戳，冷却读秒以"整秒变化"节流广播
   ///    <see cref="BattleSkillCooldownChangedEvent"/>（避免每帧刷 UI）；
   /// 3. 施放分发：读取 E/Q 输入 → 冷却/能量校验 → 扣能量、进冷却 →
   ///    装配 <see cref="BattleSkillContext"/> → 按角色元素调用差异执行器。
   /// </summary>
   public sealed class BattleSkillSystem : AbstractSystem
   {
      /// <summary>槽位总数（E + Q）。</summary>
      private const int SlotCount = 2;

      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      private IUnRegister mHitUnregister;
      private IUnRegister mKilledUnregister;
      private IUnRegister mRoleSwitchedUnregister;

      /// <summary>各角色各槽位"冷却结束时间戳"。[角色索引, 槽位]</summary>
      private float[,] mReadyAt;

      /// <summary>各槽位上次广播的剩余整秒数（节流去重用）。</summary>
      private int[,] mLastRemainSeconds;

      /// <summary>各槽位上次广播的就绪标记（节流去重用）。</summary>
      private bool[,] mLastReady;

      protected override void OnInit()
      {
         mHitUnregister = Architecture.RegisterEvent<BattleEnemyHitEvent>(e => OnEnemyHit(e));
         mKilledUnregister = Architecture.RegisterEvent<BattleEnemyKilledEvent>(_ => OnEnemyKilled());
         mRoleSwitchedUnregister = Architecture.RegisterEvent<BattleRoleSwitchedEvent>(e => OnRoleSwitched(e));
      }

      protected override void OnDeinit()
      {
         mHitUnregister?.UnRegister();
         mHitUnregister = null;
         mKilledUnregister?.UnRegister();
         mKilledUnregister = null;
         mRoleSwitchedUnregister?.UnRegister();
         mRoleSwitchedUnregister = null;
      }

      /// <summary>
      /// 战斗开始重置（PlayerControl.Start 在角色系统初始化后调用）。
      /// 能量归零、冷却清空，并广播首帧 HUD 状态。
      /// </summary>
      public void ResetForBattle()
      {
         State.RoleEnergy = 0f;
         mReadyAt = null;
         mLastRemainSeconds = null;
         mLastReady = null;

         EnsureRuntime();
         BroadcastCurrentRoleCooldowns();
      }

      /// <summary>
      /// 每帧推进管道：冷却读秒广播 + 读取 E/Q 输入尝试施放。
      /// </summary>
      public void Tick()
      {
         EnsureRuntime();

         var roleIndex = State.SelectedRoleIndex;
         if (roleIndex >= 0 && roleIndex < mReadyAt.GetLength(0))
         {
            TickCooldownBroadcast(roleIndex);
         }

         if (State.ControlTakenOver) return;

         if (State.Input.SkillEPressed)
         {
            TryCast(BattleSkillSlot.Elemental);
         }

         if (State.Input.SkillQPressed)
         {
            TryCast(BattleSkillSlot.Ultimate);
         }
      }

      /// <summary>命中敌人 → 少量能量。</summary>
      private void OnEnemyHit(BattleEnemyHitEvent e)
      {
         AddEnergy(BattleSkillCatalog.EnergyGainPerHit);
      }

      /// <summary>玩家击杀敌人 → 大额能量。</summary>
      private void OnEnemyKilled()
      {
         AddEnergy(BattleSkillCatalog.EnergyGainPerKill);
      }

      /// <summary>角色切换 → 广播新角色 E/Q 槽冷却状态（HUD 刷新）。</summary>
      private void OnRoleSwitched(BattleRoleSwitchedEvent e)
      {
         BroadcastCurrentRoleCooldowns();
      }

      /// <summary>
      /// 尝试施放指定槽位。
      /// 校验顺序：控制接管 → 角色有效 → 档案/执行器存在 → 冷却 → 能量成本。
      /// </summary>
      private bool TryCast(BattleSkillSlot slot)
      {
         var roleIndex = State.SelectedRoleIndex;
         var battleDatas = State.BattleDatas;
         if (battleDatas == null || roleIndex < 0 || roleIndex >= battleDatas.Length)
         {
            return false;
         }

         var role = battleDatas[roleIndex];
         var element = role?.RoleData != null ? role.RoleData.damageElement : DamageElement.none;

         var profile = BattleSkillCatalog.Get(element);
         if (profile == null)
         {
            return false; // 无档案角色：技能禁用（静默）
         }

         var executor = BattleSkillExecutorFactory.Create(element);
         if (executor == null)
         {
            return false;
         }

         EnsureRuntime();
         var slotIndex = (int)slot;
         if (Time.time < mReadyAt[roleIndex, slotIndex])
         {
            return false; // 冷却中
         }

         var slotData = profile.GetSlot(slot);
         var energyCost = Mathf.Max(0f, slotData.EnergyCost);
         if (energyCost > 0f && State.RoleEnergy < energyCost)
         {
            return false; // 能量不足
         }

         // 结算成本：扣能量 → 进冷却
         if (energyCost > 0f)
         {
            AddEnergy(-energyCost);
         }

         mReadyAt[roleIndex, slotIndex] = Time.time + Mathf.Max(0.05f, slotData.Cooldown);
         BroadcastSlot(roleIndex, slotIndex);

         // 装配上下文并交给差异执行器
         var ctx = new BattleSkillContext
         {
            RoleIndex = roleIndex,
            Role = role,
            Controller = State.Controller,
            State = State,
            Slot = slot,
            Profile = profile
         };

         if (slot == BattleSkillSlot.Ultimate)
         {
            executor.CastUltimate(ctx);
         }
         else
         {
            executor.CastElemental(ctx);
         }

         Debug.Log($"[T2.4] {role?.RoleName ?? "?"} 施放{(slot == BattleSkillSlot.Ultimate ? "极奏Q" : "元素战技E")}。");
         return true;
      }

      /// <summary>写入能量并广播变化（0 变化静默）；Q 就绪状态受能量影响，同时刷新。</summary>
      private void AddEnergy(float delta)
      {
         var max = BattleStateModel.RoleEnergyMax;
         var next = Mathf.Clamp(State.RoleEnergy + delta, 0f, max);
         if (Mathf.Approximately(next, State.RoleEnergy))
         {
            return;
         }

         State.RoleEnergy = next;
         Architecture.SendEvent(new BattleSkillEnergyChangedEvent
         {
            Energy = next,
            Max = max
         });

         var roleIndex = State.SelectedRoleIndex;
         if (roleIndex >= 0)
         {
            BroadcastSlot(roleIndex, (int)BattleSkillSlot.Ultimate);
         }
      }

      /// <summary>广播当前角色的 E/Q 槽状态（切人/开战时调用）。</summary>
      private void BroadcastCurrentRoleCooldowns()
      {
         var roleIndex = State.SelectedRoleIndex;
         if (roleIndex < 0 || roleIndex >= mReadyAt.GetLength(0))
         {
            return;
         }

         for (var slot = 0; slot < SlotCount; slot++)
         {
            BroadcastSlot(roleIndex, slot);
         }
      }

      /// <summary>当前角色指定槽是否就绪（含极奏能量门槛）。HUD 查询用，不驱动广播。</summary>
      public bool IsSlotReady(BattleSkillSlot slot)
      {
         var roleIndex = State.SelectedRoleIndex;
         EnsureRuntime();
         if (roleIndex < 0 || mReadyAt == null || roleIndex >= mReadyAt.GetLength(0))
         {
            return false;
         }

         var slotIndex = (int)slot;
         if (Time.time < mReadyAt[roleIndex, slotIndex])
         {
            return false;
         }

         // 无档案/无执行器的角色（如 none 元素）技能禁用
         if (BattleSkillCatalog.Get(GetRoleElement(roleIndex)) == null)
         {
            return false;
         }

         // 极奏还需能量达标
         if (slot == BattleSkillSlot.Ultimate)
         {
            var element = GetRoleElement(roleIndex);
            var profile = BattleSkillCatalog.Get(element);
            var energyCost = profile?.Ultimate.EnergyCost ?? 0f;
            if (energyCost > 0f && State.RoleEnergy < energyCost)
            {
               return false;
            }
         }

         return true;
      }

      /// <summary>当前角色指定槽的剩余冷却秒数（就绪为 0）。HUD 查询用。</summary>
      public float GetSlotRemainSeconds(BattleSkillSlot slot)
      {
         var roleIndex = State.SelectedRoleIndex;
         EnsureRuntime();
         if (roleIndex < 0 || mReadyAt == null || roleIndex >= mReadyAt.GetLength(0))
         {
            return 0f;
         }

         var readyAt = mReadyAt[roleIndex, (int)slot];
         return Mathf.Max(0f, readyAt - Time.time);
      }

      /// <summary>当前角色极奏槽的能量缺口（冷却就绪但能量不足时 &gt;0；其余为 0）。HUD 查询用。</summary>
      public float GetSlotEnergyDeficit(BattleSkillSlot slot)
      {
         if (slot != BattleSkillSlot.Ultimate)
         {
            return 0f;
         }

         var roleIndex = State.SelectedRoleIndex;
         EnsureRuntime();
         if (roleIndex < 0 || mReadyAt == null || roleIndex >= mReadyAt.GetLength(0))
         {
            return 0f;
         }

         var element = GetRoleElement(roleIndex);
         var profile = BattleSkillCatalog.Get(element);
         var energyCost = profile?.Ultimate.EnergyCost ?? 0f;
         return Mathf.Max(0f, energyCost - State.RoleEnergy);
      }

      /// <summary>冷却读秒广播：仅整秒变化 / 就绪翻转时派发（节流，避免每帧刷 UI）。</summary>
      private void TickCooldownBroadcast(int roleIndex)
      {
         for (var slot = 0; slot < SlotCount; slot++)
         {
            var remainSeconds = Mathf.Max(0f, mReadyAt[roleIndex, slot] - Time.time);
            var ready = remainSeconds <= 0f && ComputeReadyState(roleIndex, slot);
            var remainInt = ready ? 0 : Mathf.Max(1, Mathf.CeilToInt(remainSeconds));

            if (remainInt == mLastRemainSeconds[roleIndex, slot] && ready == mLastReady[roleIndex, slot])
            {
               continue;
            }

            BroadcastSlot(roleIndex, slot);
         }
      }

      /// <summary>
      /// 广播单个槽位状态（含能量门槛判定）。
      /// 冷却结束仅是"基础就绪"；极奏还需能量达标才真正可施放。
      /// </summary>
      private void BroadcastSlot(int roleIndex, int slotIndex)
      {
         if (mReadyAt == null || roleIndex < 0 || roleIndex >= mReadyAt.GetLength(0))
         {
            return;
         }

         var ready = ComputeReadyState(roleIndex, slotIndex);
         var remainSeconds = Mathf.Max(0f, mReadyAt[roleIndex, slotIndex] - Time.time);

         mLastRemainSeconds[roleIndex, slotIndex] = ready ? 0 : Mathf.Max(1, Mathf.CeilToInt(remainSeconds));
         mLastReady[roleIndex, slotIndex] = ready;

         Architecture.SendEvent(new BattleSkillCooldownChangedEvent
         {
            Slot = slotIndex,
            Ready = ready,
            RemainSeconds = remainSeconds
         });
      }

      /// <summary>
      /// 就绪综合判定：冷却时间到 且（极奏）能量达标。
      /// 供广播节流与查询共用，保证两者判定一致，避免"能量不足"期间每帧重复广播。
      /// </summary>
      private bool ComputeReadyState(int roleIndex, int slotIndex)
      {
         if (mReadyAt == null || roleIndex < 0 || roleIndex >= mReadyAt.GetLength(0))
         {
            return false;
         }

         if (Time.time < mReadyAt[roleIndex, slotIndex])
         {
            return false;
         }

         // 无档案/无执行器的角色（如 none 元素）技能禁用
         if (BattleSkillCatalog.Get(GetRoleElement(roleIndex)) == null)
         {
            return false;
         }

         if (slotIndex == (int)BattleSkillSlot.Ultimate)
         {
            var element = GetRoleElement(roleIndex);
            var profile = BattleSkillCatalog.Get(element);
            var energyCost = profile?.Ultimate.EnergyCost ?? 0f;
            if (energyCost > 0f && State.RoleEnergy < energyCost)
            {
               return false;
            }
         }

         return true;
      }

      private DamageElement GetRoleElement(int roleIndex)
      {
         var battleDatas = State.BattleDatas;
         if (battleDatas == null || roleIndex < 0 || roleIndex >= battleDatas.Length)
         {
            return DamageElement.none;
         }

         var data = battleDatas[roleIndex];
         return data?.RoleData != null ? data.RoleData.damageElement : DamageElement.none;
      }

      /// <summary>确保冷却/节流数组与当前角色数量匹配（战斗初始化后惰性扩容）。</summary>
      private void EnsureRuntime()
      {
         var count = State?.BattleDatas?.Length ?? 0;
         if (count <= 0)
         {
            return;
         }

         if (mReadyAt != null && mReadyAt.GetLength(0) >= count)
         {
            return;
         }

         mReadyAt = new float[count, SlotCount];
         mLastRemainSeconds = new int[count, SlotCount];
         mLastReady = new bool[count, SlotCount];

         for (var i = 0; i < count; i++)
         {
            for (var slot = 0; slot < SlotCount; slot++)
            {
               mReadyAt[i, slot] = float.NegativeInfinity; // 初始即就绪
               mLastRemainSeconds[i, slot] = -1;           // 强制首轮广播
            }
         }
      }
   }
}
