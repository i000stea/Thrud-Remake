using System.Collections.Generic;
using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗敌人目标接口。
   /// 锁敌系统、子弹命中系统与 UI 表现都只依赖这个抽象，
   /// 从而避免直接绑定某一个具体的敌人实现。
   /// </summary>
   public interface IBattleEnemyTarget
   {
      /// <summary>
      /// 敌人的根节点。
      /// 主要用于计算世界距离、敌人朝向以及发射物目标点。
      /// </summary>
      Transform RootTransform { get; }

      /// <summary>
      /// 锁敌参考点。
      /// 一般是敌人的胸口、头部或自定义锁定锚点。
      /// 若没有显式配置，则通常退化为 RootTransform。
      /// </summary>
      Transform LockPoint { get; }

      /// <summary>
      /// 当前敌人是否允许被锁定。
      /// 该值会综合敌人是否激活、是否在镜头中等条件。
      /// </summary>
      bool CanBeLocked { get; }

      /// <summary>
      /// 对敌人施加伤害。
      /// 具体的扣血、受击表现、死亡逻辑由各自实现负责。
      /// </summary>
      /// <param name="damage">伤害值。</param>
      void ApplyDamage(float damage);
   }

   /// <summary>
   /// 武器表现接口。
   /// 该接口统一了新武器组件与旧武器组件的访问方式，
   /// 使数据装配层可以通过适配器桥接旧 prefab，而无需强制一次性替换所有资源。
   /// </summary>
   public interface IBattleWeaponView
   {
      /// <summary>
      /// 武器根节点。
      /// 用于角色切换时控制显隐、设置父节点和执行朝向。
      /// </summary>
      Transform RootTransform { get; }

      /// <summary>
      /// 子弹发射点。
      /// 所有弹道与射线起点都以该挂点为准。
      /// </summary>
      Transform FirePoint { get; }

      /// <summary>
      /// 子弹预制体。
      /// 用于物理弹道类武器生成飞行中的子弹对象。
      /// </summary>
      GameObject BulletPrefab { get; }

      /// <summary>
      /// 线性弹道预制体。
      /// 狙击、激光等武器可用该对象承载拖尾或 LineRenderer 效果。
      /// </summary>
      GameObject BulletLinePrefab { get; }

      /// <summary>
      /// 播放枪口火焰等开火表现。
      /// </summary>
      void PlayMuzzleFire();

      /// <summary>
      /// 刷新武器朝向。
      /// 新组件由战斗系统显式驱动，旧组件则可以在适配器中选择忽略，
      /// 继续复用自身已有的 Update 逻辑。
      /// </summary>
      /// <param name="lookTarget">当前瞄准目标。</param>
      /// <param name="lockSpeed">瞄准旋转速度。</param>
      void RefreshAim(Transform lookTarget, float lockSpeed);
   }

   /// <summary>
   /// 战斗公共工具集。
   /// 这里保留一些战斗层高频使用、但又不适合挂在具体系统中的通用方法。
   /// </summary>
   public static class BattleCombatUtility
   {
      /// <summary>
      /// 从当前节点开始，沿父节点链向上查找指定组件。
      /// 该方法与旧版工具保持一致，便于命中检测与碰撞体适配。
      /// </summary>
      /// <typeparam name="T">目标组件类型。</typeparam>
      /// <param name="target">起始节点。</param>
      /// <param name="maxDepth">向上查找的最大层级。</param>
      /// <returns>查找到的组件；若不存在则返回 null。</returns>
      public static T FindParentComponent<T>(this Transform target, int maxDepth = 9) where T : Component
      {
         for (var i = 0; i < maxDepth; i++)
         {
            if (target == null) break;

            if (target.TryGetComponent(out T component))
            {
               return component;
            }

            target = target.parent;
         }

         return null;
      }
   }

   /// <summary>
   /// 战斗敌人注册表。
   /// 新版敌人会主动注册到这里，锁敌系统优先读取该注册表。
   /// 若当前场景仍然存在旧版敌人脚本，则会退回到旧静态表进行兼容。
   /// </summary>
   public static class BattleEnemyRegistry
   {
      /// <summary>
      /// 新版敌人运行时列表。
      /// 该列表只保存已经完成注册的新版敌人实例。
      /// </summary>
      private static readonly List<BattleEnemyEntity> sBattleEnemies = new();

      /// <summary>
      /// 注册新版敌人。
      /// </summary>
      public static void Register(BattleEnemyEntity enemy)
      {
         if (!enemy || sBattleEnemies.Contains(enemy)) return;
         sBattleEnemies.Add(enemy);
      }

      /// <summary>
      /// 注销新版敌人。
      /// </summary>
      public static void UnRegister(BattleEnemyEntity enemy)
      {
         if (!enemy) return;
         sBattleEnemies.Remove(enemy);
      }

      /// <summary>
      /// 清空注册表。
      /// 场景卸载或调试重置时可调用该方法。
      /// </summary>
      public static void Clear()
      {
         sBattleEnemies.Clear();
      }

      /// <summary>
      /// 枚举当前可参与锁敌的目标。
      /// 若新版敌人列表为空，则回退到旧版敌人静态表，以保证迁移过程可逐步进行。
      /// </summary>
      public static IEnumerable<IBattleEnemyTarget> EnumerateTargets()
      {
         if (sBattleEnemies.Count > 0)
         {
            for (var i = 0; i < sBattleEnemies.Count; i++)
            {
               var enemy = sBattleEnemies[i];
               if (enemy) yield return enemy;
            }

            yield break;
         }

         for (var i = 0; i < EnemyControl.enemys.Count; i++)
         {
            var legacyEnemy = EnemyControl.enemys[i];
            if (legacyEnemy) yield return new LegacyBattleEnemyTarget(legacyEnemy);
         }
      }
   }

   /// <summary>
   /// 旧版敌人适配器。
   /// 该适配器只在过渡期工作，用于让新版锁敌系统和伤害系统读取旧敌人对象。
   /// </summary>
   public sealed class LegacyBattleEnemyTarget : IBattleEnemyTarget
   {
      /// <summary>
      /// 被包装的旧版敌人实例。
      /// </summary>
      private readonly EnemyEntity_Main mLegacyEnemy;

      /// <summary>
      /// 构造适配器时缓存旧敌人引用。
      /// </summary>
      public LegacyBattleEnemyTarget(EnemyEntity_Main legacyEnemy)
      {
         mLegacyEnemy = legacyEnemy;
      }

      /// <inheritdoc />
      public Transform RootTransform => mLegacyEnemy ? mLegacyEnemy.transform : null;

      /// <inheritdoc />
      public Transform LockPoint => RootTransform;

      /// <inheritdoc />
      public bool CanBeLocked => mLegacyEnemy && mLegacyEnemy.onCame;

      /// <inheritdoc />
      public void ApplyDamage(float damage)
      {
         if (!mLegacyEnemy) return;
         mLegacyEnemy.BeHit(damage);
      }
   }
}
