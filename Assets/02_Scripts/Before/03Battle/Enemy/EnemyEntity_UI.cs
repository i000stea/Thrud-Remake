using DG.Tweening;
using TeaGame;
using UnityEngine;
using UnityEngine.UI;
namespace TeaGame
{
   public class EnemyEntity_UI : MonoBehaviour
   {
      [SerializeField] private RectTransform canvas;
      [SerializeField] private Image healthMain;
      [SerializeField] private Image healthHode;
      bool onOpen;
      private float cacheHealthPer = 1;
      [SerializeField] private Image sign;
      private EnemyUIData_Element uiIData;
      public void SetUIData(EnemyUIData_Element elementData)
      {
         uiIData = elementData;
         sign.sprite = uiIData.Sign;
      }
      private void Awake()
      {
         canvas.sizeDelta = new(0, 40);
      }
      // 用LateUpdate, 在每一帧的最后调整Canvas朝向
      void LateUpdate()
      {
         var battleCamera = GetBattleCamera();
         if (battleCamera != null)
         {
            transform.LockCamera(battleCamera);
         }
         healthHode.fillAmount = Mathf.Lerp(healthHode.fillAmount, cacheHealthPer, 0.05f);
      }

      public void HealthUpdate(float healthPer)
      {
         //Debug.Log(healthPer);
         if (!onOpen)
         {
            onOpen = true;
            HealthWidthSet();
         }
         healthMain.fillAmount = healthPer;
         cacheHealthPer = healthPer;
      }
      private void HealthWidthSet(int width = 400)
      {
         canvas.DOSizeDelta(new(width, 40), 0.5f).SetEase(Ease.OutExpo);
      }

      private Camera GetBattleCamera()
      {
         if (PlayerControl.I != null && PlayerControl.I.config != null)
         {
            return PlayerControl.I.config.camera;
         }

         return BattleRuntimeBridge.Camera;
      }
   }
}
