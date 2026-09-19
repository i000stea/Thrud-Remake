using UnityEngine;

namespace TeaGame
{
   /// <summary>武器外观瞄准与观察点，不接管发射、弹药和投射物。</summary>
   public sealed class BattleWeaponActor
   {
      private readonly Transform lookMarker;
      /// <summary>瞄准参考相机（构造期注入；缺失时回落 <see cref="yawAxis"/>，再回落 <see cref="root"/>）。</summary>
      private readonly Camera camera;
      /// <summary>水平控制轴（相机缺失时的回正点兜底）。</summary>
      private readonly Transform yawAxis;
      /// <summary>rig 根节点（最终兜底的回正点）。</summary>
      private readonly Transform root;
      /// <summary>战斗状态（**只读**：锁敌目标 / 吸附倍率 / 当前成员）。</summary>
      private readonly BattleStateModel state;
      /// <summary>表现参数（锁敌吸附基础速度）。注入引用而非快照值：Play 中调参仍即时生效。</summary>
      private readonly BattlePlayerConfig config;

      /// <summary>
      /// 依赖集合（21 文档纪律 4：构造期注入，一眼看全；运行期只读，不做任何查找）。
      /// </summary>
      public BattleWeaponActor(Transform lookMarker, Camera camera, Transform yawAxis, Transform root,
         BattleStateModel state, BattlePlayerConfig config)
      {
         this.lookMarker = lookMarker;
         this.camera = camera;
         this.yawAxis = yawAxis;
         this.root = root;
         this.state = state;
         this.config = config;
      }

      /// <summary>
      /// 每帧刷新武器朝向（2026-09-19 第 3 步：中间量内聚）。
      ///
      /// **三个派生中间量在本类内算出**（原宿主 `TickActors()` 的 486–503 行）：
      /// 锁定目标点 `target`、未锁定的回正观察点 `freePoint`、吸附速度 `lockSpeed`。
      /// 子弹发射基准（`FreeAimRotation`）不在此处，仍由 <see cref="BattleWeaponSystem"/> 每帧写入武器模式。
      /// </summary>
      public void Tick()
      {
         var member = state?.CurrentBattleData;
         var lockEnemy = state?.LockEnemy;
         var target = lockEnemy.IsAlive() ? lockEnemy.LockPoint : null;

         // 未锁定的回正观察点：相机正前 10m（无相机退回落控制轴，再退回落 rig 自身）
         var freePoint = camera ? camera.transform.position + camera.transform.forward * 10f
            : yawAxis ? yawAxis.position + yawAxis.forward * 10f
            : root ? root.position : Vector3.zero;

         var lockSpeed = config
            ? config.LockEnemySpeed * Mathf.Max(1f, state?.LockSpeedMultiplier ?? 1f)
            : 10f;

         Aim(member, target, freePoint, lockSpeed);
      }

      /// <summary>把瞄准结果交给武器视图（锁定时吸附目标，未锁定时收敛回正）。</summary>
      public void Aim(BattleRoleRuntimeData member, Transform target, Vector3 freeLookPoint, float speed)
      {
         if (lookMarker) lookMarker.position = target ? target.position : freeLookPoint;
         member?.WeaponView?.RefreshAim(target, speed);
      }
   }
}
