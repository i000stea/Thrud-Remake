using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame.App
{
   /// <summary>
   /// 启动编排（常驻于 ConfigScene 的 GameRoot）。
   /// 负责应用启动时的全局初始化，并在作为入口场景时加载首个玩法场景。
   /// </summary>
   public sealed class AppBootstrapper : MonoBehaviour
   {
      /// <summary>
      /// 首个玩法场景（Build Settings[0] 为 ConfigScene 时加载）。
      /// </summary>
      [SerializeField] private GameScene firstScene = GameScene.Login;

      /// <summary>
      /// 场景引用表（由 ConfigScene 场景注入；未配置时兜底加载）。
      /// </summary>
      [SerializeField] private SceneRefTable sceneRefTable;

      /// <summary>
      /// 场景切换控制器引用。
      /// </summary>
      private SceneFlow mSceneFlow;

      /// <summary>
      /// 全局初始化。
      /// 当前为骨架占位：日志/埋点（TelemetryLog，M0 接入）、全局配置加载（C4 迁入后接管）。
      /// 注入场景引用表实例，供全局静态访问。
      /// </summary>
      private void Awake()
      {
         if (sceneRefTable == null)
         {
            sceneRefTable = SceneRefTable.Get();
         }

         SceneRefTable.SetCurrent(sceneRefTable);

         mSceneFlow = GetComponent<SceneFlow>();
         if (mSceneFlow == null)
         {
            mSceneFlow = gameObject.AddComponent<SceneFlow>();
         }
      }

      /// <summary>
      /// 加载首个玩法场景。
      /// 仅当 ConfigScene 是应用入口（当前 active scene 为自己）时执行；
      /// 若由编辑器守卫拉起（用户在玩法场景直接 Play），则不打扰当前场景。
      /// </summary>
      private void Start()
      {
         var configSceneName = sceneRefTable != null ? sceneRefTable.ConfigSceneName : null;
         if (configSceneName != null && SceneManager.GetActiveScene().name != configSceneName)
         {
            Debug.Log("[AppBootstrapper] 当前非入口场景，跳过首个场景加载（由 ConfigSceneGuard 拉起）。");
            return;
         }

         mSceneFlow.RequestLoad(new SceneRequest { Scene = firstScene });
      }
   }
}
