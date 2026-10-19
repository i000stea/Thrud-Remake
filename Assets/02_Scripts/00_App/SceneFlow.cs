using System;
using System.Collections;
using System.Collections.Generic;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame.App
{
   /// <summary>
   /// 场景切换控制器（常驻于 ConfigScene）。
   /// 负责所有玩法场景的 additive 加载、卸载与 active scene 管理。
   /// 切换策略（决策 D20）：默认卸载旧玩法场景，入口场景（Build Settings[0]）永驻；
   /// 可按请求保留旧场景（SceneRequest.KeepPrevious）。
   /// 场景 key 即场景名称字符串（决策 D25），按名称加载，热更新可按同名 key 无缝接入。
   /// Loading 过渡（用户确认）：渐显 → 预加载(allowSceneActivation=false) → 保持 ≥minHoldDuration → 激活 → 渐隐。
   ///
   /// **两条加载通路（23 文档 S4/S5）**：
   /// - <see cref="SceneSource.BuildSettings"/>：<c>SceneManager.LoadSceneAsync</c>，要求场景在 Build Settings；
   /// - <see cref="SceneSource.AssetBundle"/>：ResKit 场景 AssetBundle（<c>ResLoader.LoadSceneAsync</c>），
   ///   场景可以不在 Build Settings，因而可由远端下发整包替换。
   /// 选择哪条通路只由请求的 <see cref="SceneRequest.Source"/> 决定，本类不含任何场景名特判。
   ///
   /// **ResKit 的一个硬约束**：<c>LoadSceneAsync</c> 不暴露 <c>allowSceneActivation</c>，
   /// 但它把 <see cref="AsyncOperation"/> 通过 onStartLoading 回调抛出来——本类在回调里
   /// 立刻置 <c>allowSceneActivation=false</c>，把「加载」与「激活」重新拆开，才能保住
   /// 既有的 Loading 过渡时序。回调是同步触发的（SimulationMode 与真机分支都是），
   /// 因此不存在「操作已经跑过 0.9 才被关掉激活」的竞态。
   /// </summary>
   public sealed class SceneFlow : MonoBehaviour
   {
      /// <summary>
      /// 全局场景切换控制器实例。
      /// 常驻于 ConfigScene，任何模块可经此请求场景切换。
      /// </summary>
      public static SceneFlow Instance { get; private set; }

      /// <summary>等待 ResKit 交出 AsyncOperation 的上限（秒）。超时按加载失败处理，避免永久卡住切换。</summary>
      private const float AssetBundleStartTimeout = 20f;

      /// <summary>
      /// 全局 Loading 遮罩根节点（由 ConfigScene 场景内置，C4 后接管）。
      /// 若未配置，回退从 Resources 实例化 LoadingGroup（过渡实现）。
      /// </summary>
      [SerializeField] private Transform loadingGroupRoot;

      [Header("Loading 过渡参数")]
      [Tooltip("Loading Canvas 渐显时长（秒）。")]
      [SerializeField] private float fadeInDuration = 0.5f;

      [Tooltip("Loading Canvas 渐隐时长（秒）。")]
      [SerializeField] private float fadeOutDuration = 0.5f;

      [Tooltip("加载完成后遮罩最少保持时长（秒）。")]
      [SerializeField] private float minHoldDuration = 1f;

      /// <summary> 当前切换请求，目标场景通过 <see cref="GetPayload{T}"/> 读取参数。 </summary>
      private SceneRequest mCurrentRequest;

      /// <summary> 是否正在切换中，防止并发切换请求导致场景状态错乱。 </summary>
      private bool mSwitching;

      /// <summary> Loading Canvas 的 CanvasGroup（渐显渐隐目标，取 LoadingGroup 根的第一个子对象）。 </summary>
      private CanvasGroup mLoadingCanvasGroup;

      /// <summary> Loading 进度文本（Loading Canvas 下 TMP 文本，用于显示加载百分比）。 </summary>
      private TMP_Text mProgressText;

      /// <summary>
      /// 每个由 AssetBundle 加载的场景所持有的 <see cref="ResLoader"/>（场景名 → loader）。
      /// 生命周期必须覆盖场景生命周期：场景还活着就不能回收它的 loader，否则资源被提前卸载。
      /// </summary>
      private readonly Dictionary<string, ResLoader> mSceneLoaders = new();

      /// <summary> AssetBundle 通路捕获到的 AsyncOperation（由 onStartLoading 回调写入）。 </summary>
      private AsyncOperation mPendingOperation;

      /// <summary> AssetBundle 通路的失败原因（回调无法返回值，用字段传出）。 </summary>
      private string mAssetBundleError;

      /// <summary>
      /// 全局 Loading 遮罩根节点（编辑器与运行时均可设置）。
      /// </summary>
      public Transform LoadingGroupRoot
      {
         get => loadingGroupRoot;
         set => loadingGroupRoot = value;
      }

      private void Awake()
      {
         if (Instance == null)
         {
            Instance = this;
         }
      }

      private void OnDestroy()
      {
         if (Instance == this)
         {
            Instance = null;
         }

         RecycleAllSceneLoaders();
      }

      /// <summary>
      /// 发起一次场景切换请求。
      /// 切换过程中再次调用会被忽略（灰盒阶段简单策略，后续可改为排队）。
      /// </summary>
      public void RequestLoad(SceneRequest request)
      {
         if (request == null)
         {
            Debug.LogError("[SceneFlow] 场景切换请求为空。");
            return;
         }

         if (mSwitching)
         {
            Debug.LogWarning($"[SceneFlow] 正在切换中，忽略请求 -> {request.SceneName}");
            return;
         }

         // 经包装协程执行切换，收尾后统一触发完成回调（保证恰好一次）
         StartCoroutine(RunLoadWithCallback(request));
      }

      /// <summary>
      /// 切换协程包装：执行 <see cref="LoadRoutine"/> 后触发请求的 <see cref="SceneRequest.OnComplete"/> 回调。
      /// </summary>
      private IEnumerator RunLoadWithCallback(SceneRequest request)
      {
         yield return LoadRoutine(request);
         request.OnComplete?.Invoke();
      }

      /// <summary>
      /// 读取当前切换请求的跨场景参数。
      /// </summary>
      public T GetPayload<T>()
      {
         return mCurrentRequest != null && mCurrentRequest.Payload is T payload ? payload : default;
      }

      /// <summary>
      /// 切换协程（旧版时序）：
      /// 遮罩渐显 → 预加载新场景（allowSceneActivation=false，progress 至 0.9 暂停）
      /// → 保持 ≥minHoldDuration → 激活切换 → 卸载旧玩法场景（可按请求保留）→ 遮罩渐隐。
      /// </summary>
      private IEnumerator LoadRoutine(SceneRequest request)
      {
         mSwitching = true;
         try
         {
            var showLoading = request.ShowLoading;
            if (showLoading)
            {
               EnsureLoadingGroupReady();
               if (mLoadingCanvasGroup != null)
               {
                  yield return FadeCanvasGroup(mLoadingCanvasGroup, 0f, 1f, fadeInDuration, easeOut: true);
               }
            }

            var sceneName = request.SceneName;
            if (string.IsNullOrEmpty(sceneName))
            {
               Debug.LogError("[SceneFlow] 场景 key（名称）为空，无法切换。");
               yield return FinishWithError(showLoading);
               yield break;
            }

            // 原地重载模式：先卸载全部旧玩法场景（仅保留入口常驻），再加载目标，
            // 避免目标与旧实例同场景并存导致状态/HUD 错乱（重开本关用）。
            if (request.UnloadFirst)
            {
               yield return UnloadPlayScenes(default);
            }

            // 按请求来源选择加载通路：Build Settings（SceneManager）或场景 AssetBundle（ResKit）
            AsyncOperation operation;
            if (request.Source == SceneSource.AssetBundle)
            {
               yield return LoadAssetBundleScene(request);
               if (mPendingOperation == null)
               {
                  Debug.LogError("[SceneFlow] 加载失败原因：" + (mAssetBundleError ?? "未知（AssetBundle 通路未产出 AsyncOperation）"));
                  yield return FinishWithError(showLoading);
                  yield break;
               }

               operation = mPendingOperation;
               mPendingOperation = null;
            }
            else
            {
               operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
               if (operation == null)
               {
                  Debug.LogError($"[SceneFlow] 场景加载失败，请确认已加入 Build Settings：{sceneName}");
                  yield return FinishWithError(showLoading);
                  yield break;
               }
            }

            // 预加载：进度到 0.9 前不激活场景，遮罩保持
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
               UpdateProgressText(operation.progress);
               yield return null;
            }

            UpdateProgressText(0.9f);

            // 加载完成后再保持最少时长，避免过渡瞬间结束
            var holdEndTime = Time.time + minHoldDuration;
            while (Time.time < holdEndTime)
            {
               yield return null;
            }

            // 激活场景并设为 active
            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
               yield return null;
            }

            var target = SceneManager.GetSceneByName(sceneName);
            if (!target.IsValid())
            {
               Debug.LogError($"[SceneFlow] 场景无效：{sceneName}");
               yield return FinishWithError(showLoading);
               yield break;
            }

            SceneManager.SetActiveScene(target);
            mCurrentRequest = request;

            // 卸载旧玩法场景（可按请求保留；入口场景始终保护）。
            // 2026-09-08：必须等**新场景激活后**再卸载——Unity 在 LoadSceneAsync 未完成
            // （allowSceneActivation=false）期间卸载其它场景会中断本次加载；
            // 这里逐个 await 卸载完成，保证旧场景（含其 UIRoot 场景实例）彻底销毁后
            // 再渐隐遮罩，UIKit 之后解析到的 UIRoot 必然是新场景的或常驻的。
            if (!request.KeepPrevious)
            {
               yield return UnloadPlayScenes(target);
            }

            // 遮罩渐隐露出新场景
            if (showLoading)
            {
               if (mLoadingCanvasGroup != null)
               {
                  yield return FadeCanvasGroup(mLoadingCanvasGroup, mLoadingCanvasGroup.alpha, 0f, fadeOutDuration, easeOut: false);
               }

               HideLoadingGroup();
            }
         }
         finally
         {
            // 无论成功、失败还是异常，都必须复位切换标记——
            // 否则后续 RequestLoad 会被"正在切换中"永久拦截（场景再也切不动）。
            mSwitching = false;
         }

         Debug.Log($"[SceneFlow] 场景切换完成 -> {request.SceneName}");
      }

      /// <summary>
      /// 经 ResKit 加载场景 AssetBundle（23 文档 S5）。
      /// 成功时把 <see cref="AsyncOperation"/> 写入 <see cref="mPendingOperation"/>，
      /// 失败时把原因写入 <see cref="mAssetBundleError"/> 并保证不泄漏 ResLoader。
      /// </summary>
      private IEnumerator LoadAssetBundleScene(SceneRequest request)
      {
         mPendingOperation = null;
         mAssetBundleError = null;

         var sceneName = request.SceneName;
         var bundleName = request.AssetBundleName;

         Debug.Log($"[SceneFlow] AssetBundle 场景请求 -> bundle={bundleName ?? "<空>"}, scene={sceneName}");

         if (string.IsNullOrEmpty(bundleName))
         {
            // 调用点没给 bundle 名时退回场景目录的统一映射，避免把 bundle 名散落到各处
            if (SceneCatalog.TryGetAssetBundle(sceneName, out var catalogBundle))
            {
               bundleName = catalogBundle;
               Debug.Log($"[SceneFlow] 请求未携带 bundle 名，改用场景目录映射：{bundleName}");
            }
            else
            {
               mAssetBundleError = $"场景未登记 AssetBundle 且请求未携带 bundle 名：{sceneName}";
               yield break;
            }
         }

         if (!TryResolveSceneInBundle(bundleName, sceneName, out var resolveError))
         {
            Debug.LogError($"[SceneFlow] bundle 命中 scene 未命中 -> bundle={bundleName}, scene={sceneName}：{resolveError}");
            mAssetBundleError = resolveError;
            yield break;
         }

         var loader = ResLoader.Allocate();
         AsyncOperation captured = null;

         // 回调里立刻关闭自动激活：这是把「加载」和「激活」拆开的唯一时机。
         loader.LoadSceneAsync(bundleName, sceneName, LoadSceneMode.Additive, LocalPhysicsMode.None,
            operation =>
            {
               if (operation == null)
               {
                  return;
               }

               operation.allowSceneActivation = false;
               captured = operation;
            });

         // 等待 ResKit 把 AsyncOperation 交回来（bundle 加载完成才会回调）
         var deadline = Time.realtimeSinceStartup + AssetBundleStartTimeout;
         while (captured == null && Time.realtimeSinceStartup < deadline)
         {
            yield return null;
         }

         if (captured == null)
         {
            loader.Recycle2Cache();
            mAssetBundleError = $"等待 ResKit 交出 AsyncOperation 超时（{AssetBundleStartTimeout:0}s），bundle={bundleName}";
            yield break;
         }

         // loader 的所有权交给场景：场景卸载后由 UnloadPlayScenes 回收
         mSceneLoaders[sceneName] = loader;
         mPendingOperation = captured;
         Debug.Log($"[SceneFlow] scene asset 命中 -> bundle={bundleName}, scene={sceneName}");
      }

      /// <summary>
      /// 在 ResKit 清单（asset_bundle_config.bin，SimulationMode 下为 AssetDatabase 实时快照）里
      /// 确认 (bundle, scene) 存在且类型是场景，用于给出比 ResKit 默认日志更准确的失败原因。
      /// </summary>
      private static bool TryResolveSceneInBundle(string bundleName, string sceneName, out string error)
      {
         error = null;
         var keys = ResSearchKeys.Allocate(sceneName, bundleName);
         try
         {
            var data = AssetBundleSettings.AssetBundleConfigFile.GetAssetData(keys);
            if (data == null)
            {
               error = "bundle 未命中：ResKit 清单里没有该 bundle/asset 条目（检查场景是否已标记 AB、是否重打过 AB）";
               return false;
            }

            if (data.AssetType != ResLoadType.ABScene)
            {
               error = $"条目存在但不是场景资源（AssetType={data.AssetType}），该场景可能没被标为 ABScene";
               return false;
            }

            return true;
         }
         catch (Exception e)
         {
            error = "查询 ResKit 清单异常：" + e.Message;
            return false;
         }
         finally
         {
            keys.Recycle2Cache();
         }
      }

      /// <summary>
      /// 卸载除「入口常驻场景」与「目标场景」之外的已加载玩法场景，并逐个等待卸载完成。
      /// 由 AssetBundle 加载的场景在卸载完成后回收其 ResLoader（23 文档 S5 的生命周期要求）。
      /// </summary>
      private IEnumerator UnloadPlayScenes(Scene keepScene)
      {
         var entrySceneName = SceneAnchor.GetEntrySceneName();

         for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
         {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            if (!string.IsNullOrEmpty(entrySceneName) && scene.name == entrySceneName) continue;
            if (scene == keepScene) continue;

            // 场景销毁后拿不到名字，先存下来用于回收它持有的 loader
            var unloadingSceneName = scene.name;

            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null)
            {
               yield return operation;
            }

            RecycleSceneLoader(unloadingSceneName);
         }
      }

      /// <summary>
      /// 回收指定场景持有的 ResLoader（没有则无操作）。
      /// </summary>
      private void RecycleSceneLoader(string sceneName)
      {
         if (string.IsNullOrEmpty(sceneName)) return;
         if (!mSceneLoaders.TryGetValue(sceneName, out var loader)) return;

         mSceneLoaders.Remove(sceneName);
         loader.Recycle2Cache();
         Debug.Log($"[SceneFlow] 已回收场景 ResLoader -> {sceneName}");
      }

      /// <summary>
      /// 回收全部场景 ResLoader（控制器销毁时调用）。
      /// </summary>
      private void RecycleAllSceneLoaders()
      {
         foreach (var pair in mSceneLoaders)
         {
            pair.Value.Recycle2Cache();
         }

         mSceneLoaders.Clear();
      }

      /// <summary>
      /// 准备 LoadingGroup：激活根对象并解析 Loading Canvas 的 CanvasGroup 与进度文本。
      /// </summary>
      private void EnsureLoadingGroupReady()
      {
         if (loadingGroupRoot == null)
         {
            loadingGroupRoot = TryLoadLoadingGroupFromResources();
         }

         if (loadingGroupRoot == null)
         {
            Debug.LogWarning("[SceneFlow] LoadingGroup 根节点为空，切换期间无遮罩。");
            return;
         }

         loadingGroupRoot.gameObject.SetActive(true);

         if (mLoadingCanvasGroup == null && loadingGroupRoot.childCount > 0)
         {
            var loadingCanvas = loadingGroupRoot.GetChild(0);
            mLoadingCanvasGroup = loadingCanvas.GetComponent<CanvasGroup>();
            if (mLoadingCanvasGroup == null)
            {
               Debug.LogWarning($"[SceneFlow] LoadingGroup 第一个子对象 {loadingCanvas.name} 缺少 CanvasGroup。");
            }

            mProgressText = loadingCanvas.GetComponentInChildren<TMP_Text>();
         }
      }

      /// <summary>
      /// 隐藏 LoadingGroup 根对象（切换完成后调用）。
      /// </summary>
      private void HideLoadingGroup()
      {
         if (loadingGroupRoot != null)
         {
            loadingGroupRoot.gameObject.SetActive(false);
         }
      }

      /// <summary>
      /// 更新加载进度文本（百分比）。
      /// </summary>
      private void UpdateProgressText(float progress)
      {
         if (mProgressText != null)
         {
            mProgressText.text = $"加载中 {Mathf.FloorToInt(progress * 100f)}%";
         }
      }

      /// <summary>
      /// CanvasGroup 渐变协程（easeOutCubic 渐显 / easeInCubic 渐隐，与旧版 LoadingControl 一致）。
      /// </summary>
      private static IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, float duration, bool easeOut)
      {
         if (canvasGroup == null)
         {
            yield break; // 遮罩可能随旧场景卸载被销毁，直接跳过（避免 NRE 中断切换协程）
         }

         if (duration <= 0f)
         {
            canvasGroup.alpha = to;
            yield break;
         }

         var elapsed = 0f;
         while (elapsed < duration)
         {
            if (canvasGroup == null) yield break;

            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            t = easeOut ? 1f - Mathf.Pow(1f - t, 3f) : Mathf.Pow(t, 3f);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
         }

         canvasGroup.alpha = to;
      }

      /// <summary>
      /// 出错后的收尾：隐藏遮罩并复位切换状态。
      /// </summary>
      private IEnumerator FinishWithError(bool showLoading)
      {
         if (showLoading)
         {
            HideLoadingGroup();
         }

         mSwitching = false;
         yield break;
      }

      /// <summary>
      /// 过渡实现：从 Resources 实例化 LoadingGroup。
      /// C4 将 LoadingGroup 置入 ConfigScene 后，本方法可移除。
      /// </summary>
      private static Transform TryLoadLoadingGroupFromResources()
      {
         var prefab = Resources.Load<GameObject>("Prefabs/LoadingGroup");
         if (prefab == null)
         {
            Debug.LogWarning("[SceneFlow] 未找到 Resources/Prefabs/LoadingGroup 预制件。");
            return null;
         }

         var instance = Instantiate(prefab);
         instance.name = "LoadingGroup(SceneFlow)";
         return instance.transform;
      }
   }
}
