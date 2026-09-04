using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 技能施放上下文（T2.4 三层模型：执行器与公共效果的共享输入）。
   /// 由技能系统在按键命中、冷却与能量校验通过后装配；
   /// 执行器与效果层只读该对象，不反向修改状态模型。
   /// </summary>
   public sealed class BattleSkillContext
   {
      /// <summary>施放者角色索引。</summary>
      public int RoleIndex;

      /// <summary>施放者角色运行时数据。</summary>
      public BattleRoleRuntimeData Role;

      /// <summary>施放者控制器（场景装配/相机/预制引用入口）。</summary>
      public PlayerControl Controller;

      /// <summary>战斗状态模型（读取当前能量、锁敌、输入等共享状态）。</summary>
      public BattleStateModel State;

      /// <summary>本次施放的槽位（E / Q）。</summary>
      public BattleSkillSlot Slot;

      /// <summary>施放者技能档案（由技能系统按角色元素从目录注入）。</summary>
      public BattleRoleSkillProfile Profile;

      /// <summary>当前槽位的数值行。</summary>
      public BattleSkillSlotData SlotData => Profile?.GetSlot(Slot);

      /// <summary>施放者元素（决定弹幕/爆发携带的元素）。</summary>
      public DamageElement RoleElement =>
         Role?.RoleData != null ? Role.RoleData.damageElement : DamageElement.none;

      /// <summary>当前锁定目标的锁定点（未锁敌为 null）。</summary>
      public Transform LockTarget
      {
         get
         {
            var locked = State?.LockEnemy;
            return locked != null && locked.LockPoint ? locked.LockPoint : null;
         }
      }

      /// <summary>施放者根节点位置。</summary>
      public Vector3 Origin => Controller ? Controller.transform.position : Vector3.zero;

      /// <summary>
      /// 面向（自由瞄准时取相机正向；无相机回退控制器自身前向）。
      /// 弹幕方向/爆发放置点均以此为基础。
      /// </summary>
      public Vector3 AimForward
      {
         get
         {
            if (Controller && Controller.Control_Camera)
            {
               return Controller.Control_Camera.transform.forward;
            }

            return Controller ? Controller.transform.forward : Vector3.forward;
         }
      }

      /// <summary>武器开火点（弹幕出生点；无则回退控制器位置）。</summary>
      public Transform FirePoint
      {
         get
         {
            var weaponView = Role?.WeaponView;
            if (weaponView?.FirePoint != null)
            {
               return weaponView.FirePoint;
            }

            return Controller ? Controller.transform : null;
         }
      }
   }
}
