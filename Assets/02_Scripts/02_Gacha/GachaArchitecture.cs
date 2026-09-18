using QFramework;

namespace TeaGame
{
   /// <summary>
   /// 作为底层内容，ab资源，账户，登录，场景切换等基础组件在该架构中
   /// 应用模块架构入口。 
   /// </summary>
   public sealed class GachaArchitecture : Architecture<GachaArchitecture>
   {
      /// <summary>
      /// 初始化架构，注册应用相关的 Model 与 System。
      /// </summary>
      protected override void Init()
      {
      }
   }
}
