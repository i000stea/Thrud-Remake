using QFramework;

namespace TeaGame
{
   /// <summary>
   /// 登录模块架构入口。
   /// 注册登录相关的 Model 与 System；场景表现层（LoginViewController）经此架构驱动逻辑。
   /// </summary>
   public sealed class LoginArchitecture : Architecture<LoginArchitecture>
   {
      protected override void Init()
      {
         this.RegisterModel(new Login.LoginModel());
         this.RegisterSystem(new Login.LoginSystem());
      }
   }
}
