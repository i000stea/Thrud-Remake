using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TeaGame.App
{
   /// <summary>
   /// 局域网静态 HTTP 后端（23 文档 §3 的第一期实现）。
   /// 约定路由与服务端 `web/LanOtaServer` 一致：
   /// <code>
   /// {baseUrl}/{channel}/{platform}/manifest.json
   /// {baseUrl}/{channel}/{platform}/{entry.path}
   /// </code>
   /// 未来换 CDN 只需要 `new LocalHttpBackend("https://cdn.example.com/content", ...)`，
   /// 或者在 `IHotUpdateContentBackend` 上写第二套实现（带签名的私有协议等），业务代码零改动。
   /// </summary>
   public sealed class LocalHttpBackend : IHotUpdateContentBackend
   {
      private readonly string mBaseUrl;
      private readonly string mChannel;
      private readonly string mPlatform;
      private readonly float mManifestTimeoutSeconds;

      /// <param name="baseUrl">服务器根地址，如 `http://192.168.1.57:8080`（不带结尾斜杠亦可）。</param>
      /// <param name="channel">渠道名，如 `lan`。</param>
      /// <param name="platform">平台名，如 `Android`。</param>
      /// <param name="manifestTimeoutSeconds">清单请求超时（秒）。下载不设总时长上限——首包可能很大。</param>
      public LocalHttpBackend(string baseUrl, string channel, string platform, float manifestTimeoutSeconds)
      {
         mBaseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
         mChannel = channel;
         mPlatform = platform;
         mManifestTimeoutSeconds = manifestTimeoutSeconds > 0f ? manifestTimeoutSeconds : 10f;
      }

      public string DisplayName => $"LocalHttp({mBaseUrl})";

      /// <summary>拼出平台目录前缀：`{base}/{channel}/{platform}`。</summary>
      private string PlatformUrl => $"{mBaseUrl}/{mChannel}/{mPlatform}";

      public IEnumerator GetManifestAsync(Action<string> onText, Action<string> onError)
      {
         var url = PlatformUrl + "/manifest.json";

         UnityWebRequest request;
         try
         {
            // Unity 在 `Allow downloads over HTTP = Not allowed`（默认）时会在这里抛
            // `InvalidOperationException: Insecure connection not allowed`，必须兜住，
            // 否则调用方协程会被静默打断、状态卡在中途。
            request = UnityWebRequest.Get(url);
         }
         catch (Exception e)
         {
            onError?.Invoke($"{url} -> {e.Message}");
            yield break;
         }

         using (request)
         {
            request.timeout = Mathf.CeilToInt(mManifestTimeoutSeconds);

            UnityWebRequestAsyncOperation operation;
            try
            {
               operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
               onError?.Invoke($"{url} -> {e.Message}");
               yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
               onError?.Invoke($"{url} -> {request.responseCode} {request.error}");
               yield break;
            }

            var text = StripBom(request.downloadHandler.text);
            if (string.IsNullOrEmpty(text))
            {
               onError?.Invoke($"{url} -> 内容为空");
               yield break;
            }

            onText?.Invoke(text);
         }
      }

      public IEnumerator DownloadFileAsync(HotUpdateFileEntry entry, string destinationPath,
         Action<float> onProgress, Action onDone, Action<string> onError, Func<bool> shouldAbort = null)
      {
         if (entry == null || string.IsNullOrEmpty(entry.path))
         {
            onError?.Invoke("条目缺少 path，无法拼下载地址");
            yield break;
         }

         var url = PlatformUrl + "/" + entry.path.TrimStart('/');
         var directory = Path.GetDirectoryName(destinationPath);
         if (!string.IsNullOrEmpty(directory))
         {
            Directory.CreateDirectory(directory);
         }

         // DownloadHandlerFile 直接写盘，避免把几十 MB 的 AssetBundle 先读进内存。
         // 不设 timeout：总超时会把「大文件但在正常下载」误判为失败；
         // 卡死由调用方的整体流程与 .part 机制兜住（失败即丢弃临时文件，旧版本不受影响）。
         UnityWebRequest request;
         try
         {
            request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            // handler 的所有权交给 request（request.Dispose 时会释放它），不要再单独 using
            request.downloadHandler = new DownloadHandlerFile(destinationPath, true);
         }
         catch (Exception e)
         {
            onError?.Invoke($"{url} -> {e.Message}");
            yield break;
         }

         using (request)
         {
            UnityWebRequestAsyncOperation operation;
            try
            {
               operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
               onError?.Invoke($"{url} -> {e.Message}");
               yield break;
            }

            while (!operation.isDone)
            {
               // 暂停/停止：每帧问一次，命中就中断本次传输（半成品由调用方删除，服务端无 Range 不能续传）
               if (shouldAbort != null && shouldAbort())
               {
                  request.Abort();
                  yield return request;
                  onError?.Invoke("传输已中止（" + url + "）");
                  yield break;
               }

               onProgress?.Invoke(request.downloadProgress);
               yield return null;
            }

            onProgress?.Invoke(1f);

            if (request.result != UnityWebRequest.Result.Success)
            {
               onError?.Invoke($"{url} -> {request.responseCode} {request.error}");
               yield break;
            }

            onDone?.Invoke();
         }
      }

      /// <summary>
      /// 剥掉 UTF-8 BOM。JsonUtility 遇到以 U+FEFF 开头的文本会直接报
      /// `JSON parse error: Invalid value`——热更清单的导出工具曾经踩过这个坑，这里统一兜住。
      /// </summary>
      private static string StripBom(string text)
      {
         if (string.IsNullOrEmpty(text)) return text;
         return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
      }
   }
}
