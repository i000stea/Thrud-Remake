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
      /// <summary> 首个玩法场景 key（场景名称，可在 Inspector 调整）。 </summary>
      [SerializeField] private string firstSceneName = SceneCatalog.Login;

      [Header("OTA 内容更新（23 文档：局域网 HTTP → CDN）")]
      [Tooltip("关闭时完全不联网，直接使用包内基线（首包默认态）。")]
      [SerializeField] private HotUpdateOtaSettings ota = new HotUpdateOtaSettings();

      /// <summary> 场景切换控制器引用。  </summary>
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
         // OTA 内容更新必须在加载任何热更/AB 之前：它要把 DLL 与 bundle 落到 persistentDataPath，
         // 而 HotUpdateLoader 与 ResKit 都是「persistent 优先、StreamingAssets 回退」。
         // 方法内部对网络/校验失败一律兜底为「保持当前可用版本」，不会阻断启动。
         yield return HotUpdateOtaService.RunAtBoot(ota);

         // 热更装配加载必须最靠前：0ConfigScene 是纯 AOT 场景，从这里往后才允许触碰
         // TeaGame.*（热更程序集）的类型。编辑器下该方法内部直接跳过。
         yield return HotUpdateLoader.LoadAll();
         if (!HotUpdateLoader.IsReady)
         {
            Debug.LogError("[AppBootstrapper] 热更装配未就绪：后续依赖热更程序集的流程会失败。");
         }

         // ResKit 初始化：真机读 StreamingAssets/AssetBundles/<platform>/asset_bundle_config.bin
         //（Android 需异步加载，故用 InitAsync）；编辑器 SimulationMode 已自动 Init，这里立即完成。
         yield return QFramework.ResKit.InitAsync();

         // P0：InitAsync 只保证"跑过"，不保证"读到清单"（mResMgrInited 在读到清单前置位）。
         // 这里显式校验一次，失败时只报一条可执行的错误，避免下游出现"Failed to Create Res. Not Find By
         // ResSearchKeys"这类容易被误判为"资源没进包"的日志。
         var resKitReady = ResKitBootstrap.Validate();
         Debug.Log(resKitReady
            ? "[AppBootstrapper] ResKit 初始化完成（AB 清单校验通过）。"
            : "[AppBootstrapper] ResKit 初始化完成，但 AB 清单校验失败——见上一条 [ResKit] 错误。");

         var entrySceneName = SceneAnchor.GetEntrySceneName();
         if (entrySceneName == null || SceneManager.GetActiveScene().name != entrySceneName)
         {
            Debug.Log("[AppBootstrapper] 当前非入口场景，跳过首个场景加载（由守卫拉起）。");
            yield break;
         }

         // 经场景目录构造请求：登录场景未登记 AssetBundle，仍走 Build Settings，
         // 通道选择统一由 SceneCatalog 决定，这里不写死来源。
         var loginRequest = SceneCatalog.CreateRequest(firstSceneName);
         loginRequest.ShowLoading = false;
         mSceneFlow.RequestLoad(loginRequest);
      }
   }
}
