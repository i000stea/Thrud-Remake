using QFramework;

namespace TeaGame.Login
{
   /// <summary>
   /// 登录流程状态模型。
   /// 当前为轻量占位：防重入标记；后续可扩展登录步骤、账号信息等。
   /// </summary>
   public sealed class LoginModel : AbstractModel
   {
      /// <summary>
      /// 登录流程是否进行中（热更检查/切换场景期间防重复点击）。
      /// </summary>
      public bool IsBusy;

      protected override void OnInit()
      {
      } 
   }
}
