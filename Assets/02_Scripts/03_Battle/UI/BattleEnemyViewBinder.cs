using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 敌人头顶 UI 绑定器与屏幕可见性投影（B1 2026-09-21 从 <see cref="BattleEnemyEntity"/> 拆出）。
   ///
   /// **职责**：
   ///   1. 头顶 UI 实例的创建（`uiPrefab` / `uiYOffset` 仍序列化在实体上，本类只取用）；
   ///   2. 屏幕可见性——把锁定点投影到战斗相机视口，产出一个布尔结果。
   ///
   /// **不参与战斗规则**：可见性只作为 <see cref="IsOnCamera"/> 暴露；
   /// "是否可被锁定"的规则判定仍留在实体（<see cref="BattleEnemyEntity.CanBeLocked"/>）。
   ///
   /// 纯 C# 类：不在 prefab 上新增 MonoBehaviour、不改变任何序列化字段名与引用，
   /// 因此 20 个敌人 prefab 的 `uiPrefab` / `uiYOffset` 字段与 GUID 全部保持不变。
   /// </summary>
   public sealed class BattleEnemyViewBinder
   {
      private readonly BattleEnemyEntity mOwner;

      /// <summary>
      /// 构造绑定器。
      /// </summary>
      /// <param name="owner">宿主敌人实体（UI 挂到它子级，锁定点取自它）。</param>
      public BattleEnemyViewBinder(BattleEnemyEntity owner)
      {
         mOwner = owner;
      }

      /// <summary>
      /// 当前是否位于战斗相机视口内（由 <see cref="TickVisibility"/> 每帧写入）。
      /// </summary>
      public bool IsOnCamera { get; private set; }

      /// <summary>
      /// 已创建的头顶 UI 实例；缺少 `uiPrefab` 引用或 prefab 根未挂
      /// <see cref="Battle_EnemyUI"/> 时为 null。
      /// </summary>
      public Battle_EnemyUI HeadUi { get; private set; }

      /// <summary>
      /// 创建敌人头顶 UI（QFramework View：<see cref="Battle_EnemyUI"/>），挂到敌人子级。
      /// 缺少 <paramref name="uiPrefab"/> 引用时跳过，不影响敌人基础行为（与迁移前一致）。
      /// UI 数据改为自读宿主（元素/状态/血量），无需额外 Setup。
      /// </summary>
      /// <param name="uiPrefab">头顶 UI prefab（实体上序列化的 `uiPrefab`）。</param>
      /// <param name="yOffset">头顶 UI 的本地 Y 偏移（实体上序列化的 `uiYOffset`）。</param>
      public void CreateHeadUi(GameObject uiPrefab, float yOffset)
      {
         if (!uiPrefab || !mOwner) return;

         var uiObject = Object.Instantiate(uiPrefab, mOwner.transform);
         uiObject.transform.localPosition = Vector3.up * yOffset;

         if (!uiObject.TryGetComponent(out Battle_EnemyUI ui))
         {
            Debug.LogWarning($"敌人 {mOwner.name} 生成的 UI 预制体上缺少 Battle_EnemyUI 组件。");
            return;
         }

         HeadUi = ui;
      }

      /// <summary>
      /// 判断敌人是否位于当前战斗相机的可视范围内，结果写入 <see cref="IsOnCamera"/>。
      /// 相机取自 Model 的派生只读属性 `BattleStateModel.BattleCamera`。
      /// </summary>
      public void TickVisibility()
      {
         var camera = BattleArchitecture.Interface.GetModel<BattleStateModel>().BattleCamera;
         var lockPoint = mOwner ? mOwner.LockPoint : null;
         if (!camera || !lockPoint)
         {
            IsOnCamera = false;
            return;
         }

         var screenPoint = camera.WorldToViewportPoint(lockPoint.position);
         IsOnCamera = screenPoint.z > 0f
                      && screenPoint.x >= 0f && screenPoint.x <= 1f
                      && screenPoint.y >= 0f && screenPoint.y <= 1f;
      }
   }
}
