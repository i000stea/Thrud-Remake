using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 敌人表现 Actor（B1 2026-09-21 从 <see cref="BattleEnemyEntity"/> 拆出）。
   ///
   /// **职责**：把实体的状态施加到场景对象上——身体着色、伤害飘字、自爆范围环。
   /// **不拥有战斗规则**：不判定伤害、不见血、不决定死亡；血量与元素一律以参数传入。
   ///
   /// 与玩家侧的表现层一致（见 `Actor/` 下的 BattleRoleActor / BattleCameraActor / BattleWeaponActor）：
   /// 纯 C# 类，不在 prefab 上新增 MonoBehaviour、不改变任何序列化字段名与引用。
   /// 序列化字段（<c>blastRangeRing</c> / <c>blastRingPrefab</c>、身体 Renderer 等）
   /// 仍留在 <see cref="BattleEnemyEntity"/> 上，由本类取用。
   /// </summary>
   public sealed class BattleEnemyActor
   {
      /// <summary>
      /// 爆炸范围环的基准面尺寸约定：内置 Plane 网格原始尺寸为 10×10，
      /// 故半径 <c>radius</c> 对应 x/z 缩放 = <c>radius*2/10</c>。
      /// </summary>
      private const float RangeRingPlaneSize = 10f;

      private readonly BattleEnemyEntity mHost;
      private readonly Transform mOwner;
      private readonly Transform mBlastRangeRing;
      private readonly GameObject mBlastRingPrefab;

      /// <summary>身体渲染器缓存（元素/状态着色，初始化时一次性获取）。</summary>
      private Renderer mBodyRenderer;

      /// <summary>身体原始色（prefab/工具给的类型色；恢复用）。</summary>
      private Color mBaseColor = Color.white;

      /// <summary>自身元素色（敌人带元素时；无元素为白=不使用）。</summary>
      private Color mElementBodyColor = Color.white;

      /// <summary>上次应用的身体着色掩码（避免每帧重涂）。</summary>
      private int mLastVisualMask = -1;

      /// <summary>
      /// 构造表现 Actor。
      /// </summary>
      /// <param name="host">宿主敌人实体（提供 transform 与 Renderer 查找）。</param>
      /// <param name="blastRangeRing">部署后常驻的感应/爆炸范围环（Mine prefab 子对象，可空）。</param>
      /// <param name="blastRingPrefab">爆炸瞬间范围提示环 prefab（对象池，可空）。</param>
      public BattleEnemyActor(BattleEnemyEntity host, Transform blastRangeRing, GameObject blastRingPrefab)
      {
         mHost = host;
         mOwner = host ? host.transform : null;
         mBlastRangeRing = blastRangeRing;
         mBlastRingPrefab = blastRingPrefab;
      }

      // ---------------- 身体着色 ----------------

      /// <summary>
      /// 初始化时一次性缓存身体渲染器与其原始色（原 <c>Awake</c> 中的表现部分）。
      /// </summary>
      public void CacheBodyRenderer()
      {
         if (!mHost) return;

         mBodyRenderer = mHost.GetComponentInChildren<Renderer>();
         if (mBodyRenderer)
         {
            mBaseColor = mBodyRenderer.material.color;
         }
      }

      /// <summary>
      /// 记录敌人自身元素色（由实体 <c>Element</c> setter 传入；无元素为白=不使用）。
      /// </summary>
      public void SetElement(DamageElement element)
      {
         mElementBodyColor = element == DamageElement.none ? Color.white : BattleElementBalance.GetElementColor(element);
      }

      /// <summary>
      /// 按激活状态掩码重涂身体色；掩码未变则跳过。
      /// 优先级与迁移前一致：电 &gt; 火 &gt; 冰 &gt; 风；掩码为 0 时回落到基础色。
      /// </summary>
      /// <param name="activeMask">状态掩码（1冰/2火/4电/8风；0=无状态）。</param>
      /// <param name="element">敌人自身元素（基础色取用）。</param>
      public void RefreshVisual(int activeMask, DamageElement element)
      {
         if (activeMask == mLastVisualMask) return;

         mLastVisualMask = activeMask;
         ApplyVisualMask(activeMask, element);
      }

      /// <summary>
      /// 恢复到"基础身体色"（优先敌人自身元素色，否则 prefab 类型色）。
      /// </summary>
      public void RefreshBaseBodyColor(DamageElement element)
      {
         if (!mBodyRenderer) return;

         mBodyRenderer.material.color = element == DamageElement.none ? mBaseColor : mElementBodyColor;
      }

      private void ApplyVisualMask(int mask, DamageElement element)
      {
         if (!mBodyRenderer) return;

         if (mask != 0)
         {
            // 优先级：电 > 火 > 冰 > 风
            var color = BattleElementBalance.GetElementColor(DamageElement.Wind);
            if ((mask & 1) != 0) color = BattleElementBalance.GetElementColor(DamageElement.Ice);
            if ((mask & 2) != 0) color = BattleElementBalance.GetElementColor(DamageElement.Fire);
            if ((mask & 8) != 0) color = BattleElementBalance.GetElementColor(DamageElement.Wind);
            if ((mask & 4) != 0) color = BattleElementBalance.GetElementColor(DamageElement.Electric);
            mBodyRenderer.material.color = color;
            return;
         }

         RefreshBaseBodyColor(element);
      }

      // ---------------- 伤害飘字 ----------------

      /// <summary>
      /// 在自身位置生成伤害飘字（T2.3：元素色 / 克制金色放大）。
      /// 环境特效控制器取自 Model 的派生只读属性 <see cref="BattleStateModel.EnvironmentEffects"/>。
      /// </summary>
      public void ShowDamageText(int amount, Color color, bool big)
      {
         var controller = BattleArchitecture.Interface.GetModel<BattleStateModel>().EnvironmentEffects;
         if (!controller || !mOwner) return;

         controller.CreateDamageText(mOwner.position, amount, color, big);
      }

      // ---------------- 自爆范围环 ----------------

      /// <summary>
      /// 把常驻范围环（子对象，Mine 感应圈）的直径同步为当前 BlastRadius。
      /// 在初始化时调用一次；Mine prefab 子环使用 Plane 网格。
      /// </summary>
      public void SyncBlastRangeRing(float radius)
      {
         if (!mBlastRangeRing) return;

         var diameter = radius * 2f;
         mBlastRangeRing.localScale = new Vector3(diameter / RangeRingPlaneSize, 1f, diameter / RangeRingPlaneSize);
      }

      /// <summary>
      /// 爆炸瞬间显示一次范围提示环（对象池，短暂后自动回池），供灰盒确认自爆伤害半径。
      /// 环贴地面（y=0.02）展开。
      /// </summary>
      public void ShowBlastRing(float radius)
      {
         if (!mBlastRingPrefab || !mOwner) return;

         var position = new Vector3(mOwner.position.x, 0.02f, mOwner.position.z);
         var ring = BattleObjectPool.Spawn(mBlastRingPrefab, position, Quaternion.identity);
         if (!ring) return;

         var diameter = Mathf.Max(0.2f, radius) * 2f;
         ring.transform.localScale = new Vector3(diameter / RangeRingPlaneSize, 1f, diameter / RangeRingPlaneSize);
         BattleObjectPool.ReturnAfter(ring, 0.55f);
      }
   }
}
