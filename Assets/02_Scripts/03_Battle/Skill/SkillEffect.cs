using System.Collections.Generic;
using DG.Tweening;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.4 公共功能效果层（三层模型第三层）约定与共享工具。
   ///
   /// 设计要点：
   /// 1. 每个效果是一个无状态静态类（SkillEffect_*），只依赖 <see cref="BattleSkillContext"/>
   ///    （数值由档案注入），因此可被任意角色执行器按任意顺序复用——
   ///    例如"武器强化 + 特殊弹道"两个功能类可被不同角色技能各自组合。
   /// 2. 本类集中放置效果层高频共享逻辑：敌人枚举、瞄准中心求解、元素弹幕生成、
   ///    元素色与临时范围圈表现，避免各效果重复实现。
   /// 3. 效果层不写状态模型、不发事件；只做"对世界/敌人/表现的一次性操作"。
   /// </summary>
   public static class BattleSkillEffect
   {
      /// <summary>弹幕子弹飞行速度（元素弹幕用）。</summary>
      public const float BarrageBulletSpeed = 35f;

      /// <summary>元素色获取入口（效果圈/飘字等表现统一用）。</summary>
      public static Color ElementColor(DamageElement element)
      {
         return element == DamageElement.none
            ? Color.white
            : BattleElementBalance.GetElementColor(element);
      }

      /// <summary>
      /// 枚举当前在场的全部新版敌人实体。
      /// 旧版（Legacy）敌人不参与 T2.4 技能结算，统一跳过。
      /// </summary>
      public static IEnumerable<BattleEnemyEntity> EnumerateEnemies()
      {
         foreach (var target in BattleEnemyRegistry.EnumerateTargets())
         {
            if (target is BattleEnemyEntity enemy && enemy)
            {
               yield return enemy;
            }
         }
      }

      /// <summary>
      /// 求解技能施放的中心点：
      /// 有锁敌 → 锁定点；否则从相机/控制器前向打射线取落点，无命中则取最大距离点。
      /// </summary>
      public static Vector3 ResolveAimCenter(BattleSkillContext ctx, float maxDistance)
      {
         var lockTarget = ctx.LockTarget;
         if (lockTarget)
         {
            return lockTarget.position;
         }

         var origin = ctx.Origin;
         var forward = ctx.AimForward;
         if (Physics.Raycast(origin, forward, out var hitInfo, maxDistance))
         {
            return hitInfo.point;
         }

         return origin + forward * maxDistance;
      }

      /// <summary>
      /// 在指定点生成一发元素弹幕（对象池）。
      /// 复用 BattleElementBulletLibrary 的元素子弹 prefab；命中结算走 BattleBullet 新路径。
      /// </summary>
      public static void SpawnElementBullet(
         BattleSkillContext ctx,
         Vector3 position,
         Quaternion rotation,
         float damage,
         DamageElement element,
         float lifeSeconds)
      {
         var prefab = BattleElementBulletLibrary.Resolve(element);
         if (!prefab && ctx.Role?.WeaponView != null)
         {
            prefab = ctx.Role.WeaponView.BulletPrefab; // 元素子弹缺失时回退武器默认弹
         }

         if (!prefab)
         {
            return;
         }

         var bulletObject = BattleObjectPool.Spawn(prefab, position, rotation);
         if (!bulletObject)
         {
            return;
         }

         if (bulletObject.TryGetComponent(out BattleBullet battleBullet))
         {
            battleBullet.ResetBullet(Mathf.Max(0f, damage), Mathf.Max(0.1f, lifeSeconds), element);
         }

         //if (bulletObject.TryGetComponent(out Bullet_Base legacyBullet))
         //{
         //   legacyBullet.enabled = false; // 同挂载的旧版结算禁用，避免双重处理
         //}

         if (bulletObject.TryGetComponent(out Rigidbody rigidbody))
         {
            rigidbody.linearVelocity = bulletObject.transform.forward * BarrageBulletSpeed;
         }
      }

      /// <summary>
      /// 灰盒表现：在中心点铺一圈元素色范围圈（贴地圆盘，短暂后自动销毁）。
      /// 非战斗高频路径，允许一次性创建（结束后自动销毁，无泄漏）。
      /// </summary>
      public static void ShowImpactRing(Vector3 center, float radius, DamageElement element)
      {
         if (radius <= 0.01f)
         {
            return;
         }

         var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
         disc.name = "[SkillImpactRing]";

         var discCollider = disc.GetComponent<Collider>();
         if (discCollider)
         {
            Object.Destroy(discCollider);
         }

         disc.transform.SetPositionAndRotation(
            new Vector3(center.x, 0.03f, center.z),
            Quaternion.identity);

         var size = radius * 2f;
         disc.transform.localScale = new Vector3(size, 0.02f, size);

         var discRenderer = disc.GetComponent<Renderer>();
         if (discRenderer)
         {
            discRenderer.sharedMaterial = GetRingMaterial(element);
         }

         // 轻微上浮淡出，随后自毁
         disc.transform.DOScaleY(0.01f, 0.55f);
         disc.transform.DOMoveY(0.12f, 0.55f);
         BattleObjectPool.ReturnAfter(disc, 0.6f);
      }

      /// <summary>范围圈共享材质缓存（按元素懒创建，避免每次施放分配/泄漏材质）。</summary>
      private static readonly Dictionary<DamageElement, Material> sRingMaterials =
         new Dictionary<DamageElement, Material>();

      private static Material GetRingMaterial(DamageElement element)
      {
         if (sRingMaterials.TryGetValue(element, out var material))
         {
            return material;
         }

         material = new Material(Shader.Find("Sprites/Default"));
         var color = ElementColor(element);
         color.a = 0.4f;
         material.color = color;
         sRingMaterials[element] = material;
         return material;
      }
   }
}
