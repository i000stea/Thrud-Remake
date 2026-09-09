using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T1 触屏虚拟输入源（13 Demo P3/P4：手机端在"采集层插入虚拟输入源"）。
   ///
   /// 职责：承载由触摸 UI（<see cref="BattleTouchInputPanel"/>）写入的虚拟输入值，
   /// 供 <c>BattleInputSystem</c> 在采集时读取——战斗逻辑（移动/镜头/武器/技能/切人）
   /// 只消费 <c>BattleInputSnapshot</c>，因此**零改动**；Editor 键鼠路径在虚拟源未启用时不受影响。
   ///
   /// 使用规则：
   /// - <see cref="Enabled"/>：触摸 UI 在场且激活时为 true（真机平台或 Editor 调试开关）。
   ///   仅在启用时，<c>BattleInputSystem</c> 用虚拟值覆盖键鼠值；
   /// - 连续量（摇杆轴 / 镜头拖拽累计）直接写字段；
   /// - 边沿量（技能/换弹/切人/暂停/锁敌/冲刺翻滚）用"置位 + 消费"脉冲，
   ///   由 InputSystem / AimSystem 每帧 <see cref="ConsumeXxx"/> 取走，取走后自动清零。
   ///   即使 UI 事件晚于本帧采集，下一帧仍能取到（最多延迟 1 帧，无丢事件）。
   /// </summary>
   public static class BattleVirtualInput
   {
      /// <summary>
      /// 镜头拖拽像素 → 等效输入轴换算（InputSnapshot.MouseX 语义为"鼠标轴值"，
      /// 相机系统会再乘 MouseSensitivity×dt）。真机手感微调项（13 §3.1 T1 微调清单）。
      /// </summary>
      public const float VirtualLookAxisScale = 0.06f;

      /// <summary>触摸层是否在场激活（真机或 Editor 启用）。</summary>
      public static bool Enabled { get; private set; }

      /// <summary>
      /// Editor 下是否启用触屏 UI（默认 **true**：编辑器内默认显示手机触屏层，便于调试）。
      /// 由 <c>Thrud/Battle/Touch Input/Enable in Editor</c> 菜单切换（本会话内生效）。
      /// </summary>
      public static bool EditorTouchEnabled = true;

      /// <summary>
      /// 当前是否应启用触屏层：真机平台，或编辑器下未关闭调试开关。
      /// </summary>
      public static bool ShouldEnableTouchUi =>
         Application.isMobilePlatform || (Application.isEditor && EditorTouchEnabled);

      // ---------------- 连续量（摇杆 / 镜头拖拽） ----------------

      /// <summary>虚拟摇杆水平轴（-1..1，向右为正，对应 InputSnapshot.MoveHorizontal）。</summary>
      public static float MoveX;

      /// <summary>虚拟摇杆纵轴（-1..1，向前为正，对应 InputSnapshot.MoveVertical）。</summary>
      public static float MoveY;

      /// <summary>本帧镜头水平拖拽累计像素量（正=右转，消费后清零，写入 InputSnapshot.MouseX）。</summary>
      public static float LookDeltaX;

      /// <summary>本帧镜头垂直拖拽累计像素量（正=上移视角，消费后清零，写入 InputSnapshot.MouseY）。</summary>
      public static float LookDeltaY;

      // ---------------- 边沿脉冲（置位 + 消费） ----------------

      /// <summary>普攻按住（区别于脉冲：长按语义，由 UI 每帧保持 true/false）。</summary>
      public static bool FireHeld;

      /// <summary>冲刺/翻滚按住（同普攻，长按语义）。</summary>
      public static bool SprintHeld;

      private static bool sSkillEPulse;
      private static bool sSkillQPulse;
      private static bool sReloadPulse;
      private static bool sSprintDownPulse;
      private static bool sCycleRoleNextPulse;
      private static bool sCycleRolePrevPulse;
      private static bool sPausePulse;
      private static bool sLockSwitchPulse;

      // ---------------- 启用控制 ----------------

      /// <summary>由触摸 UI 建立/销毁时调用。</summary>
      public static void SetEnabled(bool enabled)
      {
         Enabled = enabled;
         if (!enabled)
         {
            ClearAll();
         }
      }

      /// <summary>一次性事件触达时重置全部连续量（暂停/恢复/离场等）。</summary>
      public static void ClearAll()
      {
         MoveX = 0f;
         MoveY = 0f;
         LookDeltaX = 0f;
         LookDeltaY = 0f;
         FireHeld = false;
         SprintHeld = false;
         sSkillEPulse = false;
         sSkillQPulse = false;
         sReloadPulse = false;
         sSprintDownPulse = false;
         sCycleRoleNextPulse = false;
         sCycleRolePrevPulse = false;
         sPausePulse = false;
         sLockSwitchPulse = false;
      }

      /// <summary>每帧采集结束后调用：清空镜头拖拽累计（一次性消费量）。</summary>
      public static void EndFrame()
      {
         LookDeltaX = 0f;
         LookDeltaY = 0f;
      }

      // ---------------- UI 置位入口（BattleTouchInputPanel 子节点调用） ----------------

      public static void RequestSkillE() { sSkillEPulse = true; }
      public static void RequestSkillQ() { sSkillQPulse = true; }
      public static void RequestReload() { sReloadPulse = true; }
      public static void RequestRollOrSprint() { sSprintDownPulse = true; }
      public static void RequestCycleRoleNext() { sCycleRoleNextPulse = true; }
      public static void RequestCycleRolePrev() { sCycleRolePrevPulse = true; }
      public static void RequestPauseToggle() { sPausePulse = true; }
      public static void RequestLockSwitch() { sLockSwitchPulse = true; }

      // ---------------- 消费入口（InputSystem / AimSystem 调用） ----------------

      public static bool ConsumeSkillE()
      {
         var v = sSkillEPulse;
         sSkillEPulse = false;
         return v;
      }

      public static bool ConsumeSkillQ()
      {
         var v = sSkillQPulse;
         sSkillQPulse = false;
         return v;
      }

      public static bool ConsumeReload()
      {
         var v = sReloadPulse;
         sReloadPulse = false;
         return v;
      }

      public static bool ConsumeRollOrSprint()
      {
         var v = sSprintDownPulse;
         sSprintDownPulse = false;
         return v;
      }

      public static int ConsumeCycleRole()
      {
         if (sCycleRoleNextPulse)
         {
            sCycleRoleNextPulse = false;
            return 1;
         }

         if (sCycleRolePrevPulse)
         {
            sCycleRolePrevPulse = false;
            return -1;
         }

         return 0;
      }

      public static bool ConsumePauseToggle()
      {
         var v = sPausePulse;
         sPausePulse = false;
         return v;
      }

      public static bool ConsumeLockSwitch()
      {
         var v = sLockSwitchPulse;
         sLockSwitchPulse = false;
         return v;
      }
   }
}
