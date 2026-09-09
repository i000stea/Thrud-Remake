using UnityEngine;
using UnityEngine.EventSystems;

namespace TeaGame
{
   /// <summary>
   /// T1 中央拖拽转镜头层（13 Demo P3：屏中约 40% 宽区域单指拖拽 = 镜头旋转）。
   /// 把拖拽像素量累加到 <see cref="BattleVirtualInput.LookDeltaX"/>/<see cref="BattleVirtualInput.LookDeltaY"/>
   /// （正=右/上），由 BattleInputSystem 在下一帧采集为 MouseX/MouseY 并清零。
   /// 多指互斥：每个手指独立累计，互不干扰（与摇杆/按钮分属不同热区，天然隔离）。
   /// </summary>
   public sealed class TouchDragLookZone : MonoBehaviour,
      IPointerDownHandler, IPointerUpHandler, IDragHandler
   {
      private bool mDragging;

      public void OnPointerDown(PointerEventData eventData)
      {
         mDragging = true;
      }

      public void OnDrag(PointerEventData eventData)
      {
         if (!mDragging) return;

         BattleVirtualInput.LookDeltaX += eventData.delta.x;
         BattleVirtualInput.LookDeltaY += eventData.delta.y;
      }

      public void OnPointerUp(PointerEventData eventData)
      {
         mDragging = false;
      }

      private void OnDisable()
      {
         mDragging = false;
      }
   }
}
