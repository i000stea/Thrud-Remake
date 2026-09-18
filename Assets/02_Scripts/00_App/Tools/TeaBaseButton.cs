using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
namespace TeaGame
{
   public class TeaBaseButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
   {
      public event Action OnClicked;
      public UnityEvent clickEvent;

      [SerializeField] private float clickChangeScale = 1;
      [SerializeField] private float clickTransparency = 0.7f;
      [Range(0.2f, 5f)][SerializeField] private float clickTransSpeed = 1f;
      private float targetTrans = 1;
      private CanvasGroup cGroup;
      private void Awake()
      {
         TryGetComponent(out cGroup);
      }

      private void LateUpdate()
      {
         if (cGroup) cGroup.alpha =
               Mathf.Lerp(cGroup.alpha, targetTrans, 0.04f * clickTransSpeed);
      }

      public void OnPointerClick(PointerEventData eventData)
      {
         Debug.Log("按下" + gameObject.name);
         clickEvent?.Invoke();
         OnClicked?.Invoke();
      }

      public void OnPointerDown(PointerEventData eventData)
      {
         if (clickChangeScale != 1)
            transform.DOScale(0.95f, 0.1f);
         targetTrans = clickTransparency;

      }

      public void OnPointerUp(PointerEventData eventData)
      {
         if (clickChangeScale != 1)
            transform.DOScale(1f, 0.1f);
         targetTrans = 1;
      }


   }
}
