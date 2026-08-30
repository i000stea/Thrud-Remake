using DG.Tweening;
using TeaFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{


   /// <summary>
   /// 新版敌人子弹。
   /// 负责在碰撞到玩家时派发强类型受击事件，并及时销毁自己。
   /// </summary>
   public class BattleEnemyBullet : MonoBehaviour
   {
      [SerializeField] private float destroyDelay = 10f;
      [SerializeField] private int damageRangeMin = 4;
      [SerializeField] private int damageRangeMax = 7;

      /// <summary>
      /// 启动时设置自毁时间，防止子弹长时间滞留场景。
      /// </summary>
      private void Start()
      {
         Destroy(gameObject, destroyDelay);
      }

      /// <summary>
      /// 命中玩家时派发受击事件。
      /// </summary>
      private void OnTriggerEnter(Collider other)
      {
         var player = other.transform.FindParentComponent<PlayerControl>();
         if (!player) return;

         BattleArchitecture.Interface.SendEvent(new BattlePlayerDamagedEvent
         {
            Damage = Random.Range(damageRangeMin, damageRangeMax)
         });

         Destroy(gameObject);
      }
   }
}
