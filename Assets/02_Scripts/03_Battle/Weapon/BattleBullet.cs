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
      /// 命中点特效预制体（T1.4，灰盒闪烁球占位）。
      /// 命中时从对象池取出并在命中处显示，短暂后自动回池。
      /// </summary>
      [SerializeField] private GameObject hitEffectPrefab;

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
            OnHit(other);
            return;
         }

         var legacyEnemy = other.transform.FindParentComponent<EnemyEntity_Main>();
         if (legacyEnemy)
         {
            legacyEnemy.BeHit(Damage);
            OnHit(other);
            return;
         }

         OnHit(other);
      }

      /// <summary>
      /// 命中后的统一处理：生成命中点特效并立即回收到对象池。
      /// </summary>
      private void OnHit(Collider other)
      {
         mCanHit = false;
         mArmed = false;

         if (hitEffectPrefab)
         {
            var effectPoint = other ? ResolveEntrySurfacePoint(other) : transform.position;
            var effectObject = BattleObjectPool.Spawn(hitEffectPrefab, effectPoint, Quaternion.identity);
            if (effectObject)
            {
               BattleObjectPool.ReturnAfter(effectObject, 0.3f);
            }
         }

         BattleObjectPool.Despawn(gameObject);
      }

      /// <summary>
      /// 求子弹真正击中碰撞体的表面点。
      /// OnTriggerEnter 回调发生时，子弹可能已高速穿过碰撞体到达后侧，
      /// 直接取当前位置会得到背面点（特效被敌人挡住、相机看不到）。
      /// 做法：沿子弹飞行方向倒退一段距离到碰撞体前（体外），再对该碰撞体打射线，
      /// 取射线与碰撞体的首个交点为真实命中表面点，并向子弹来向外推少许避免特效嵌进表面。
      /// </summary>
      private Vector3 ResolveEntrySurfacePoint(Collider other)
      {
         var fireDirection = transform.forward;

         const float backDistance = 2f;   // 倒退距离：需大于子弹单物理步位移 + 穿入深度
         const float rayMaxDistance = 4f;

         var rayOrigin = transform.position - fireDirection * backDistance;
         if (other.Raycast(new Ray(rayOrigin, fireDirection), out var hitInfo, rayMaxDistance))
         {
            // 沿来向（体表外/通常为相机侧）外推，保证特效可见
            return hitInfo.point - fireDirection * 0.1f;
         }

         // 极端情况（掠射未命中射线）：退回体前最近点
         return other.ClosestPoint(rayOrigin);
      }
   }
}
