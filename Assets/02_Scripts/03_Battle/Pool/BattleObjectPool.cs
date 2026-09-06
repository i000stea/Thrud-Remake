using System.Collections.Generic;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 可复用对象池化接口。
   /// 对象被池取出（准备激活）时调用 <see cref="OnPoolSpawn"/>，
   /// 组件可在此重置一次性状态（如碰撞开关、计时器）。
   /// </summary>
   public interface IBattlePoolable
   {
      void OnPoolSpawn();
   }

   /// <summary>
   /// 战斗对象池（T1.3）。
   /// 按 prefab 分桶 + 栈复用，替代战斗高频路径（射击/敌人子弹/弹道表现）中的
   /// <c>Instantiate</c>/<c>Destroy</c> 分配。
   ///
   /// 使用纪律：
   /// - 实例首次使用时按桶惰性预热（默认 64 个），打空后自动补充一批，保证持续射击路径 0 分配；
   /// - 带生命周期自动回收的对象（弹道线等）用 <see cref="ReturnAfter"/>；
   /// - 有命中结算的子弹由子弹脚本自管回收（命中即 Despawn，未命中超时自回），避免双重回收。
   /// </summary>
   public static class BattleObjectPool
   {
      /// <summary>每个分桶首次 Spawn 时预热的实例数量（子弹按"每武器 64-128"取值）。</summary>
      private const int DefaultWarmUpPerBucket = 64;

      /// <summary>实例最大缓存数量（超出后丢弃，避免无限膨胀）。</summary>
      private const int MaxInstancesPerBucket = 256;

      private static readonly Dictionary<GameObject, Stack<GameObject>> sBuckets =
         new Dictionary<GameObject, Stack<GameObject>>(8);

      private static readonly Dictionary<GameObject, GameObject> sPrefabOf =
         new Dictionary<GameObject, GameObject>(64);

      /// <summary>
      /// 每个池化实例的 TrailRenderer 缓存（首次取用惰性查找，之后复用避免每帧/每发分配）。
      /// TrailRenderer 失活不会自动清空轨迹，重新激活时需 Clear，否则会出现一帧滞留拖尾。
      /// </summary>
      private static readonly Dictionary<GameObject, TrailRenderer> sTrailRenderers =
         new Dictionary<GameObject, TrailRenderer>(64);

      private static Transform sRoot;
      private static bool sRootCreated;

      /// <summary>累计取出次数。</summary>
      public static int TotalSpawnCount { get; private set; }

      /// <summary>累计回收次数。</summary>
      public static int TotalDespawnCount { get; private set; }

      /// <summary>累计新建实例次数（仅预热/扩容时发生）。</summary>
      public static int TotalInstantiatedCount { get; private set; }

      /// <summary>
      /// 从池中取一个实例（父节点为池根）。
      /// 若对应分桶为空，先预热一批（首次取用时会集中 Instantiate，之后走复用）。
      /// </summary>
      public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
      {
         return Spawn(prefab, position, rotation, null);
      }

      /// <summary>
      /// 从池中取一个实例并挂到指定父节点下（用于 UI 飘字等需要特定父层级的对象）。
      /// </summary>
      public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
      {
         if (!prefab) return null;

         EnsureRoot();

         var bucket = GetOrCreateBucket(prefab);
         if (bucket.Count == 0)
         {
            WarmUp(prefab, DefaultWarmUpPerBucket);
         }

         var instance = PopValid(bucket);
         if (!instance)
         {
            instance = CreateInstance(prefab);
         }

         var pooledTransform = instance.transform;
         pooledTransform.SetParent(parent ? parent : sRoot, false);
         pooledTransform.SetPositionAndRotation(position, rotation);

         instance.SetActive(true);
         ClearPooledTrail(instance);
         ResetRigidbody(instance);

         // 复用对象的一次性状态由组件自行重置
         var poolable = instance.GetComponent<IBattlePoolable>();
         poolable?.OnPoolSpawn();

         TotalSpawnCount++;
         return instance;
      }

      /// <summary>
      /// 把实例回收到对应分桶（立即回收）。
      /// 若实例不是本池创建的（旧路径直接 Instantiate 的子弹等），则退化为直接销毁。
      /// </summary>
      public static void Despawn(GameObject instance)
      {
         if (!instance) return;

         // 取消待执行的延迟自动回收（弹道线等），避免回池后计时残留
         CancelAutoReturn(instance);

         if (sPrefabOf.TryGetValue(instance, out var prefab)
             && prefab
             && sBuckets.TryGetValue(prefab, out var bucket))
         {
            if (bucket.Count < MaxInstancesPerBucket)
            {
               instance.SetActive(false);
               instance.transform.SetParent(sRoot, false);
               bucket.Push(instance);
            }
            else
            {
               // 分桶已满：丢弃实例，避免缓存无限膨胀
               sPrefabOf.Remove(instance);
               sTrailRenderers.Remove(instance);
               Object.Destroy(instance);
            }

            TotalDespawnCount++;
            return;
         }

         // 非池化实例：兼容旧路径的直接销毁
         Object.Destroy(instance);
      }

      /// <summary>
      /// 清空全部「在场（激活中）」的池化实例（战斗结束/重开前调用）。
      /// 池根为 DontDestroyOnLoad，场景卸载不会带走激活子弹——
      /// 不清理会导致重开/离开战斗后旧弹幕滞留在场景中。
      /// </summary>
      public static void DespawnAllActive()
      {
         if (sPrefabOf.Count == 0) return;

         // 取快照：Despawn 可能改写 sPrefabOf（桶满销毁实例），不能在遍历中修改字典
         var snapshot = new List<GameObject>(sPrefabOf.Count);
         foreach (var instance in sPrefabOf.Keys)
         {
            if (instance != null && instance.activeSelf)
            {
               snapshot.Add(instance);
            }
         }

         for (var i = 0; i < snapshot.Count; i++)
         {
            Despawn(snapshot[i]);
         }
      }

      /// <summary>
      /// 延迟回收（供纯表现对象使用，例如狙击弹道线）。
      /// 该对象不应在等待期间被手动回收，否则可能出现双重回收。
      /// </summary>
      public static void ReturnAfter(GameObject instance, float delay)
      {
         if (!instance) return;

         var marker = instance.GetComponent<BattlePoolAutoReturn>();
         if (!marker)
         {
            marker = instance.AddComponent<BattlePoolAutoReturn>();
         }

         marker.life = Mathf.Max(0f, delay);
         marker.enabled = true;
      }

      /// <summary>
      /// 预热指定 prefab 的分桶。
      /// 场景加载/首次开火前调用可消除射击路径上的首次 Instantiate。
      /// </summary>
      public static void WarmUp(GameObject prefab, int count)
      {
         if (!prefab || count <= 0) return;

         EnsureRoot();
         var bucket = GetOrCreateBucket(prefab);

         var available = bucket.Count;
         var createCount = count - available;
         for (var i = 0; i < createCount; i++)
         {
            if (bucket.Count >= MaxInstancesPerBucket) break;
            bucket.Push(CreateInstance(prefab));
         }
      }

      /// <summary>
      /// 输出当前池状态（验证回收数与分配数用）。
      /// </summary>
      public static string DumpStats()
      {
         var pooledInactive = 0;
         foreach (var pair in sBuckets)
         {
            pooledInactive += pair.Value.Count;
         }

         return $"Spawn={TotalSpawnCount} Despawn={TotalDespawnCount} " +
                $"Instantiate={TotalInstantiatedCount} PooledInactive={pooledInactive}";
      }

      private static Stack<GameObject> GetOrCreateBucket(GameObject prefab)
      {
         if (!sBuckets.TryGetValue(prefab, out var bucket))
         {
            bucket = new Stack<GameObject>(DefaultWarmUpPerBucket);
            sBuckets.Add(prefab, bucket);
         }

         return bucket;
      }

      /// <summary>
      /// 从分桶中弹出一个有效实例。
      /// 若栈顶存在已被外部销毁的残留引用，则丢弃并继续弹出。
      /// </summary>
      private static GameObject PopValid(Stack<GameObject> bucket)
      {
         while (bucket.Count > 0)
         {
            var candidate = bucket.Pop();
            if (!candidate)
            {
               // 已被外部 Destroy 的残留实例：顺带清理缓存条目
               sPrefabOf.Remove(candidate);
               sTrailRenderers.Remove(candidate);
               continue;
            }

            return candidate;
         }

         return null;
      }

      private static GameObject CreateInstance(GameObject prefab)
      {
         var instance = Object.Instantiate(prefab, sRoot);
         instance.SetActive(false);

         sPrefabOf[instance] = prefab;
         TotalInstantiatedCount++;
         return instance;
      }

      private static void ResetRigidbody(GameObject instance)
      {
         var rigidbody = instance.GetComponent<Rigidbody>();
         if (!rigidbody) return;

         rigidbody.velocity = Vector3.zero;
         rigidbody.angularVelocity = Vector3.zero;
      }

      /// <summary>
      /// 取消实例上待执行的延迟自动回收（BattlePoolAutoReturn），防止回池后计时残留误回收。
      /// </summary>
      private static void CancelAutoReturn(GameObject instance)
      {
         var marker = instance.GetComponent<BattlePoolAutoReturn>();
         if (marker)
         {
            marker.enabled = false;
         }
      }

      /// <summary>
      /// 清空实例上的 TrailRenderer 旧轨迹（含子对象），避免池化复用出现一帧滞留拖尾。
      /// Trail 引用按实例缓存：首次取用查找一次，之后查字典复用（无每帧/每发分配）。
      /// </summary>
      private static void ClearPooledTrail(GameObject instance)
      {
         if (!sTrailRenderers.TryGetValue(instance, out var trail))
         {
            trail = instance.GetComponentInChildren<TrailRenderer>(true);
            sTrailRenderers[instance] = trail; // 可能为 null（无 Trail 的对象）
         }

         if (trail)
         {
            trail.Clear();
         }
      }

      private static void EnsureRoot()
      {
         if (sRootCreated && sRoot) return;

         var rootObject = new GameObject("[BattleObjectPool]");
         Object.DontDestroyOnLoad(rootObject);
         sRoot = rootObject.transform;
         sRootCreated = true;
      }
   }
}
