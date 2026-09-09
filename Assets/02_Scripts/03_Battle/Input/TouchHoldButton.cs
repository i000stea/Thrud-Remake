using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeaGame
{
   /// <summary>
   /// T1 按住型触屏键（长按语义：普攻 / 冲刺翻滚）。
   /// 以可序列化的 <see cref="kind"/> 区分行为，挂在 prefab 节点上即生效
   /// （Editor 生成器 BattleTouchInputPanelBuilder 负责装配）；
   /// 亦保留 <see cref="OnHeldChanged"/> 委托注入以便运行时定制。
   /// 多指互斥：只响应按下本键的那根手指。
   /// </summary>
   public enum TouchHoldKind
   {
      None = 0,
      /// <summary>普攻按住 → BattleVirtualInput.FireHeld。</summary>
      Fire,
      /// <summary>冲刺/翻滚按住 → BattleVirtualInput.SprintHeld（按住=持续，按下一刻另有 Sprinted 脉冲可选）。</summary>
      Sprint,
   }

   public sealed class TouchHoldButton : MonoBehaviour,
      IPointerDownHandler, IPointerUpHandler
   {
      [Tooltip("本键行为（prefab 序列化驱动）。")]
      [SerializeField] private TouchHoldKind kind = TouchHoldKind.Fire;

      /// <summary>可选委托（优先于 kind，供运行时定制）。</summary>
      public Action<bool> OnHeldChanged;

      private int mPointerId = int.MinValue;
      private bool mHeld;

      public void Setup(TouchHoldKind holdKind)
      {
         kind = holdKind;
      }

      public void Setup(Action<bool> onHeldChanged)
      {
         OnHeldChanged = onHeldChanged;
      }

      public void OnPointerDown(PointerEventData eventData)
      {
         if (mHeld) return;

         mPointerId = eventData.pointerId;
         SetHeld(true);
      }

      public void OnPointerUp(PointerEventData eventData)
      {
         if (!mHeld || eventData.pointerId != mPointerId) return;
         SetHeld(false);
      }

      private void OnDisable()
      {
         SetHeld(false);
      }

      private void SetHeld(bool held)
      {
         mHeld = held;
         if (!held) mPointerId = int.MinValue;

         if (OnHeldChanged != null)
         {
            OnHeldChanged(held);
            return;
         }

         switch (kind)
         {
            case TouchHoldKind.Fire:
               BattleVirtualInput.FireHeld = held;
               break;
            case TouchHoldKind.Sprint:
               BattleVirtualInput.SprintHeld = held;
               if (held)
               {
                  // 与键盘 Shift 的 GetKeyDown 语义对齐：按下瞬间请求翻滚/冲刺准备
                  // （BattleMovementSystem：SprintPressed 触发翻滚，SprintHeld 维持冲刺）
                  BattleVirtualInput.RequestRollOrSprint();
               }

               break;
         }
      }
   }
}
