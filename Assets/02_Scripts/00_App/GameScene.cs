namespace TeaGame.App
{
   /// <summary>
   /// 游戏场景枚举。
   /// 所有玩法场景以 additive 方式叠加在 ConfigScene（常驻管理场景）之上。
   /// 场景路径映射见 <see cref="SceneNames"/>。
   /// </summary>
   public enum GameScene
   {
      /// <summary>登录场景（本地占位）。</summary>
      Login,

      /// <summary>主城 2D 功能页。</summary>
      MainCity,

      /// <summary>抽卡场景。</summary>
      Gacha,

      /// <summary>战斗场景。</summary>
      Battle,

      /// <summary>战斗灰盒测试盒（当前指向 4BattleScene-Test0）。</summary>
      BattleGraybox,
   }
}
