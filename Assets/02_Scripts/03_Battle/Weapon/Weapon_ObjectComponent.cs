using UnityEngine;
using TeaGame;
namespace TeaGame
{
   public class Weapon_ObjectComponent : MonoBehaviour
   {
      #region 参数变量

      /// <summary> 子弹发射起始点 </summary>
      public Transform showPoint;
      public ParticleSystem eff_MuzzleFire;
      public GameObject bulletPrefab;
      public GameObject bulletLinePrefab;

      /// <summary> 子弹射速 </summary>
      public float shotSpeed = 50f;
      /// <summary> 瞄准旋转速度 </summary>
      private float LockSpeed
      {
         get => BattleRuntimeBridge.LockSpeed;
      }

      #endregion

      private void Update()
      {
         TowardsUpdate();
      }
      /// <summary>
      /// 角度更新
      /// </summary>
      private void TowardsUpdate()
      {
         var lockEnemy = GetLockEnemy();
         if (lockEnemy)
         {
            // 获取目标方向
            Vector3 targetAxis = lockEnemy.transform.position - transform.position;

            // 计算目标的旋转（使用Quaternion）
            Quaternion targetRotation = Quaternion.LookRotation(targetAxis);

            // 将旋转值应用到物体
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, LockSpeed * Time.deltaTime);
         }
         else
         {
            // 目标旋转为单位Quaternion（不旋转）
            Quaternion targetRotation = Quaternion.identity;

            // 通过插值将当前旋转平滑过渡到目标旋转
            transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, LockSpeed * Time.deltaTime);

            //Debug.Log($"原始旋转: {transform.localRotation} 更新旋转: {targetRotation}");
         }
      }

      private Transform GetLockEnemy()
      {
         return BattleRuntimeBridge.LockEnemyTransform;
      }
   }
}
