using System.Collections;
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
   /// </summary>
   public sealed class SceneFlow : MonoBehaviour
   {
      /// <summary>
      /// 全局场景切换控制器实例。
      /// 常驻于 ConfigScene，任何模块可经此请求场景切换。
      /// </summary>
      public static SceneFlow Instance { get; private set; }

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

      /// <summary>
      /// 当前切换请求，目标场景通过 <see cref="GetPayload{T}"/> 读取参数。
      /// </summary>
      private SceneRequest mCurrentRequest;

      /// <summary>
      /// 是否正在切换中，防止并发切换请求导致场景状态错乱。
      /// </summary>
      private bool mSwitching;

      /// <summary>
      /// Loading Canvas 的 CanvasGroup（渐显渐隐目标，取 LoadingGroup 根的第一个子对象）。
      /// </summary>
      private CanvasGroup mLoadingCanvasGroup;

      /// <summary>
      /// Loading 进度文本（Loading Canvas 下 TMP 文本，用于显示加载百分比）。
      /// </summary>
      private TMP_Text mProgressText;

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

         StartCoroutine(LoadRoutine(request));
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

         // 当前阶段按场景名经 Build Settings 加载；热更新接入后按同名 key 走资源系统
         var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
         if (operation == null)
         {
            Debug.LogError($"[SceneFlow] 场景加载失败，请确认已加入 Build Settings：{sceneName}");
            yield return FinishWithError(showLoading);
            yield break;
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

         // 卸载旧玩法场景（可按请求保留；入口场景始终保护）
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

         mSwitching = false;
         Debug.Log($"[SceneFlow] 场景切换完成 -> {sceneName}");
      }

      /// <summary>
      /// 卸载除入口场景（Build Settings[0]）与目标场景之外的全部已加载场景。
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

            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null)
            {
               yield return operation;
            }
         }
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
         if (canvasGroup == null || duration <= 0f)
         {
            canvasGroup.alpha = to;
            yield break;
         }

         var elapsed = 0f;
         while (elapsed < duration)
         {
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
