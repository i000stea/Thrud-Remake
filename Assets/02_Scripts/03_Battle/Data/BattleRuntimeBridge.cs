using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗运行时桥接层（**正在退役中，2026-09-19**）。
   ///
   /// **历史职责**：向尚未完成迁移的旧组件暴露当前战斗上下文（相机 / 玩家 / 锁敌 / 元素 / 飘字控制器）。
   /// **问题**：它只是 `BattleStateModel` + rig 引用的**静态可变镜像**——"双重真相"，
   /// 与已消灭的 `RoleEnergy` / `SelectedRoleIndex` 镜像同类，且读写跨 11 个文件、无法静态追踪。
   ///
   /// **已删除的成员（2026-09-19 第 1 批）**——它们本来就能从 Model 派生，改为读 `BattleStateModel` 的派生只读属性：
   /// - `Camera` → `BattleStateModel.BattleCamera`（读者：`Battle_EnemyUI` / `BattleEnemyEntity` / `BattleEnvironmentEffectController`）
   /// - `EnvironmentEffectController` → `BattleStateModel.EnvironmentEffects`（读者：`BattleEnemyEntity`）
   /// - `ActiveElement` → `BattleStateModel.CurrentDamageElement`，由 `BattleWeaponSystem` 每帧写入武器模式的 `ActiveElement`
   /// - `LockEnemyTransform` / `LockSpeed` → 随 `Weapon_ObjectComponent` 自驱 Update 退役而**失去全部读者**（删）
   ///
   /// **剩余唯一成员 `PlayerTransform`**（7 处读者：`BattleStageDirector` + 4 个 `IEnemyBehaviour` + `BattleEnemyEntity`）：
   /// 敌人"找玩家"本质是**目标选择**而非"要一个 Transform"，应改为 `IBattlePlayerTarget` 注册表
   /// （对称于既有的 `BattleEnemyRegistry`），届时本类可整体删除。
   /// ⚠️ 在此之前**不要新增成员**——每次新增都等于再造一份全局镜像。
   /// 设计与淘汰计划：`Docs/03_专项设计与重构/19_表现层分层_View与Actor_设计.md`。
   /// </summary>
   public static class BattleRuntimeBridge
   {
      /// <summary>
      /// 当前桥接层的拥有者。
      /// 只有拥有者销毁时，桥接层才会真正清空，避免多实例场景下误删状态。
      /// </summary>
      private static Battle_PlayerControl sOwner;

      /// <summary>
      /// 玩家根节点。
      /// </summary>
      public static Transform PlayerTransform { get; private set; }

      /// <summary>
      /// 刷新桥接层状态。
      /// 该方法会在 `Battle_PlayerControl` 的生命周期中持续调用，确保桥接数据与当前战斗同步。
      /// </summary>
      public static void UpdateState(Battle_PlayerControl owner, Transform playerTransform)
      {
         sOwner = owner;
         PlayerTransform = playerTransform;
      }

      /// <summary>
      /// 清理桥接层状态。
      /// 只有当前调用者正是桥接层拥有者时才会执行清空，避免误清理其他战斗实例的数据。
      /// </summary>
      public static void Clear(Battle_PlayerControl owner)
      {
         if (sOwner != owner) return;

         sOwner = null;
         PlayerTransform = null;
      }
   }
}
