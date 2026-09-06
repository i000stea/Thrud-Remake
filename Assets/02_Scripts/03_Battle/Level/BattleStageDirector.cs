using System.Collections.Generic;
using QFramework;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.2 区域波次与条件生成器。
   /// T3.1C（D14）：由场景引导 battle_runtime 经 <see cref="Setup"/> 注入 BattleLevelData（内嵌波次表）与敌人目录；
   /// 旧 BattleStageAsset 配置资产已删除（迁移步骤6），本组件不再持有任何序列化关卡配置。
   /// 玩家进入站点半径 → 刷首批 → 在场击杀比例 ≥X% → 刷下一批 → 清场激活下一站 → 整场限时。
   /// 站点状态机：Inactive → Active → Arrived(刷首批) → Clearing(逐批) → Cleared → 下一站 → AllCleared。
   /// </summary>
   public class BattleStageDirector : MonoBehaviour
   {
      /// <summary>目标点到达圈子对象命名（与编辑器预览结构共用约定）。</summary>
      public const string ArriveRingChildName = "ArriveRing";

      /// <summary>环 Plane 原始尺寸（10×10）换算常量。</summary>
      private const float RingPlaneSize = 10f;

      [Header("装配注入（BattleSceneRuntimeHost/battle_runtime 于 Awake 注入；本组件不再序列化任何关卡配置资产）")]
      private BattleLevelData mLevelData;

      /// <summary>敌人目录（经 <see cref="Setup"/> 注入）。</summary>
      private EnemyCatalog mCatalog;

      /// <summary>
      /// 装配入口：注入关卡数据（<see cref="BattleLevelData"/> 内嵌波次表）与敌人目录。
      /// T3.1C（D14）：由场景引导 battle_runtime 调用；旧 BattleStageAsset 数据源已删除。
      /// </summary>
      public void Setup(BattleLevelData level, EnemyCatalog enemyCatalog)
      {
         mLevelData = level;
         if (enemyCatalog != null)
         {
            mCatalog = enemyCatalog;
         }
      }

      /// <summary>生效站点表：仅来自装配注入的关卡数据。</summary>
      private BattleSiteRow[] SitesRows => mLevelData == null ? null : mLevelData.Sites;

      /// <summary>生效整场限时。</summary>
      private int TimeLimitSecondsValue => mLevelData == null ? 0 : mLevelData.TimeLimitSeconds;

      [Header("表现")]
      [Tooltip("目标点光柱半透明色。")]
      [SerializeField] private Color markerColor = new Color(0.25f, 0.75f, 1f, 0.35f);

      /// <summary>运行时站点（配置 + 自动创建的目标点）。</summary>
      private sealed class SiteRuntime
      {
         public BattleSiteRow Row;
         public Transform Marker;
      }

      private readonly List<SiteRuntime> mSites = new List<SiteRuntime>();
      private readonly List<BattleEnemyEntity> mBatchEnemies = new List<BattleEnemyEntity>();

      /// <summary>当前站点索引 / 到达标记 / 待触发批次索引 / 当前批已刷数量。</summary>
      private int mSiteIndex = -1;
      private bool mSiteArrived;
      private int mBatchIndex;
      private int mBatchSpawned;

      private float mElapsed;
      private bool mFinished;

      /// <summary>本场是否已派发"波次开始"事件（首个站点刷怪时置位一次，T3.1）。</summary>
      private bool mWaveStarted;

      private Material mMarkerMaterial;

      private void Start()
      {
         if (SitesRows == null || SitesRows.Length == 0)
         {
            Debug.LogWarning("[T2.2] BattleStageDirector 缺少站点数据（请由 BattleFlowRunner 装配 BattleLevelData，或检查旧配置资产）。");
            return;
         }

         BuildSiteMarkers();
         if (mSites.Count == 0)
         {
            Debug.LogWarning("[T2.2] 关卡数据内没有站点。");
            return;
         }

         // 只显示第 1 个目标点
         for (var i = 0; i < mSites.Count; i++)
         {
            mSites[i].Marker.gameObject.SetActive(i == 0);
         }

         ActivateNextSite();
      }

      private void Update()
      {
         if (mFinished) return;

         mElapsed += Time.deltaTime;
         if (TimeLimitSecondsValue > 0 && mElapsed >= TimeLimitSecondsValue)
         {
            TimeUp();
            return;
         }

         TickCurrentSite();
      }

      // ---------------- 站点推进 ----------------

      private void TickCurrentSite()
      {
         var site = CurrentSite;
         if (site == null) return;

         if (!mSiteArrived)
         {
            if (!IsPlayerWithin(site.Marker.position, site.Row.ArriveRadius)) return;

            mSiteArrived = true;
            Debug.Log($"[T2.2] 玩家到达站点 {mSiteIndex + 1}/{mSites.Count}，开始刷怪。");
            SpawnBatch(0);

            // T3.1：首个站点刷怪 = 波次开始（关卡状态机 Prepare → Wave）
            if (!mWaveStarted)
            {
               mWaveStarted = true;
               BattleArchitecture.Interface.SendEvent(new BattleStageWaveStartedEvent());
            }

            return;
         }

         var batchCount = site.Row.Batches == null ? 0 : site.Row.Batches.Length;
         if (mBatchIndex >= batchCount)
         {
            if (PruneAndCountAlive() <= 0)
            {
               CompleteSite();
            }

            return;
         }

         var batch = site.Row.Batches[mBatchIndex];
         var aliveNow = PruneAndCountAlive();

         if (mBatchSpawned <= 0 || aliveNow <= 0)
         {
            // 当前批已全灭（或批内容为空）：按比例必然满足，推进下一批/完成
            if (aliveNow <= 0 && mBatchSpawned > 0)
            {
               SpawnBatch(mBatchIndex + 1);
            }

            return;
         }

         var killed = mBatchSpawned - aliveNow;
         var ratio = killed / (float)mBatchSpawned;
         if (ratio >= batch.KillRatio)
         {
            Debug.Log($"[T2.2] 批次 {mBatchIndex + 1} 击杀比例 {ratio:P0} ≥ {batch.KillRatio:P0}，刷新下一批。");
            SpawnBatch(mBatchIndex + 1);
         }
      }

      /// <summary>
      /// 按配置批次刷怪（敌人 id → Catalog → prefab；位置按相对站点角距换算）。
      /// </summary>
      private void SpawnBatch(int batchIndex)
      {
         var site = CurrentSite;
         if (site == null || site.Row.Batches == null) return;

         mBatchIndex = batchIndex;
         if (batchIndex >= site.Row.Batches.Length) return;

         var batch = site.Row.Batches[batchIndex];
         mBatchEnemies.Clear();
         mBatchSpawned = 0;

         if (batch.Spawns != null)
         {
            for (var i = 0; i < batch.Spawns.Length; i++)
            {
               var row = batch.Spawns[i];
               if (row == null || row.Count <= 0) continue;

               var prefab = ResolveEnemyPrefab(row.EnemyId);
               if (prefab == null)
               {
                  Debug.LogWarning($"[T2.2] 站点 {mSiteIndex + 1} 批次 {batchIndex + 1} 敌人 id「{row.EnemyId}」在 EnemyCatalog 中未找到 prefab，已跳过。");
                  continue;
               }

               var spawnOrigin = ResolveSpawnPosition(site.Marker, row);
               mBatchSpawned += SpawnEnemies(prefab, row.Count, spawnOrigin, row.Scatter, row.Element);
            }
         }

         Debug.Log($"[T2.2] 站点 {mSiteIndex + 1} 已刷批次 {batchIndex + 1}/{site.Row.Batches.Length}：敌人 {mBatchSpawned} 个。");
      }

      private int SpawnEnemies(GameObject prefab, int count, Vector3 origin, float scatter, DamageElement element)
      {
         var spawned = 0;
         for (var i = 0; i < count; i++)
         {
            var offset = Vector3.zero;
            if (scatter > 0.01f)
            {
               var angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
               var radius = Mathf.Sqrt(Random.value) * scatter;
               offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            var go = Instantiate(prefab, origin + offset, Quaternion.identity);
            var entity = go.GetComponent<BattleEnemyEntity>();
            if (entity == null)
            {
               Debug.LogWarning($"[T2.2] 敌人 prefab {prefab.name} 根节点缺少 BattleEnemyEntity，已生成但不参与统计。");
               continue;
            }

            // T2.3：配置的敌人元素（同时按元素染色）
            if (element != DamageElement.none)
            {
               entity.Element = element;
            }

            mBatchEnemies.Add(entity);
            spawned++;
         }

         return spawned;
      }

      // ---------------- 站点切换 / 结束 ----------------

      private void CompleteSite()
      {
         var site = CurrentSite;
         if (site != null && site.Marker != null)
         {
            site.Marker.gameObject.SetActive(false);
         }

         Debug.Log($"[T2.2] 站点 {mSiteIndex + 1}/{mSites.Count} 已清剿完成。");
         ActivateNextSite();
      }

      private void ActivateNextSite()
      {
         mSiteIndex++;
         mSiteArrived = false;
         mBatchIndex = 0;
         mBatchSpawned = 0;
         mBatchEnemies.Clear();

         if (mSiteIndex >= mSites.Count)
         {
            mFinished = true;
            Debug.Log("[T2.2] 全部站点清剿完成（演示通关）。");

            // T3.1：整场清剿完成 → 关卡状态机 Wave → Victory
            BattleArchitecture.Interface.SendEvent(new BattleStageClearedEvent());
            return;
         }

         var site = mSites[mSiteIndex];
         site.Marker.gameObject.SetActive(true);
         SyncArriveRing(site.Marker, site.Row.ArriveRadius);
         Debug.Log($"[T2.2] 目标点 {mSiteIndex + 1} 已出现（玩家进入半径 {site.Row.ArriveRadius:N0}m 开始刷怪）。");
      }

      private void TimeUp()
      {
         mFinished = true;
         Debug.LogWarning($"[T2.2] 整场限时 {TimeLimitSecondsValue}s 到，判负（占位，正式结算归 T3.4）。");

         // T3.1：限时到 → 关卡状态机 Wave → Defeat
         BattleArchitecture.Interface.SendEvent(new BattleStageTimedOutEvent());
      }

      // ---------------- 运行时可视化（按配置自动生成目标点） ----------------

      private void BuildSiteMarkers()
      {
         var rows = SitesRows;
         if (rows == null) return;

         var container = new GameObject("RuntimeStageMarkers");
         container.transform.SetParent(transform, false);

         for (var i = 0; i < rows.Length; i++)
         {
            var row = rows[i];
            if (row == null) continue;

            var marker = new GameObject($"Goal_{i + 1}");
            marker.transform.SetParent(container.transform, false);
            marker.transform.position = new Vector3(row.Position.x, 0f, row.Position.y);

            var material = EnsureMarkerMaterial();

            // 光柱
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "GoalBeam";
            beam.transform.SetParent(marker.transform, false);
            beam.transform.localPosition = new Vector3(0f, 4f, 0f);
            beam.transform.localScale = new Vector3(0.8f, 4f, 0.8f);
            beam.GetComponent<MeshRenderer>().sharedMaterial = material;
            DestroyCollider(beam);

            // 到达圈
            var ring = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ring.name = ArriveRingChildName;
            ring.transform.SetParent(marker.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = material;
            DestroyCollider(ring);
            SyncArriveRing(marker.transform, row.ArriveRadius);

            mSites.Add(new SiteRuntime { Row = row, Marker = marker.transform });
         }
      }

      private Material EnsureMarkerMaterial()
      {
         if (mMarkerMaterial != null) return mMarkerMaterial;

         var shader = Shader.Find("Universal Render Pipeline/Lit");
         if (shader == null) shader = Shader.Find("Standard");

         mMarkerMaterial = new Material(shader != null ? shader : Shader.Find("Diffuse"))
         {
            color = markerColor
         };
         mMarkerMaterial.SetOverrideTag("RenderType", "Transparent");
         mMarkerMaterial.SetFloat("_Surface", 1f);
         mMarkerMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
         mMarkerMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
         mMarkerMaterial.SetInt("_ZWrite", 0);
         mMarkerMaterial.DisableKeyword("_SURFACE_TYPE_OPAQUE");
         mMarkerMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
         mMarkerMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
         return mMarkerMaterial;
      }

      // ---------------- 工具 ----------------

      /// <summary>按字符串 id 从目录解析敌人 prefab。</summary>
      private GameObject ResolveEnemyPrefab(string enemyId)
      {
         if (mCatalog == null) return null;
         return mCatalog.ResolvePrefab(enemyId);
      }

      /// <summary>
      /// 刷怪起点：站点位置 + (方位角 Yaw → +Z 前方旋转) × 距离，再叠 y 高度。
      /// </summary>
      private static Vector3 ResolveSpawnPosition(Transform marker, BattleSpawnRow row)
      {
         var markerBase = marker.position;
         var direction = Quaternion.Euler(0f, row.AngleDeg, 0f) * Vector3.forward;
         return new Vector3(markerBase.x + direction.x * row.Distance, row.Y, markerBase.z + direction.z * row.Distance);
      }

      private static void SyncArriveRing(Transform marker, float arriveRadius)
      {
         var ring = marker.Find(ArriveRingChildName);
         if (ring == null) return;

         var diameter = Mathf.Max(0.2f, arriveRadius) * 2f;
         ring.localScale = new Vector3(diameter / RingPlaneSize, 1f, diameter / RingPlaneSize);
      }

      private static bool IsPlayerWithin(Vector3 center, float radius)
      {
         var player = BattleRuntimeBridge.PlayerTransform;
         if (!player) return false;

         var dx = center.x - player.position.x;
         var dz = center.z - player.position.z;
         return dx * dx + dz * dz <= radius * radius;
      }

      private int PruneAndCountAlive()
      {
         var alive = 0;
         for (var i = mBatchEnemies.Count - 1; i >= 0; i--)
         {
            var enemy = mBatchEnemies[i];
            if (enemy == null)
            {
               mBatchEnemies.RemoveAt(i);
               continue;
            }

            alive++;
         }

         return alive;
      }

      private SiteRuntime CurrentSite
      {
         get
         {
            if (mSiteIndex < 0 || mSiteIndex >= mSites.Count) return null;
            return mSites[mSiteIndex];
         }
      }

      private static void DestroyCollider(GameObject target)
      {
         var collider = target.GetComponent<Collider>();
         if (collider)
         {
            Object.Destroy(collider);
         }
      }
   }
}
