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
   /// T2.4：维护静态存活集（OnEnable/OnDisable 登记），供"清弹幕"类技能效果
   /// <see cref="SkillEffect_ClearEnemyBullets"/> 一键回收场上全部敌人子弹。
   /// </summary>
   public class BattleEnemyBullet : MonoBehaviour, IBattlePoolable
   {
      [SerializeField] private float destroyDelay = 10f;
      [SerializeField] private int damageRangeMin = 4;
      [SerializeField] private int damageRangeMax = 7;

      /// <summary>当前在场（激活）的敌人子弹集合（T2.4 清弹幕用）。</summary>
      private static readonly System.Collections.Generic.HashSet<BattleEnemyBullet> sActive =
         new System.Collections.Generic.HashSet<BattleEnemyBullet>();

      /// <summary>当前激活子弹数量（清弹幕反馈/日志用）。</summary>
      public static int ActiveCount => sActive.Count;

      /// <summary>激活时登记（含对象池取出）。</summary>
      private void OnEnable()
      {
         sActive.Add(this);
      }

      /// <summary>失活/销毁时注销（含对象池回收）。</summary>
      private void OnDisable()
      {
         sActive.Remove(this);
      }

      /// <summary>
      /// 清弹幕：把场上全部激活的敌人子弹立即回收（T2.4 风系技能）。
      /// 遍历前先取快照，避免回收过程中 OnDisable 修改集合导致枚举失效。
      /// </summary>
      public static void DespawnAllActive()
      {
         if (sActive.Count == 0)
         {
            return;
         }

         var snapshot = new System.Collections.Generic.List<BattleEnemyBullet>(sActive);
         for (var i = 0; i < snapshot.Count; i++)
         {
            var bullet = snapshot[i];
            if (bullet)
            {
               bullet.mCanHit = false;
               BattleObjectPool.Despawn(bullet.gameObject);
            }
         }
      }

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
