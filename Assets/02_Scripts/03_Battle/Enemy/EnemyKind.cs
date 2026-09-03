namespace TeaGame
{
   /// <summary>
   /// T2.1（2026-09-03 需求变更）敌人类型。
   /// BattleEnemyEntity 通过该枚举选择运行时行为策略（EnemyBehaviourFactory 工厂）。
   /// 规格：4 类普通敌人，均以弹幕/爆炸为对玩家伤害来源，固定姿态移动、不持续追踪；
   /// 无近战爪击、无位置盾；不做精英/Boss（Boss 归 T3.2）。
   /// </summary>
   public enum EnemyKind
   {
      /// <summary>巡弋者：以生成点为中心小半径游走，不追踪玩家，对玩家单发速射。</summary>
      Patrol = 0,

      /// <summary>站桩炮台：不移动；直线/扇形弹幕参数化（重装变体=高血慢速重弹）。</summary>
      GunTurret = 1,

      /// <summary>自爆·导弹：高速追踪玩家，贴近后小范围爆炸并自毁。</summary>
      Missile = 2,

      /// <summary>自爆·地雷：出生后静置；玩家进入感应半径或被击杀 → 较大范围爆炸。</summary>
      Mine = 3,
   }

   /// <summary>
   /// 敌人行为策略接口（T2.1 精简版）。
   /// 纯 C# 策略，非 MonoBehaviour——只在代码内被 BattleEnemyEntity 引用与调用，
   /// 不在 prefab/场景中序列化挂载，因此不受"挂载组件需独立文件"的纪律约束。
   /// </summary>
   public interface IEnemyBehaviour
   {
      /// <summary>
      /// 每帧移动逻辑（固定姿态：游走/站桩/追踪，不持续追踪玩家）。宿主 Update 调用。
      /// </summary>
      void TickMove(BattleEnemyEntity enemy);

      /// <summary>
      /// 每帧攻击逻辑（弹幕射击 / 引爆判定）。宿主 Update 调用。
      /// </summary>
      void TickAttack(BattleEnemyEntity enemy);

      /// <summary>
      /// 实体即将销毁（无论主动自爆还是被击杀）前的最终回调。
      /// 地雷借此在被击杀时引爆；需幂等（mBlasted 标志防二次结算）。
      /// </summary>
      void OnBeforeDestroy(BattleEnemyEntity enemy);
   }

   /// <summary>
   /// 敌人行为工厂。
   /// 依据 kind 返回对应策略；未知类型回退巡弋者，保证序列化数据缺省时行为安全。
   /// </summary>
   public static class EnemyBehaviourFactory
   {
      public static IEnemyBehaviour Create(EnemyKind kind)
      {
         switch (kind)
         {
            case EnemyKind.GunTurret: return new EnemyBehaviour_GunTurret();
            case EnemyKind.Missile: return new EnemyBehaviour_Missile();
            case EnemyKind.Mine: return new EnemyBehaviour_Mine();
            default: return new EnemyBehaviour_Patrol();
         }
      }
   }
}
