using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeaGame
{
   /// <summary>
   /// T1 点按型触屏键（边沿语义：E/Q 技能、切人 ←/→、暂停、换弹、锁敌切换）。
   /// 以可序列化的 <see cref="kind"/> 区分行为，挂在 prefab 节点上即生效；
   /// 亦保留 <see cref="OnTap"/> 委托注入以便运行时定制。多指只认第一根按下手指。
   /// </summary>
   public enum TouchTapKind
   {
      None = 0,
      SkillE,
      SkillQ,
      /// <summary>切到上一名存活角色。</summary>
      RolePrev,
      /// <summary>切到下一名存活角色。</summary>
      RoleNext,
      Pause,
      Reload,
      LockSwitch,
   }

   public sealed class TouchTapButton : MonoBehaviour, IPointerDownHandler
   {
      [Tooltip("本键行为（prefab 序列化驱动）。")]
      [SerializeField] private TouchTapKind kind = TouchTapKind.None;

      /// <summary>可选委托（优先于 kind，供运行时定制）。</summary>
      public Action OnTap;

      public void Setup(TouchTapKind tapKind)
      {
         kind = tapKind;
      }

      public void Setup(Action onTap)
      {
         OnTap = onTap;
      }

      public void OnPointerDown(PointerEventData eventData)
      {
         if (OnTap != null)
         {
            OnTap();
            return;
         }

         switch (kind)
         {
            case TouchTapKind.SkillE: BattleVirtualInput.RequestSkillE(); break;
            case TouchTapKind.SkillQ: BattleVirtualInput.RequestSkillQ(); break;
            case TouchTapKind.RolePrev: BattleVirtualInput.RequestCycleRolePrev(); break;
            case TouchTapKind.RoleNext: BattleVirtualInput.RequestCycleRoleNext(); break;
            case TouchTapKind.Pause: BattleVirtualInput.RequestPauseToggle(); break;
            case TouchTapKind.Reload: BattleVirtualInput.RequestReload(); break;
            case TouchTapKind.LockSwitch: BattleVirtualInput.RequestLockSwitch(); break;
         }
      }
   }
}
