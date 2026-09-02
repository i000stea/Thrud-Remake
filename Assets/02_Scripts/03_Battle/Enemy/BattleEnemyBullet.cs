using DG.Tweening;
using TeaFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{


   /// <summary>
   /// 新版敌人子弹。
   /// 负责在碰撞到玩家时派发强类型受击事件，并回收到对象池（T1.3）。
   /// 生命周期由对象池激活时重置：未命中按 <see cref="destroyDelay"/> 超时自回，
   /// 命中玩家时立即回池。
   /// </summary>
   public class BattleEnemyBullet : MonoBehaviour, IBattlePoolable
   {
      [SerializeField] private float destroyDelay = 10f;
      [SerializeField] private int damageRangeMin = 4;
      [SerializeField] private int damageRangeMax = 7;

      /// <summary>
      /// 未命中剩余存活时间（每次取出时重置）。
      /// </summary>
      private float mRemainLife;

      /// <summary>
      /// 是否可命中（每次取出时重置，防止同帧重复结算）。
      /// </summary>
      private bool mCanHit = true;

      /// <summary>
      /// 对象池取出后的状态重置。
      /// </summary>
      public void OnPoolSpawn()
      {
         mCanHit = true;
         mRemainLife = Mathf.Max(0.05f, destroyDelay);
      }

      /// <summary>
      /// 未命中超时自动回池。
      /// </summary>
      private void Update()
      {
         if (mRemainLife <= 0f) return;

         mRemainLife -= Time.deltaTime;
         if (mRemainLife <= 0f)
         {
            mCanHit = false;
            BattleObjectPool.Despawn(gameObject);
         }
      }

      /// <summary>
      /// 命中玩家时派发受击事件并回池。
      /// </summary>
      private void OnTriggerEnter(Collider other)
      {
         if (!mCanHit) return;

         var player = other.transform.FindParentComponent<PlayerControl>();
         if (!player) return;

         mCanHit = false;
         BattleArchitecture.Interface.SendEvent(new BattlePlayerDamagedEvent
         {
            Damage = Random.Range(damageRangeMin, damageRangeMax)
         });

         BattleObjectPool.Despawn(gameObject);
      }
   }
}
