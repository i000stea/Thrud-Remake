namespace TeaGame.App
{
   /// <summary>
   /// 游戏场景枚举（逻辑 ID，与场景文件名解耦）。
   /// 所有玩法场景以 additive 方式叠加在 ConfigScene（常驻管理场景）之上。
   /// 枚举 → 场景资产引用 的映射见 <see cref="SceneRefTable"/>（GUID 锚定，重命名/移动不失效）。
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
