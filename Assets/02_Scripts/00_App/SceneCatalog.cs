using System.Collections.Generic;

namespace TeaGame.App
{
   /// <summary>
   /// 场景 key 目录（AOT 层，23 文档 S6）。
   ///
   /// **为什么需要它**：场景统一改名为 `scene_0000_xxx` 后，场景名不再只出现在
   /// 各模块自己的常量里——登录（AOT）、战斗契约（热更）、Editor 工具（Editor）都要用同一批 key。
   /// 把 key 与「场景 → AssetBundle」的映射集中在这里，任何调用点都不需要写字面量，
   /// 也就不需要 AOT 层去引用热更层（`TeaGame.Menu` / `TeaGame.Battle.Contracts`）的类型。
   ///
   /// **不在这里做的事**：不提供加载方法。加载通路只有 <see cref="SceneFlow"/> 一条，
   /// 这里只回答「这个 key 叫什么」和「它来自哪个 bundle」。禁止在 SceneFlow 里写
   /// `if (sceneName == "...")` 之类的分支——查这张表即可。
   /// </summary>
   public static class SceneCatalog
   {
      /// <summary>常驻管理场景（Build Settings[0]，AOT 入口）。</summary>
      public const string Config = "scene_0000_config";

      /// <summary>登录场景。</summary>
      public const string Login = "scene_0001_login";

      /// <summary>主菜单/主城场景（第一阶段唯一走 AssetBundle 的场景）。</summary>
      public const string Main = "scene_0002_main";

      /// <summary>抽卡场景（当前未启用）。</summary>
      public const string Gacha = "scene_0003_gacha";

      /// <summary>灰盒战斗场景。</summary>
      public const string BattleGraybox = "scene_0004_battle_graybox";

      /// <summary>旧战斗场景（已从 Build Settings 停用，保留资产）。</summary>
      public const string BattleLegacy = "scene_0005_battle_legacy";

      /// <summary>战斗测试场景 0（未启用）。</summary>
      public const string BattleTest0 = "scene_0006_battle_test0";

      /// <summary>战斗测试场景 1（未启用）。</summary>
      public const string BattleTest1 = "scene_0007_battle_test1";

      /// <summary>
      /// 主菜单场景的 AssetBundle 名（与 <c>QAssetBundle.Main_scene_0002_main.BundleName</c> 同源，
      /// 由 ResKit 构建时重新生成为一致值）。
      /// </summary>
      public const string MainAssetBundle = QAssetBundle.Main_scene_0002_main.BundleName;

      /// <summary>
      /// 已纳入 AssetBundle 管理的场景：场景 key → bundle 名。
      /// 未登记的场景按 <see cref="SceneSource.BuildSettings"/> 处理。
      /// 新场景要热更时，在这里登记一行 + 在 Editor 里标记 bundle 名即可，加载代码零改动。
      /// </summary>
      private static readonly Dictionary<string, string> sSceneBundles = new()
      {
         { Main, MainAssetBundle },
      };

      /// <summary>
      /// 查询场景是否走 AssetBundle，并取出 bundle 名。
      /// </summary>
      /// <param name="sceneName">场景 key（场景名称）。</param>
      /// <param name="assetBundleName">命中时返回 bundle 名，否则为 null。</param>
      /// <returns>该场景是否已登记为 AssetBundle 场景。</returns>
      public static bool TryGetAssetBundle(string sceneName, out string assetBundleName)
      {
         assetBundleName = null;
         return !string.IsNullOrEmpty(sceneName) && sSceneBundles.TryGetValue(sceneName, out assetBundleName);
      }

      /// <summary>
      /// 按场景目录构造一次切换请求：登记过 bundle 的场景走 <see cref="SceneSource.AssetBundle"/>，
      /// 其余走 <see cref="SceneSource.BuildSettings"/>。
      ///
      /// **所有调用点都应经此构造**，不要自己 new SceneRequest 再填 SceneName。
      /// 原因很具体：主菜单移出 Build Settings 之后，任何「只传场景名」的请求
      /// （典型是战斗结束回主菜单的 <c>ReturnSceneName</c>）都会当场失效，
      /// 而这类回跳路径散落在暂停/结算/编队多个面板里，逐个补源字段必漏。
      /// </summary>
      public static SceneRequest CreateRequest(string sceneName)
      {
         var request = new SceneRequest { SceneName = sceneName };

         if (TryGetAssetBundle(sceneName, out var assetBundleName))
         {
            request.Source = SceneSource.AssetBundle;
            request.AssetBundleName = assetBundleName;
         }

         return request;
      }
   }
}
