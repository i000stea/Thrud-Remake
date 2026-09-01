using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame.App
{
   /// <summary>
   /// 启动编排（常驻于 ConfigScene 的 GameRoot）。
   /// 负责应用启动时的全局初始化，并在作为入口场景时加载首个玩法场景。
   /// 场景 key 使用名称字符串（决策 D25），入口场景经 <see cref="SceneAnchor"/> 动态锚定（不写死）。
   /// </summary>
   public sealed class AppBootstrapper : MonoBehaviour
   {
      /// <summary>
      /// 首个玩法场景 key（场景名称，可在 Inspector 调整）。
      /// </summary>
      [SerializeField] private string firstSceneName = "1LoginScene";  

      /// <summary>
      /// 场景切换控制器引用。
      /// </summary>
      private SceneFlow mSceneFlow;

      /// <summary>
      /// 全局初始化。
      /// 当前为骨架占位：日志/埋点（TelemetryLog，M0 接入）、全局配置加载（C4 迁入后接管）。
      /// </summary>
      private void Awake()
      {
         mSceneFlow = GetComponent<SceneFlow>();
         if (mSceneFlow == null)
         {
            mSceneFlow = gameObject.AddComponent<SceneFlow>();
         }
      }

      /// <summary>
      /// 加载首个玩法场景。
      /// 仅当 ConfigScene 是应用入口（当前 active scene 为入口场景）时执行；
      /// 若由编辑器守卫拉起（用户在玩法场景直接 Play），则不打扰当前场景。
      /// </summary>
      private void Start()
      {
         var entrySceneName = SceneAnchor.GetEntrySceneName();
         if (entrySceneName == null || SceneManager.GetActiveScene().name != entrySceneName)
         {
            Debug.Log("[AppBootstrapper] 当前非入口场景，跳过首个场景加载（由守卫拉起）。");
            return;
         }

         mSceneFlow.RequestLoad(new SceneRequest { SceneName = firstSceneName });
      }
   }
}
