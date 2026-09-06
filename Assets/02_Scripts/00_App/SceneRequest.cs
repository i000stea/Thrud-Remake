using System;

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

      /// <summary>
      /// 是否保留旧玩法场景（不卸载）。
      /// 默认 false：切换后卸载旧玩法场景（决策 D20）；
      /// 置 true 时按需保留（如主城常驻等场景），由调用方显式声明。
      /// </summary>
      public bool KeepPrevious;

      /// <summary>
      /// 加载完成回调（场景切换协程收尾后触发，成功或失败均调用一次）。
      /// 用于通知调用方切换流程已结束，可在此处执行后续逻辑。
      /// </summary>
      public Action OnComplete;
   }
}
