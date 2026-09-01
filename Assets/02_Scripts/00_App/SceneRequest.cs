namespace TeaGame.App
{
   /// <summary>
   /// 场景切换请求。
   /// 场景 key 即**场景名称字符串**（决策 D25）：
   /// - 玩法场景按名称经 SceneManager 加载（当前 Build Settings 阶段）
   /// - 热更新新增场景时，按同名 key 从资源系统加载，同一 key 无缝衔接，无需改代码
   /// 目标场景通过 <see cref="SceneFlow.GetPayload{T}"/> 读取跨场景参数。
   /// </summary>
   public sealed class SceneRequest
   {
      /// <summary>场景 key（即场景名称）。</summary>
      public string SceneName;

      /// <summary>
      /// 跨场景参数（如关卡 ID、编队数据、回跳目标）。
      /// 由 SceneFlow 暂存，目标场景在加载完成后读取。
      /// </summary>
      public object Payload;

      /// <summary>是否显示全局 Loading 遮罩。默认 true。</summary>
      public bool ShowLoading = true;
   }
}
