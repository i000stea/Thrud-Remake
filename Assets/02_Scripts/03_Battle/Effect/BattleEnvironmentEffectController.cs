using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗环境特效控制器。
   /// 目前主要负责管理伤害飘字对象池与朝向相机的显示逻辑。
   /// </summary>
   public class BattleEnvironmentEffectController : MonoBehaviour
   {
      [Header("飘字资源")]
      [SerializeField] private GameObject damageTextPrefab;
      [SerializeField] private Transform damageTextParent;

      [Header("飘字参数")]
      [SerializeField] private float damageTextShowTime = 2f;
      [SerializeField] private int angleScope = 60;
      [SerializeField][Range(1f, 10f)] private float distanceMinScope = 1f;
      [SerializeField][Range(1f, 10f)] private float distanceMaxScope = 3f;

      /// <summary>
      /// 正在显示的飘字对象。
      /// </summary>
      private readonly List<GameObject> mShowingTexts = new();

      /// <summary>
      /// 已回收到对象池中的飘字对象。
      /// </summary>
      private readonly List<GameObject> mTextPool = new();

      /// <summary>
      /// 用于复用的等待对象，避免每次协程都分配新实例。
      /// </summary>
      private WaitForSeconds mLifeTimeWait;

      /// <summary>
      /// 初始化控制器。
      /// </summary>
      private void Awake()
      {
         mLifeTimeWait = new WaitForSeconds(damageTextShowTime);
         mShowingTexts.Clear();
         mTextPool.Clear();
      }

      /// <summary>
      /// 每帧修正飘字朝向，使其始终面向战斗相机。
      /// </summary>
      private void LateUpdate()
      {
         var battleCamera = BattleRuntimeBridge.Camera;
         if (!battleCamera) return;

         for (var i = 0; i < mShowingTexts.Count; i++)
         {
            if (mShowingTexts[i])
            {
               mShowingTexts[i].transform.rotation = battleCamera.transform.rotation;
            }
         }
      }

      /// <summary>
      /// 创建或复用一个伤害飘字。
      /// </summary>
      /// <param name="worldPosition">飘字生成位置。</param>
      /// <param name="damage">显示的伤害值。</param>
      public void CreateDamageText(Vector3 worldPosition, int damage)
      {
         if (!damageTextPrefab || !damageTextParent) return;

         var textObject = GetDamageTextObject();
         textObject.SetActive(true);
         textObject.transform.position = worldPosition;

         var childTransform = textObject.transform.GetChild(0);
         SetRandomOffset(childTransform);
         RefreshTextContent(childTransform, damage);

         textObject.transform.localScale = Vector3.one * 0.7f;
         textObject.transform.DOScale(1f, 0.1f);

         if (!mShowingTexts.Contains(textObject))
         {
            mShowingTexts.Add(textObject);
         }

         StartCoroutine(DamageTextLife(textObject));
      }

      /// <summary>
      /// 取得一个可用的飘字对象。
      /// 优先复用对象池，其次复用最早的显示对象，最后才会实例化新对象。
      /// </summary>
      private GameObject GetDamageTextObject()
      {
         if (mTextPool.Count > 0)
         {
            var pooledObject = mTextPool[0];
            mTextPool.RemoveAt(0);
            return pooledObject;
         }

         if (mShowingTexts.Count > 160)
         {
            var reusedObject = mShowingTexts[0];
            mShowingTexts.RemoveAt(0);
            return reusedObject;
         }

         return Instantiate(damageTextPrefab, damageTextParent);
      }

      /// <summary>
      /// 设置飘字在父节点下的随机偏移，避免大量伤害数字完全重叠。
      /// </summary>
      private void SetRandomOffset(Transform childTransform)
      {
         var angleInDegrees = Random.Range(-angleScope, angleScope);
         var distance = Random.Range(distanceMinScope, distanceMaxScope);
         var angleInRadians = angleInDegrees * Mathf.Deg2Rad;

         var x = Mathf.Cos(angleInRadians) * distance;
         var y = Mathf.Sin(angleInRadians) * distance;
         if (Random.Range(0, 2) == 0)
         {
            x = -x;
         }

         childTransform.localPosition = new Vector3(x, y, -0.1f);
      }

      /// <summary>
      /// 刷新飘字文本内容与淡入效果。
      /// </summary>
      private static void RefreshTextContent(Transform childTransform, int damage)
      {
         if (!childTransform.TryGetComponent(out TMP_Text tmpText)) return;

         tmpText.text = damage.ToString();
         tmpText.color = new Color(1f, 1f, 1f, 0f);
         tmpText.DOColor(Color.white, 0.1f);
      }

      /// <summary>
      /// 控制飘字从显示到回收的完整生命周期。
      /// </summary>
      private IEnumerator DamageTextLife(GameObject damageTextObject)
      {
         yield return mLifeTimeWait;

         mShowingTexts.Remove(damageTextObject);
         if (!mTextPool.Contains(damageTextObject))
         {
            mTextPool.Add(damageTextObject);
         }

         damageTextObject.SetActive(false);
      }
   }
}
