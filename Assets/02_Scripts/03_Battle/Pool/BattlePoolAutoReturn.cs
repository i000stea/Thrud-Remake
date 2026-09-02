using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 延迟回收标记组件（独立文件）。
   /// 由 <see cref="BattleObjectPool.ReturnAfter"/> 惰性挂载到需要延迟回收的池化对象上，
   /// 存活期间递减计时，归零后调用池回收自身。
   /// 注意：需要被序列化挂载的 MonoBehaviour 一律独立成文件
   /// （多类文件无法用 {fileID:11500000} 在 prefab/场景 YAML 中精确引用对应类）。
   /// </summary>
   public sealed class BattlePoolAutoReturn : MonoBehaviour
   {
      public float life;

      private void Update()
      {
         life -= Time.deltaTime;
         if (life > 0f) return;

         life = 0f;
         enabled = false;
         BattleObjectPool.Despawn(gameObject);
      }
   }
}
