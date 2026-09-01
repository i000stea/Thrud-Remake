using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame.App
{
   /// <summary>
   /// 场景切换控制器（常驻于 ConfigScene）。
   /// 负责所有玩法场景的 additive 加载、卸载与 active scene 管理。
   /// 切换策略（决策 D20）：卸载旧玩法场景，ConfigScene 永驻。
   /// 场景引用经 <see cref="SceneRefTable"/> 获取，名称/路径不写死。
   /// </summary>
   public sealed class SceneFlow : MonoBehaviour
   {
      /// <summary>
      /// 场景引用表（场景资产引用与路径的权威来源）。
      /// 未注入时兜底 <see cref="SceneRefTable.Get"/>（Resources 加载）。
      /// </summary>
      [SerializeField] private SceneRefTable sceneRefTable;

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

      /// <summary>
      /// 全局 Loading 遮罩根节点（编辑器与运行时均可设置）。
      /// </summary>
      public Transform LoadingGroupRoot
      {
         get => loadingGroupRoot;
         set => loadingGroupRoot = value;
      }

      /// <summary>
      /// 场景引用表（供外部校验）。
      /// </summary>
      public SceneRefTable SceneRefTable => sceneRefTable;

      private void Awake()
      {
         if (sceneRefTable == null)
         {
            sceneRefTable = SceneRefTable.Get();
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
            Debug.LogWarning($"[SceneFlow] 正在切换中，忽略请求 -> {request.Scene}");
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

         var path = sceneRefTable != null ? sceneRefTable.GetPath(request.Scene) : null;
         if (string.IsNullOrEmpty(path))
         {
            Debug.LogError($"[SceneFlow] 场景 {request.Scene} 路径为空，请检查 SceneRefTable 配置。");
            HideLoading();
            mSwitching = false;
            yield break;
         }

         var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);

         if (operation == null)
         {
            Debug.LogError($"[SceneFlow] 场景加载失败，请确认已加入 Build Settings：{path}");
            HideLoading();
            mSwitching = false;
            yield break;
         }

         while (!operation.isDone)
         {
            yield return null;
         }

         var target = SceneManager.GetSceneByPath(path);
         if (!target.IsValid())
         {
            Debug.LogError($"[SceneFlow] 场景无效：{path}");
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
         Debug.Log($"[SceneFlow] 场景切换完成 -> {request.Scene}");
      }

      /// <summary>
      /// 卸载除 ConfigScene 与目标场景之外的全部已加载场景。
      /// ConfigScene 名称从场景引用表读取，不写死。
      /// </summary>
      private IEnumerator UnloadPlayScenes(Scene keepScene)
      {
         var configSceneName = sceneRefTable != null ? sceneRefTable.ConfigSceneName : string.Empty;

         for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
         {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            if (!string.IsNullOrEmpty(configSceneName) && scene.name == configSceneName) continue;
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
