using System.Collections;
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
      /// 全局初始化 + 加载首个玩法场景。
      /// 先初始化 ResKit（真机必需：编辑器仅 SimulationMode 下由 <c>ResKit.CheckAutoInit</c> 自动
      /// 初始化；打包后 SimulationMode=false 且无人调用 → AB 清单未加载，UIKit 面板经 ResKit 加载
      /// 全部失败「Failed to Create Res」。此处统一显式 InitAsync，编辑器下幂等跳过）。
      /// 首个场景加载仅当 ConfigScene 是应用入口（当前 active scene 为入口场景）时执行；
      /// 若由编辑器守卫拉起（用户在玩法场景直接 Play），则不打扰当前场景。
      /// </summary>
      private IEnumerator Start()
      {
         // ResKit 初始化：真机读 StreamingAssets/AssetBundles/<platform>/asset_bundle_config.bin
         //（Android 需异步加载，故用 InitAsync）；编辑器 SimulationMode 已自动 Init，这里立即完成
         yield return QFramework.ResKit.InitAsync();
         Debug.Log("[AppBootstrapper] ResKit 初始化完成。");

         var entrySceneName = SceneAnchor.GetEntrySceneName();
         if (entrySceneName == null || SceneManager.GetActiveScene().name != entrySceneName)
         {
            Debug.Log("[AppBootstrapper] 当前非入口场景，跳过首个场景加载（由守卫拉起）。");
            yield break;
         }

         mSceneFlow.RequestLoad(new SceneRequest { SceneName = firstSceneName, ShowLoading = false });
      }
   }
}
