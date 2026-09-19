using UnityEngine;
using TeaGame;
namespace TeaGame
{
   /// <summary>
   /// 旧版武器表现组件（挂在 `Assets/04_Prefabs/Wep/Wep-Base.prefab`）。
   ///
   /// **2026-09-19 变更：不再自驱朝向。**
   /// 原实现自己在 `Update` 里读静态 `BattleRuntimeBridge.LockSpeed` / `LockEnemyTransform`
   /// 做插值——那是"表现层自己找数据"，且与 Actor 路径重复（`LegacyBattleWeaponViewAdapter.RefreshAim`
   /// 因此被留空以避免双插值抖动）。现在朝向改由 `BattleWeaponActor.Aim` 每帧调用
   /// <see cref="RefreshAim"/> 驱动，本组件不再读取任何全局状态。
   /// </summary>
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

      #endregion

      /// <summary>
      /// 刷新瞄准朝向（由 `BattleWeaponActor.Aim` 每帧调用；替换原自身 `Update` 的自驱逻辑）。
      /// 有锁定目标时朝目标水平插值；无目标时回正到挂点朝向（单位四元数，保持原行为）。
      /// </summary>
      public void RefreshAim(Transform lockEnemy, float lockSpeed)
      {
         var speed = Mathf.Max(0.01f, lockSpeed);

         if (lockEnemy)
         {
            // 获取目标方向
            Vector3 targetAxis = lockEnemy.position - transform.position;

            // 计算目标的旋转（使用Quaternion）
            Quaternion targetRotation = Quaternion.LookRotation(targetAxis);

            // 将旋转值应用到物体
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, speed * Time.deltaTime);
         }
         else
         {
            // 目标旋转为单位Quaternion（不旋转）
            Quaternion targetRotation = Quaternion.identity;

            // 通过插值将当前旋转平滑过渡到目标旋转
            transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, speed * Time.deltaTime);
         }
      }
   }
}
