using DG.Tweening;
using TeaFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{

   /// <summary>
   /// 新版敌人 UI 组件。
   /// 负责敌人血条展开、缓动掉血和元素标识显示。
   /// </summary>
   public class BattleEnemyUI : MonoBehaviour
   {
      [SerializeField] private RectTransform canvas;
      [SerializeField] private Image healthMain;
      [SerializeField] private Image healthHold;
      [SerializeField] private Image sign;

      /// <summary>
      /// 当前缓存的血量百分比。
      /// `healthMain` 立即更新，`healthHold` 延迟跟随，用于营造掉血反馈。
      /// </summary>
      private float mCachedHealthPercent = 1f;

      /// <summary>
      /// 是否已经展开过血条。
      /// </summary>
      private bool mOpened;

      /// <summary>
      /// 初始化 UI 尺寸。
      /// </summary>
      private void Awake()
      {
         if (canvas)
         {
            canvas.sizeDelta = new Vector2(0f, 40f);
         }
      }

      /// <summary>
      /// 每帧保持 UI 朝向战斗相机。
      /// </summary>
      private void LateUpdate()
      {
         if (BattleRuntimeBridge.Camera)
         {
            transform.rotation = BattleRuntimeBridge.Camera.transform.rotation;
         }

         if (healthHold)
         {
            healthHold.fillAmount = Mathf.Lerp(healthHold.fillAmount, mCachedHealthPercent, 0.05f);
         }
      }

      /// <summary>
      /// 设置 UI 资源。
      /// </summary>
      public void Setup(EnemyUIData_Element elementData)
      {
         if (sign && elementData != null)
         {
            sign.sprite = elementData.Sign;
         }
      }

      /// <summary>
      /// 刷新血量显示。
      /// 首次刷新时会播放血条展开动画。
      /// </summary>
      public void RefreshHealth(float healthPercent)
      {
         if (!mOpened)
         {
            mOpened = true;
            OpenHealthBar();
         }

         if (healthMain)
         {
            healthMain.fillAmount = healthPercent;
         }

         mCachedHealthPercent = healthPercent;
      }

      /// <summary>
      /// 展开血条容器。
      /// </summary>
      private void OpenHealthBar()
      {
         if (canvas)
         {
            canvas.DOSizeDelta(new Vector2(400f, 40f), 0.5f).SetEase(Ease.OutExpo);
         }
      }
   }

}
