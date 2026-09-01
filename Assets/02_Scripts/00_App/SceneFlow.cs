using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame.App
{
   /// <summary>
   /// 场景切换控制器（常驻于 ConfigScene）。
   /// 负责所有玩法场景的 additive 加载、卸载与 active scene 管理。
   /// 切换策略（决策 D20）：卸载旧玩法场景，入口场景（Build Settings[0]）永驻。
   /// 场景 key 即场景名称字符串（决策 D25），按名称加载，热更新可按同名 key 无缝接入。
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

      /// <summary>
      /// 当前切换请求，目标场景通过 <see cref="GetPayload{T}"/> 读取参数。
      /// </summary>
      private SceneRequest mCurrentRequest;

      /// <summary>
      /// 是否正在切换中，防止并发切换请求导致场景状态错乱。
      /// </summary>
      private bool mSwitching;

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
      /// 全局 Loading 遮罩根节点（编辑器与运行时均可设置）。
      /// </summary>
      public Transform LoadingGroupRoot
      {
         get => loadingGroupRoot;
         set => loadingGroupRoot = value;
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
      /// 切换协程：遮罩 → additive 加载 → 设 active → 卸载旧玩法场景 → 关遮罩。
      /// </summary>
      private IEnumerator LoadRoutine(SceneRequest request)
      {
         mSwitching = true;
         ShowLoading(request.ShowLoading);
         yield return null; // 等一帧让遮罩生效

         var sceneName = request.SceneName;
         if (string.IsNullOrEmpty(sceneName))
         {
            Debug.LogError("[SceneFlow] 场景 key（名称）为空，无法切换。");
            HideLoading();
            mSwitching = false;
            yield break;
         }

         // 当前阶段按场景名经 Build Settings 加载；热更新接入后按同名 key 走资源系统
         var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
         if (operation == null)
         {
            Debug.LogError($"[SceneFlow] 场景加载失败，请确认已加入 Build Settings：{sceneName}");
            HideLoading();
            mSwitching = false;
            yield break;
         }

         while (!operation.isDone)
         {
            yield return null;
         }

         var target = SceneManager.GetSceneByName(sceneName);
         if (!target.IsValid())
         {
            Debug.LogError($"[SceneFlow] 场景无效：{sceneName}");
            HideLoading();
            mSwitching = false;
            yield break;
         }

         // 场景加载完成后立即设为 active，保证 UI 事件与相机顺序正确
         SceneManager.SetActiveScene(target);

         mCurrentRequest = request;
         yield return UnloadPlayScenes(target);

         HideLoading();
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
      /// 显示全局 Loading 遮罩。
      /// </summary>
      private void ShowLoading(bool show)
      {
         if (!show) return;

         if (loadingGroupRoot == null)
         {
            loadingGroupRoot = TryLoadLoadingGroupFromResources();
         }

         if (loadingGroupRoot)
         {
            loadingGroupRoot.gameObject.SetActive(true);
         }
      }

      /// <summary>
      /// 关闭全局 Loading 遮罩。
      /// </summary>
      private void HideLoading()
      {
         if (loadingGroupRoot)
         {
            loadingGroupRoot.gameObject.SetActive(false);
         }
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
