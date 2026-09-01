using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 角色切换事件。
   /// 当玩家通过数字键切换当前操控角色时，由战斗系统派发该事件，
   /// 用于通知表现层刷新角色显隐、武器挂点与动画状态。
   /// </summary>
   public struct BattleRoleSwitchedEvent
   {
      /// <summary>
      /// 当前被激活的角色索引。
      /// 该索引与运行时角色数组一一对应。
      /// </summary>
      public int Index;
   }

   /// <summary>
   /// 翻滚触发事件。
   /// 该事件主要服务于动画层，用于同步翻滚方向与翻滚动画播放。
   /// </summary>
   public struct BattleRollTriggeredEvent
   {
      /// <summary>
      /// 翻滚方向。
      /// X 表示左右，Y 表示前后，数值已经在逻辑层归一化。
      /// </summary>
      public Vector2 RollForce;
   }

   /// <summary>
   /// 锁敌目标变化事件。
   /// 当系统重新选中敌人或取消锁敌时派发，
   /// 便于武器朝向、准星表现和调试工具同步刷新。
   /// </summary>
   public struct BattleLockEnemyChangedEvent
   {
      /// <summary>
      /// 当前锁定目标的根节点。
      /// 当值为 null 时，表示当前没有锁定任何敌人。
      /// </summary>
      public Transform EnemyTransform;
   }

   /// <summary>
   /// 玩家受击事件。
   /// 旧版逻辑中使用字符串通知广播玩家受击，
   /// 迁移后统一改为强类型事件，便于后续接入血量系统、音效和受击反馈。
   /// </summary>
   public struct BattlePlayerDamagedEvent
   {
      /// <summary>
      /// 本次受击造成的伤害值。
      /// </summary>
      public int Damage;
   }

   /// <summary>
   /// 玩家死亡事件。
   /// 当玩家当前生命值归零时，由健康系统派发该事件，
   /// 用于触发失败流程（关卡状态机）、死亡演出与埋点记录。
   /// </summary>
   public struct BattlePlayerDiedEvent
   {
      /// <summary>
      /// 玩家死亡时所在的波次编号。
      /// 由关卡流程在后续阶段写入，首版默认为 0。
      /// </summary>
      public int WaveIndex;
   }
}
