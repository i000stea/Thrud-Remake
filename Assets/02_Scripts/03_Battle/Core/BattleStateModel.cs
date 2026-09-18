using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗状态模型。
   /// 所有角色控制系统共享的运行时状态都保存在这里，
   /// 这样输入、移动、瞄准、武器与表现系统就可以围绕同一份数据协作。
   /// </summary>
   public sealed class BattleStateModel : AbstractModel
   {
      /// <summary> 当前战斗控制器。 </summary>
      public Battle_PlayerControl Controller;

      /// <summary> 当前战斗角色运行时数据集合。 </summary>
      public BattleRoleRuntimeData[] BattleDatas = System.Array.Empty<BattleRoleRuntimeData>();

      /// <summary>
      /// 每帧输入快照。
      /// 输入系统只负责写入，其他系统只负责读取。
      /// </summary>
      public readonly BattleInputSnapshot Input = new();

      /// <summary>
      /// 移动系统运行时状态。
      /// 包括缓存推力、翻滚方向、冲刺准备态等。
      /// </summary>
      public readonly BattleMovementState Movement = new();

      /// <summary>
      /// 当前选中的角色索引。
      /// </summary>
      public int SelectedRoleIndex;

      /// <summary>
      /// T2.4 技能能量上限（命中/击杀增长，极奏 Q 消耗）。
      /// </summary>
      public const float RoleEnergyMax = 100f;

      /// <summary>
      /// T2.4 当前技能能量（0~<see cref="RoleEnergyMax"/>）。
      /// </summary>
      public float RoleEnergy;

      /// <summary>
      /// T3.1 当前关卡流程状态（Prepare/Wave/Victory/Defeat/Exit）。
      /// 由 BattleLevelFlow 状态机写入，HUD/表现层只读。
      /// </summary>
      public BattleLevelState LevelState = BattleLevelState.Exit;

      /// <summary>
      /// T3.1 暂停标记（ESC 暂停菜单：Time.timeScale=0，进程不暂停）。
      /// 暂停期间战斗输入/物理/表现不推进，UI 菜单可点击。
      /// </summary>
      public bool Paused;

      /// <summary>
      /// 当前是否处于控制接管状态。
      /// 例如过场动画、暂停、技能演出、全灭终局等都可能接管控制。
      /// </summary>
      public bool ControlTakenOver;

      /// <summary>
      /// 相机俯仰角缓存。
      /// </summary>
      public float CameraPitch;

      /// <summary>
      /// 当前实际使用的视场角。
      /// </summary>
      public float CurrentFov;

      /// <summary> 当前锁定的敌人目标。 </summary>
      public IBattleEnemyTarget LockEnemy;

      /// <summary> 锁敌目标在 UI 中的目标位置。 </summary>
      public Vector2 ScreenTarget;

      /// <summary>
      /// 锁敌插值速度倍率。
      /// 根据准星距目标的远近动态调整。
      /// </summary>
      public float LockSpeedMultiplier = 1f;

      /// <summary>
      /// 当前正在操控的角色数据。
      /// 若当前没有有效角色，则返回 null。
      /// </summary>
      public BattleRoleRuntimeData CurrentBattleData
      {
         get
         {
            if (BattleDatas == null || BattleDatas.Length == 0) return null;
            var safeIndex = Mathf.Clamp(SelectedRoleIndex, 0, BattleDatas.Length - 1);
            return BattleDatas[safeIndex];
         }
      }

      protected override void OnInit()  { }
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

      /// <summary> 是否处于“翻滚后允许进入冲刺”的准备状态。 </summary>
      public bool ReadySprint;

      /// <summary> 当前是否已经进入冲刺状态。</summary>
      public bool Sprinting;

      /// <summary> 上一次执行翻滚的时间。 </summary>
      public float LastRollTime = -999f;
   }
}
