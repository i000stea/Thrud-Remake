using QFramework;
using TeaGame.App;
using UnityEngine;

namespace TeaGame.Login
{
   /// <summary>
   /// 登录流程系统。
   /// 负责登录入口逻辑：防重入 → 资源热更新检查（占位，OTA 层接入后在此执行版本比对与下载）→ 切换主城。
   /// 场景切换统一走全局 <see cref="SceneFlow"/>（名称 key，热更新后按同 key 从场景 AssetBundle 加载）。
   /// </summary>
   public sealed class LoginSystem : AbstractSystem
   {
      private IArchitecture Architecture => AppArchitecture.Interface;
      private LoginModel Model => Architecture.GetModel<LoginModel>();

      protected override void OnInit()
      {
      }

      /// <summary>
      /// 发起登录流程。
      /// </summary>
      /// <param name="mainSceneKey">登录完成后进入的主城场景 key（场景名称）。</param>
      public void BeginLogin(string mainSceneKey)
      {
         if (Model.IsBusy)
         {
            return;
         }

         Model.IsBusy = true;
         StartHotUpdateCheck(mainSceneKey);
      }

      /// <summary>
      /// 资源热更新检查。
      /// 当前为占位：直接完成。OTA 层（23 文档）接入后在此执行版本比对 → 下载 → 校验 → 落地缓存，
      /// 代码 DLL 仍由 AppBootstrapper 里的 HybridCLR 加载器装配。
      /// 登录场景承担热更检查，完成后进入主城（决策 D25：目标场景 key 由调用方传入，可配置）。
      /// </summary>
      private void StartHotUpdateCheck(string mainSceneKey)
      {
         Debug.Log("[LoginSystem] 热更新检查（占位：OTA 层接入后执行真实版本比对与下载）。");
         CompleteLogin(mainSceneKey);
      }

      /// <summary>
      /// 登录完成：派发完成事件并请求切换到主城。
      /// </summary>
      private void CompleteLogin(string mainSceneKey)
      {
         Architecture.SendEvent<LoginCompletedEvent>();

         if (string.IsNullOrEmpty(mainSceneKey))
         {
            Debug.LogError("[LoginSystem] 主城场景 key 为空，无法切换。请检查 LoginViewController 配置。");
            return;
         }

         SceneFlow.Instance?.RequestLoad(BuildMainSceneRequest(mainSceneKey));
      }

      /// <summary>
      /// 构造主菜单场景请求（23 文档 S6）。
      /// 已登记在 <see cref="SceneCatalog"/> 里的场景走场景 AssetBundle——这是「整包替换主菜单」的前提；
      /// 未登记的场景退回 Build Settings，保持老路径可用。判定完全由场景目录给出，
      /// 因此 LoginScene 的 mainSceneKey 在 Inspector 里怎么改，加载通路都自动跟随。
      /// </summary>
      private static SceneRequest BuildMainSceneRequest(string mainSceneKey)
      {
         var request = new SceneRequest { SceneName = mainSceneKey };

         if (SceneCatalog.TryGetAssetBundle(mainSceneKey, out var bundleName))
         {
            request.Source = SceneSource.AssetBundle;
            request.AssetBundleName = bundleName;
            Debug.Log($"[LoginSystem] 主菜单走场景 AssetBundle：bundle={bundleName}, scene={mainSceneKey}");
         }
         else
         {
            Debug.Log($"[LoginSystem] 主菜单走 Build Settings：scene={mainSceneKey}");
         }

         return request;
      }
   }
}
