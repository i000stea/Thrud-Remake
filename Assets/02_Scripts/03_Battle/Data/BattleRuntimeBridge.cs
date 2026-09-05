using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗运行时桥接层。
   /// 该类用于向仍未完全迁移的旧组件，或需要脱离架构层读取上下文的表现组件，
   /// 暴露当前战斗的关键状态，例如相机、玩家位置、锁敌目标和伤害飘字控制器。
   /// </summary>
   public static class BattleRuntimeBridge
   {
      /// <summary>
      /// 当前桥接层的拥有者。
      /// 只有拥有者销毁时，桥接层才会真正清空，避免多实例场景下误删状态。
      /// </summary>
      private static Battle_PlayerControl sOwner;

      /// <summary>
      /// 当前战斗相机。
      /// </summary>
      public static Camera Camera { get; private set; }

      /// <summary>
      /// 玩家根节点。
      /// </summary>
      public static Transform PlayerTransform { get; private set; }

      /// <summary>
      /// 当前锁敌目标的 Transform。
      /// 若为空，表示当前没有锁定目标。
      /// </summary>
      public static Transform LockEnemyTransform { get; private set; }

      /// <summary>
      /// 当前锁敌速度。
      /// 旧武器表现仍可通过该值维持平滑朝向。
      /// </summary>
      public static float LockSpeed { get; private set; } = 1f;

      /// <summary>
      /// 战斗环境特效控制器。
      /// 供敌人受击时创建飘字、播放场景内基础反馈。
      /// </summary>
      public static BattleEnvironmentEffectController EnvironmentEffectController { get; private set; }

      /// <summary>
      /// T2.3 当前操控角色的元素（武器/子弹据此携带元素）。
      /// </summary>
      public static DamageElement ActiveElement { get; private set; } = DamageElement.none;

      /// <summary>
      /// 刷新桥接层状态。
      /// 该方法会在 `Battle_PlayerControl` 的生命周期中持续调用，确保桥接数据与当前战斗同步。
      /// </summary>
      public static void UpdateState(
         Battle_PlayerControl owner,
         Camera camera,
         Transform playerTransform,
         Transform lockEnemyTransform,
         float lockSpeed,
         BattleEnvironmentEffectController environmentEffectController,
         DamageElement activeElement)
      {
         sOwner = owner;
         Camera = camera;
         PlayerTransform = playerTransform;
         LockEnemyTransform = lockEnemyTransform;
         LockSpeed = Mathf.Max(0.01f, lockSpeed);
         EnvironmentEffectController = environmentEffectController;
         ActiveElement = activeElement;
      }

      /// <summary>
      /// 清理桥接层状态。
      /// 只有当前调用者正是桥接层拥有者时才会执行清空，避免误清理其他战斗实例的数据。
      /// </summary>
      public static void Clear(Battle_PlayerControl owner)
      {
         if (sOwner != owner) return;

         sOwner = null;
         Camera = null;
         PlayerTransform = null;
         LockEnemyTransform = null;
         LockSpeed = 1f;
         EnvironmentEffectController = null;
         ActiveElement = DamageElement.none;
      }
   }
}
