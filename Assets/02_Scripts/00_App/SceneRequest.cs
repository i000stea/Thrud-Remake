namespace TeaGame.App
{
   /// <summary>
   /// 场景切换请求。
   /// 由任意模块构造并交给 <see cref="SceneFlow.RequestLoad"/>，
   /// 目标场景通过 <see cref="SceneFlow.GetPayload{T}"/> 读取跨场景参数。
   /// </summary>
   public sealed class SceneRequest
   {
      /// <summary>目标场景。</summary>
      public GameScene Scene;

      /// <summary>
      /// 跨场景参数（如关卡 ID、编队数据、回跳目标）。
      /// 由 SceneFlow 暂存，目标场景在加载完成后读取。
      /// </summary>
      public object Payload;

      /// <summary>是否显示全局 Loading 遮罩。默认 true。</summary>
      public bool ShowLoading = true;
   }
}
