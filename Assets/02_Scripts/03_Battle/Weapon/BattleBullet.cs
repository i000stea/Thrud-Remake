using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 新版玩家子弹（独立文件，供子弹预制体序列化挂载）。
   /// 负责把伤害分发给新旧两套敌人实现，命中后回收到对象池（T1.3）。
   /// 该脚本同时作为"可池化"标记：旧路径（未走对象池）生成的子弹保持未武装状态，
   /// 由同挂载的旧版 <c>Bullet_Base</c> 结算，避免新旧脚本双重处理。
   /// </summary>
   public class BattleBullet : MonoBehaviour, IBattlePoolable
   {
      /// <summary>
      /// 当前子弹的伤害值。
      /// </summary>
      public float Damage = 1f;

      /// <summary>
      /// 是否武装（仅对象池新路径为 true）。
      /// </summary>
      private bool mArmed;

      /// <summary>
      /// 用于防止一颗子弹在同一帧内重复命中多个碰撞体。
      /// </summary>
      private bool mCanHit = true;

      /// <summary>
      /// 未命中时的自动回收时间点（武装时由开火路径写入）。
      /// </summary>
      private float mAutoReturnTime;

      /// <summary>
      /// 供对象池取出后的组件状态重置。
      /// </summary>
      public void OnPoolSpawn()
      {
         mCanHit = true;
      }

      /// <summary>
      /// 武装子弹（对象池新路径调用）。
      /// 写入伤害与超时回收时间；命中时由 OnTriggerEnter 立即回池。
      /// </summary>
      public void ResetBullet(float damage, float lifeSeconds)
      {
         Damage = damage;
         mCanHit = true;
         mArmed = true;
         mAutoReturnTime = Time.time + Mathf.Max(0.01f, lifeSeconds);
      }

      /// <summary>
      /// 未命中子弹超时自动回池。
      /// 旧路径子弹未武装，Update 直接返回，保持零开销。
      /// </summary>
      private void Update()
      {
         if (!mArmed) return;
         if (Time.time < mAutoReturnTime) return;

         mArmed = false;
         BattleObjectPool.Despawn(gameObject);
      }

      /// <summary>
      /// 当子弹进入触发器时，尝试寻找目标并施加伤害。
      /// </summary>
      private void OnTriggerEnter(Collider other)
      {
         if (!mArmed || !mCanHit) return;

         var otherBullet = other.transform.FindParentComponent<BattleBullet>();
         if (otherBullet) return;

         var battleEnemy = other.transform.FindParentComponent<BattleEnemyEntity>();
         if (battleEnemy)
         {
            battleEnemy.ApplyDamage(Damage);
            OnHit();
            return;
         }

         var legacyEnemy = other.transform.FindParentComponent<EnemyEntity_Main>();
         if (legacyEnemy)
         {
            legacyEnemy.BeHit(Damage);
            OnHit();
            return;
         }

         OnHit();
      }

      /// <summary>
      /// 命中后的统一处理：立即回收到对象池。
      /// </summary>
      private void OnHit()
      {
         mCanHit = false;
         mArmed = false;
         BattleObjectPool.Despawn(gameObject);
      }
   }
}
