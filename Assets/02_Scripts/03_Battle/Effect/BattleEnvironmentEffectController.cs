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
      /// 正在显示的飘字对象（用于 LateUpdate 统一朝向相机）。
      /// </summary>
      private readonly List<GameObject> mShowingTexts = new();

      /// <summary>
      /// 用于复用的等待对象，避免每次协程都分配新实例。
      /// </summary>
      private WaitForSeconds mLifeTimeWait;

      /// <summary>飘字基准字号（T2.3 克制放大用，Awake 从 prefab 读取缓存）。</summary>
      private float mBaseFontSize = 24f;

      /// <summary>
      /// 初始化控制器。
      /// </summary>
      private void Awake()
      {
         mLifeTimeWait = new WaitForSeconds(damageTextShowTime);
         mShowingTexts.Clear();
         BattleObjectPool.WarmUp(damageTextPrefab, 8);

         if (damageTextPrefab && damageTextPrefab.GetComponentInChildren<TMP_Text>())
         {
            mBaseFontSize = damageTextPrefab.GetComponentInChildren<TMP_Text>().fontSize;
         }
      }

      /// <summary>
      /// 每帧修正飘字朝向，使其始终面向战斗相机。
      /// 相机取自 Model 的派生只读属性 `BattleStateModel.BattleCamera`。
      /// </summary>
      private void LateUpdate()
      {
         var battleCamera = BattleArchitecture.Interface.GetModel<BattleStateModel>().BattleCamera;
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
      /// 创建或复用一个伤害飘字（对象取自 BattleObjectPool，T1.3 统一池；默认白字）。
      /// </summary>
      public void CreateDamageText(Vector3 worldPosition, int damage)
      {
         CreateDamageText(worldPosition, damage, Color.white, false);
      }

      /// <summary>
      /// 创建伤害飘字（T2.3：支持元素色 / 克制金色放大）。
      /// </summary>
      public void CreateDamageText(Vector3 worldPosition, int damage, Color color, bool big)
      {
         if (!damageTextPrefab || !damageTextParent) return;

         var textObject = BattleObjectPool.Spawn(damageTextPrefab, worldPosition, Quaternion.identity, damageTextParent);
         if (!textObject) return;

         var childTransform = textObject.transform.GetChild(0);
         SetRandomOffset(childTransform);
         RefreshTextContent(childTransform, damage, color, big);

         textObject.transform.localScale = Vector3.one * 0.7f;
         textObject.transform.DOScale(1f, 0.1f);

         if (!mShowingTexts.Contains(textObject))
         {
            mShowingTexts.Add(textObject);
         }

         StartCoroutine(DamageTextLife(textObject));
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
      /// 刷新飘字文本内容、颜色与淡入（克制金/元素色）。
      /// </summary>
      private void RefreshTextContent(Transform childTransform, int damage, Color color, bool big)
      {
         if (!childTransform.TryGetComponent(out TMP_Text tmpText)) return;

         tmpText.text = damage.ToString();
         tmpText.color = new Color(color.r, color.g, color.b, 0f);
         tmpText.fontSize = big ? mBaseFontSize * 1.4f : mBaseFontSize;
         tmpText.DOColor(color, 0.1f);
      }

      /// <summary>
      /// 控制飘字从显示到回收的完整生命周期。
      /// </summary>
      private IEnumerator DamageTextLife(GameObject damageTextObject)
      {
         yield return mLifeTimeWait;

         mShowingTexts.Remove(damageTextObject);
         BattleObjectPool.Despawn(damageTextObject);
      }
   }
}
