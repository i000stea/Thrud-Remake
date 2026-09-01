namespace TeaGame.Login
{
   /// <summary>
   /// 登录完成事件。
   /// 登录流程（热更检查 → 准备就绪）完成后由 LoginSystem 派发，
   /// 表现层订阅后执行音频淡出等收尾演出。
   /// </summary>
   public struct LoginCompletedEvent
   {
   }
}
