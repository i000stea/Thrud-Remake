using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 角色切换事件。
   /// 当玩家通过数字键切换当前操控角色时，由战斗系统派发该事件，
   /// 用于通知表现层刷新角色显隐、武器挂点与动画状态。
   /// </summary>
   public struct BattleEvent_RoleSwitched
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
   public struct BattleEvent_RollTriggered
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
   public struct BattleEvent_LockEnemyChanged
   {
      /// <summary>
      /// 当前锁定目标的根节点。
      /// 当值为 null 时，表示当前没有锁定任何敌人。
      /// </summary>
      public Transform EnemyTransform;
   }

   // 先受攻击，提供后续数值计算，然后再血量变化

   /// <summary>
   /// 玩家受击事件。
   /// 敌方伤害经此事件送入健康系统；T3.1B 起由健康系统结算到「当前出战角色」的成员级血量。
   /// </summary>
   public struct BattleEvent_PlayerDamaged
   {
      /// <summary> 本次受击造成的伤害值。 </summary>
      public int Damage;
   }

   /// <summary>
   /// 成员血量变化事件（T3.1B）。
   /// 健康系统扣血/治疗结算后派发，HUD/血条订阅后刷新该成员与当前出战成员表现。
   /// </summary>
   public struct BattleEvent_RoleHealthChanged
   {
      /// <summary> 血量发生变化的成员索引（槽位序=切人序）。 </summary>
      public int RoleIndex;
   }

   /// <summary>
   /// 成员阵亡事件（T3.1B）。
   /// 某成员血量归零、置 IsDowned 后派发；健康系统随后自动切下一存活成员，无存活才进入全灭判定。
   /// </summary>
   public struct BattleEvent_RoleDowned
   {
      /// <summary>
      /// 阵亡的成员索引。
      /// </summary>
      public int RoleIndex;
   }

   /// <summary>
   /// 玩家死亡事件。
   /// T3.1B 起语义收敛为「全队成员阵亡」→ 触发失败流程（关卡状态机 Defeat）；
   /// 整场限时失败不经此事件。单名成员阵亡不派发本事件。
   /// </summary>
   public struct BattleEvent_PlayerDied
   {
      /// <summary>
      /// 玩家死亡时所在的波次编号。
      /// 由关卡流程在后续阶段写入，首版默认为 0。
      /// </summary>
      public int WaveIndex;
   }

   /// <summary>
   /// 换弹完成事件。
   /// 当换弹计时结束、弹匣被补满时由武器系统派发，
   /// 供 HUD（T3.3）、换弹音效与动画占位消费。
   /// </summary>
   public struct BattleEvent_ReloadCompleted
   {
   }

   /// <summary>
   /// 玩家命中敌人事件（T2.4 技能能量来源之一）。
   /// 仅当玩家子弹真正命中新版敌人实体（<see cref="BattleEnemyEntity"/>）时派发；
   /// 擦弹、命中场景物、命中旧版敌人均不派发。
   /// 技能系统订阅该事件积攒少量能量；击杀另有 <see cref="BattleEvent_EnemyKilled"/>。
   /// </summary>
   public struct BattleEvent_EnemyHit
   {
      /// <summary>
      /// 本次命中实际结算的伤害（已含元素克制/易伤倍率）。
      /// </summary>
      public float Damage;

      /// <summary>
      /// 本次命中的攻击元素（子弹开火时写入；none=无元素）。
      /// </summary>
      public DamageElement Element;
   }

   /// <summary>
   /// 玩家击杀敌人事件（T2.4 技能能量的主要来源）。
   /// 仅当敌人因受到外部伤害（玩家子弹/技能）致死时派发；
   /// 地雷踩爆、导弹贴近引爆、导弹超时自毁等非玩家击杀不派发。
   /// </summary>
   public struct BattleEvent_EnemyKilled
   {
      /// <summary>
      /// 被击杀敌人销毁前的世界坐标快照（供击杀特效/聚怪结算定位）。
      /// </summary>
      public Vector3 Position;

      /// <summary>
      /// 被击杀敌人的自身元素（供元素联动类技能读取）。
      /// </summary>
      public DamageElement Element;
   }

   /// <summary>
   /// 技能能量变化事件（T2.4）。
   /// 能量增/减结算完毕后由技能系统派发，
   /// HUD 订阅后即时刷新 Q 行能量条，避免每帧轮询状态模型。
   /// </summary>
   public struct BattleEvent_SkillEnergyChanged
   {
      /// <summary>
      /// 变化后的当前能量值。
      /// </summary>
      public float Energy;

      /// <summary>
      /// 能量上限（通常为 <see cref="BattleStateModel.RoleEnergyMax"/>）。
      /// </summary>
      public float Max;
   }

   /// <summary>
   /// 技能槽就绪状态变化事件（T2.4）。
   /// 冷却进入/结束、能量达标/耗尽时由技能系统派发，
   /// HUD 订阅后即时刷新 E/Q 行的就绪与剩余冷却显示。
   /// </summary>
   public struct BattleEvent_SkillCooldownChanged
   {
      /// <summary>
      /// 技能槽位：0 = 元素战技 E；1 = 极奏 Q。
      /// </summary>
      public int Slot;

      /// <summary>
      /// 该槽当前是否就绪（可施放）。
      /// </summary>
      public bool Ready;

      /// <summary>
      /// 距就绪的剩余冷却秒数（就绪时为 0）。
      /// </summary>
      public float RemainSeconds;
   }
}
