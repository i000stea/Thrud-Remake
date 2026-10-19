using System;

namespace TeaGame.App
{
   /// <summary>
   /// 场景来源（23 文档 S4）：决定 <see cref="SceneFlow"/> 走哪条加载通路。
   /// 刻意放在 AOT 层，任何模块构造请求时都只依赖这个枚举，不依赖具体资源系统。
   /// </summary>
   public enum SceneSource
   {
      /// <summary>经 <c>SceneManager</c> 按名称加载，要求场景已加入 Build Settings。</summary>
      BuildSettings = 0,

      /// <summary>
      /// 经 ResKit 从场景 AssetBundle 加载，要求场景已标记 bundle 名且
      /// ResKit 清单（asset_bundle_config.bin）里有该 bundle/asset 条目。
      /// </summary>
      AssetBundle = 1,
   }

   /// <summary>
   /// 场景切换请求。
   /// 场景 key 即**场景名称字符串**（决策 D25）；统一命名见 <see cref="SceneCatalog"/>。
   /// 目标场景通过 <see cref="SceneFlow.GetPayload{T}"/> 读取跨场景参数。
   /// </summary>
   public sealed class SceneRequest
   {
      /// <summary>场景 key（即场景名称）。</summary>
      public string SceneName;

      /// <summary>
      /// 场景来源。默认 <see cref="SceneSource.BuildSettings"/>：保持既有调用点行为不变；
      /// 走资源系统的场景必须显式声明 <see cref="SceneSource.AssetBundle"/>。
      /// </summary>
      public SceneSource Source = SceneSource.BuildSettings;

      /// <summary>
      /// 场景所在 AssetBundle 名（仅 <see cref="SceneSource.AssetBundle"/> 时使用）。
      /// 由 <see cref="SceneCatalog"/> 统一提供，调用点不要写字面量。
      /// </summary>
      public string AssetBundleName;

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
      /// 是否先卸载旧玩法场景（仅保留入口常驻场景）再加载目标。
      /// 默认 false：加载目标后再卸载旧玩法场景（首进场景用，避免黑屏等待）；
      /// 置 true 用于「重开本关」等原地重载——先清场再加载，杜绝新旧同场景实例并存导致状态错乱。
      /// </summary>
      public bool UnloadFirst;

      /// <summary>
      /// 加载完成回调（场景切换协程收尾后触发，成功或失败均调用一次）。
      /// 用于通知调用方切换流程已结束，可在此处执行后续逻辑。
      /// </summary>
      public Action OnComplete;
   }
}
