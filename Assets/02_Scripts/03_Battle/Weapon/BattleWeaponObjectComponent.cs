using System.Collections.Generic;
using System.Threading.Tasks;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 新版武器表现组件。
   /// 该组件用于承载新武器 prefab 的表现信息，并由战斗系统统一驱动瞄准朝向。
   /// </summary>
   public class BattleWeaponObjectComponent : MonoBehaviour
   {
      [Header("武器挂点")]
      [SerializeField] private Transform showPoint;
      [SerializeField] private ParticleSystem muzzleFireEffect;
      [SerializeField] private GameObject bulletPrefab;
      [SerializeField] private GameObject bulletLinePrefab;

      /// <summary>
      /// 子弹发射点。
      /// 若未手动配置，则退回到当前物体自身。
      /// </summary>
      public Transform ShowPoint => showPoint ? showPoint : transform;

      /// <summary>
      /// 子弹预制体。
      /// </summary>
      public GameObject BulletPrefab => bulletPrefab;

      /// <summary>
      /// 线性弹道表现预制体。
      /// </summary>
      public GameObject BulletLinePrefab => bulletLinePrefab;

      /// <summary>
      /// 播放枪口火焰。
      /// </summary>
      public void PlayMuzzleFire()
      {
         if (muzzleFireEffect)
         {
            muzzleFireEffect.Play();
         }
      }

      /// <summary>
      /// 刷新武器瞄准朝向。
      /// 若存在锁敌目标，则朝向目标；
      /// 否则缓动回到本地零旋转，保持武器挂回默认朝向。
      /// </summary>
      public void RefreshAim(Transform lookTarget, float lockSpeed)
      {
         if (lookTarget)
         {
            var targetAxis = lookTarget.position - transform.position;
            var targetRotation = Quaternion.LookRotation(targetAxis);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, lockSpeed * Time.deltaTime);
         }
         else
         {
            transform.localRotation = Quaternion.Lerp(
               transform.localRotation,
               Quaternion.identity,
               lockSpeed * Time.deltaTime);
         }
      }
   }
}
