using System.IO;

namespace TeaGame.App
{
   /// <summary>
   /// 场景路径与名称常量。
   /// 所有场景切换统一引用本类，禁止魔法字符串。
   /// 场景路径相对 Assets 目录，供 SceneManager.LoadSceneAsync 使用。
   /// </summary>
   public static class SceneNames
   {
      /// <summary>常驻管理场景名称。</summary>
      public const string ConfigScene = "ConfigScene";

      /// <summary>常驻管理场景路径（Build Settings[0]，应用入口）。</summary>
      public const string ConfigScenePath = "Assets/01_Scenes/ConfigScene.unity";

      /// <summary>登录场景路径。</summary>
      public const string LoginScenePath = "Assets/01_Scenes/0LoginScene.unity";

      /// <summary>主城场景路径。</summary>
      public const string MainScenePath = "Assets/01_Scenes/1MainScene.unity";

      /// <summary>抽卡场景路径。</summary>
      public const string GachaScenePath = "Assets/01_Scenes/2GachaScene.unity";

      /// <summary>战斗场景路径。</summary>
      public const string BattleScenePath = "Assets/01_Scenes/3BattleScene.unity";

      /// <summary>战斗灰盒测试盒路径（Test0）。</summary>
      public const string BattleGrayboxPath = "Assets/01_Scenes/4BattleScene-Test0.unity";

      /// <summary>
      /// 根据游戏场景枚举获取场景路径。
      /// </summary>
      public static string GetPath(GameScene scene)
      {
         switch (scene)
         {
            case GameScene.Login: return LoginScenePath;
            case GameScene.MainCity: return MainScenePath;
            case GameScene.Gacha: return GachaScenePath;
            case GameScene.Battle: return BattleScenePath;
            case GameScene.BattleGraybox: return BattleGrayboxPath;
            default: return LoginScenePath;
         }
      }

      /// <summary>
      /// 根据游戏场景枚举获取场景名称（不含路径与扩展名）。
      /// </summary>
      public static string GetName(GameScene scene)
      {
         return Path.GetFileNameWithoutExtension(GetPath(scene));
      }
   }
}
