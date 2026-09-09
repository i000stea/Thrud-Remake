using UnityEngine;
using UnityEngine.EventSystems;

namespace TeaGame
{
   /// <summary>
   /// T1 虚拟摇杆（13 Demo P3：左下移动摇杆）。
   /// 挂到"摇杆热区"Image 上；按下后在热区内拖动，输出 -1..1 轴向到
   /// <see cref="BattleVirtualInput.MoveX"/>/<see cref="BattleVirtualInput.MoveY"/>。
   /// 抬起/离开重置为 0。多指互斥：记录按下手指 pointerId，其它手指不干扰。
   /// </summary>
   public sealed class TouchJoystick : MonoBehaviour,
      IPointerDownHandler, IPointerUpHandler, IDragHandler
   {
      [SerializeField] private RectTransform knob;
      [SerializeField] private float radius = 120f;

      private int mPointerId = int.MinValue;
      private Vector2 mCenter;
      private bool mActive;

      public void Setup(RectTransform knobTransform, float knobRadius)
      {
         knob = knobTransform;
         radius = Mathf.Max(1f, knobRadius);
      }

      public void OnPointerDown(PointerEventData eventData)
      {
         if (mActive) return; // 已有手指在摇杆上，忽略第二指

         mPointerId = eventData.pointerId;
         mActive = true;
         mCenter = eventData.position;
         if (knob) knob.position = mCenter;
         BattleVirtualInput.MoveX = 0f;
         BattleVirtualInput.MoveY = 0f;
      }

      public void OnDrag(PointerEventData eventData)
      {
         if (!mActive || eventData.pointerId != mPointerId) return;

         var offset = eventData.position - mCenter;
         var clamped = Vector2.ClampMagnitude(offset, radius);
         if (knob) knob.position = mCenter + clamped;

         // 上=前(MoveVertical+)，右=侧(MoveHorizontal+)，与 WASD 轴语义一致
         BattleVirtualInput.MoveX = clamped.x / radius;
         BattleVirtualInput.MoveY = clamped.y / radius;
      }

      public void OnPointerUp(PointerEventData eventData)
      {
         if (!mActive || eventData.pointerId != mPointerId) return;

         ResetStick();
      }

      private void OnDisable()
      {
         ResetStick();
      }

      private void ResetStick()
      {
         mActive = false;
         mPointerId = int.MinValue;
         BattleVirtualInput.MoveX = 0f;
         BattleVirtualInput.MoveY = 0f;
         if (knob) knob.localPosition = Vector3.zero;
      }
   }
}
