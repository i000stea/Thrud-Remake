using System;
using System.Collections;

namespace TeaGame.App
{
   /// <summary>
   /// 内容后端接口（23 文档 §3）：唯一负责「从哪儿取清单、从哪儿取文件」的抽象。
   ///
   /// **职责边界**：只发 HTTP（或将来别的传输），不解析清单、不校验 hash、不决定落盘位置。
   /// 换 CDN 时只换实现（甚至只换 baseUrl），清单格式、缓存目录、ResKit 加载代码与场景 key 全不变。
   ///
   /// **禁止**把 UnityWebRequest 直接写进 SceneFlow / 面板 / 业务系统——那样换服务器地址就要改业务代码，
   /// 而且断线重试、.part 原子写入这些规则会散落成多份。
   /// </summary>
   public interface IHotUpdateContentBackend
   {
      /// <summary>后端自述（写进日志，便于判断「这次跑的是哪个服务器」）。</summary>
      string DisplayName { get; }

      /// <summary>
      /// 取版本清单文本。成功回调 <paramref name="onText"/>，失败回调 <paramref name="onError"/>（含具体原因）。
      /// 两者恰有一个被调用。
      /// </summary>
      IEnumerator GetManifestAsync(Action<string> onText, Action<string> onError);

      /// <summary>
      /// 下载一个文件到 <paramref name="destinationPath"/>（调用方给的是 `.part` 临时路径）。
      /// <paramref name="onProgress"/> 报告 0..1 进度，完成回调 <paramref name="onDone"/>，
      /// 失败回调 <paramref name="onError"/>。传输层不校验 hash——校验属于缓存层。
      ///
      /// <paramref name="shouldAbort"/>：**暂停/停止的唯一入口**。传输层每帧问一次，返回 true 就中断本次传输
      /// 并走 <paramref name="onError"/>（由调用方根据自己置的暂停/停止标记来判断这是「用户操作」还是「真失败」）。
      /// 之所以不由传输层直接暴露 Pause/Resume：服务端不支持 Range（无断点续传），
      /// 已传的半个文件无法复用，暂停只能「中断并丢弃当前文件」，恢复时整文件重下——
      /// 这个语义必须由调用方显式决定，不能藏在传输层里。
      /// </summary>
      IEnumerator DownloadFileAsync(HotUpdateFileEntry entry, string destinationPath,
         Action<float> onProgress, Action onDone, Action<string> onError, Func<bool> shouldAbort = null);
   }
}
