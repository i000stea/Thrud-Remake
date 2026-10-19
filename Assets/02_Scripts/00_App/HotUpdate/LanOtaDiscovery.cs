using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TeaGame.App
{
   /// <summary>
   /// 局域网内容服务器发现（23 文档 §3 的「可替换后端」在地址层面的补充）。
   ///
   /// **要解决的问题**：PC 的局域网 IP 会变（DHCP / 换网络），设备端如果把 `127.0.0.1` 或某个固定 IP
   /// 写进配置，就会得到 `Cannot connect to destination host`。真机上的 `127.0.0.1` 还是手机自己。
   ///
   /// **解析顺序（先便宜后贵，命中即止）**：
   /// 1. 显式配置的 baseUrl（开发时想固定指向某台机器就用它）；
   /// 2. `http://127.0.0.1:&lt;discoveryPort&gt;`——编辑器里跑、服务器就在本机，免配置；
   /// 3. 上次成功解析的地址（PlayerPrefs 缓存，下次启动免探测）；
   /// 4. **UDP 广播**：向 `255.255.255.255` 与各网卡的子网广播地址发 `THRUD-OTA-DISCOVER/1`，
   ///    服务端（`web/LanOtaServer`）回一段带 `httpPort` 的 JSON，据此拼出 `http://&lt;服务端IP&gt;:&lt;端口&gt;`。
   ///
   /// 每个候选都用 `HEAD /&lt;channel&gt;/&lt;platform&gt;/manifest.json` 验证——**只认能真的取到清单的地址**，
   /// 不是「能连上」就算命中。这样广播回来的错误地址、残留缓存都能被自动淘汰。
   /// 未来换 CDN 时把 CDN 地址写进配置即可，本类完全不参与。
   /// </summary>
   public static class LanOtaDiscovery
   {
      /// <summary>广播探测报文内容，必须与服务端 `DISCOVERY_MAGIC` 完全一致。</summary>
      public const string DiscoveryMagic = "THRUD-OTA-DISCOVER/1";

      /// <summary>缓存键（PlayerPrefs）。</summary>
      private const string CacheKey = "Thrud.Ota.BaseUrl";

      /// <summary>单个候选地址的校验超时（秒）。</summary>
      private const float ValidateTimeoutSeconds = 2f;

      /// <summary>广播重发间隔（秒）。丢一两个包不至于整次发现失败。</summary>
      private const float RebroadcastIntervalSeconds = 0.4f;

      /// <summary>上次成功解析到的地址（没有则为空）。</summary>
      public static string CachedBaseUrl => PlayerPrefs.GetString(CacheKey, string.Empty);

      /// <summary>记住成功地址，下次启动直接复用。</summary>
      public static void CacheBaseUrl(string baseUrl)
      {
         if (string.IsNullOrEmpty(baseUrl)) return;
         PlayerPrefs.SetString(CacheKey, baseUrl);
         PlayerPrefs.Save();
      }

      /// <summary>清掉缓存（地址变了又自动发现失败时手工用）。</summary>
      public static void ClearCache()
      {
         PlayerPrefs.DeleteKey(CacheKey);
         PlayerPrefs.Save();
      }

      /// <summary>
      /// 解析出一个**已验证可用**的内容服务器根地址。
      /// 成功回调 <paramref name="onResolved"/>，失败回调 <paramref name="onFailed"/>（含每个候选的失败原因）。
      /// </summary>
      /// <param name="explicitBaseUrl">显式配置的地址（可为空）。</param>
      /// <param name="channel">渠道名，用于拼校验路径。</param>
      /// <param name="platform">平台名，用于拼校验路径。</param>
      /// <param name="discoveryPort">UDP 广播端口（与服务端 `--discovery-port` 一致）。</param>
      /// <param name="timeoutSeconds">广播等待上限（秒）。</param>
      /// <param name="allowBroadcast">是否允许 UDP 广播发现。</param>
      public static IEnumerator ResolveAsync(string explicitBaseUrl, string channel, string platform,
         int discoveryPort, float timeoutSeconds, bool allowBroadcast,
         Action<string> onResolved, Action<string> onFailed)
      {
         var tried = new List<string>();
         var resolved = (string)null;
         var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

         foreach (var candidate in BuildCandidates(explicitBaseUrl, discoveryPort, sources))
         {
            var ok = false;
            yield return Validate(candidate, channel, platform, value => ok = value);
            tried.Add($"{candidate}（{sources[candidate]}）{(ok ? " 命中" : " 不通")}");

            if (ok)
            {
               resolved = candidate;
               break;
            }
         }

         if (resolved == null && allowBroadcast)
         {
            yield return DiscoverByBroadcast(channel, platform, discoveryPort, timeoutSeconds,
               url => resolved = url, reason => tried.Add("UDP 广播：" + reason));
         }

         if (resolved != null)
         {
            CacheBaseUrl(resolved);
            Debug.Log($"[HotUpdate] 资源服务器已定位：{resolved}");
            onResolved?.Invoke(resolved);
            yield break;
         }

         var detail = tried.Count == 0 ? "没有可用的候选地址" : string.Join("；", tried.ToArray());
         Debug.LogError($"[HotUpdate] 无法定位内容服务器：{detail}。" +
                        "检查手机与 PC 是否同一网段（部分路由会隔离广播），或在 ConfigScene 的 AppBootstrapper 手填 ota.baseUrl。");
         onFailed?.Invoke(detail);
      }

      /// <summary>按「先便宜后贵」的顺序拼候选地址（列表保证顺序，字典记录来源用于日志）。</summary>
      private static List<string> BuildCandidates(string explicitBaseUrl, int discoveryPort,
         Dictionary<string, string> sources)
      {
         var list = new List<string>();

         void Add(string url, string source)
         {
            if (string.IsNullOrEmpty(url)) return;
            url = url.TrimEnd('/');
            if (sources.ContainsKey(url)) return;
            list.Add(url);
            sources[url] = source;
         }

         Add(explicitBaseUrl, "显式配置");

         if (discoveryPort > 0)
         {
            Add($"http://127.0.0.1:{discoveryPort}", "本机回环");
         }

         Add(CachedBaseUrl, "上次成功地址");
         return list;
      }

      /// <summary>
      /// 校验地址是否真的能取到清单（`HEAD /{channel}/{platform}/manifest.json` → 200）。
      ///
      /// **必须包住异常**：Unity 在 `Player Settings → Allow downloads over HTTP = Not allowed`（默认）时，
      /// 对非 localhost 的 http 地址会在**构造 UnityWebRequest 时**直接抛
      /// `InvalidOperationException: Insecure connection not allowed`。若让它冒出去，
      /// 整个发现协程会静默死掉、`Status` 永远停在「正在定位资源服务器」。
      /// 本工程已在 ProjectSettings 里把该项设为 Always allowed（局域网 HTTP 必需），这里再兜一层。
      /// </summary>
      private static IEnumerator Validate(string baseUrl, string channel, string platform, Action<bool> onResult)
      {
         var url = $"{baseUrl.TrimEnd('/')}/{channel}/{platform}/manifest.json";
         UnityWebRequest request = null;

         try
         {
            request = UnityWebRequest.Head(url);
         }
         catch (Exception e)
         {
            Debug.LogWarning($"[HotUpdate] 无法校验 {url}：{e.Message}");
            onResult?.Invoke(false);
            yield break;
         }

         using (request)
         {
            request.timeout = Mathf.CeilToInt(ValidateTimeoutSeconds);

            UnityWebRequestAsyncOperation operation;
            try
            {
               operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
               Debug.LogWarning($"[HotUpdate] 校验请求发送失败 {url}：{e.Message}");
               onResult?.Invoke(false);
               yield break;
            }

            yield return operation;

            var ok = request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
            if (!ok)
            {
               Debug.Log($"[HotUpdate] 校验未通过 {url}：{request.result} {request.responseCode} {request.error}");
            }

            onResult?.Invoke(ok);
         }
      }

      /// <summary>
      /// UDP 广播探测：发 `THRUD-OTA-DISCOVER/1`，收服务端回来的 JSON（含 httpPort），再校验。
      /// </summary>
      private static IEnumerator DiscoverByBroadcast(string channel, string platform, int discoveryPort,
         float timeoutSeconds, Action<string> onResolved, Action<string> onFailed)
      {
         if (discoveryPort <= 0 || discoveryPort > 65535)
         {
            onFailed?.Invoke("未配置合法端口");
            yield break;
         }

         UdpClient udp = null;
         try
         {
            udp = new UdpClient { EnableBroadcast = true };
         }
         catch (Exception e)
         {
            onFailed?.Invoke("UDP 初始化失败：" + e.Message);
            yield break;
         }

         var payload = Encoding.UTF8.GetBytes(DiscoveryMagic);
         var targets = BuildBroadcastTargets(discoveryPort);
         var socket = udp.Client;

         var realtimeDeadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, timeoutSeconds);
         var nextBroadcastAt = 0f;
         var found = (string)null;
         var sawReply = false;

         while (Time.realtimeSinceStartup < realtimeDeadline && found == null)
         {
            if (Time.realtimeSinceStartup >= nextBroadcastAt)
            {
               foreach (var target in targets)
               {
                  try
                  {
                     udp.Send(payload, payload.Length, target);
                  }
                  catch (Exception e)
                  {
                     Debug.LogWarning($"[HotUpdate] 广播到 {target} 失败：{e.Message}");
                  }
               }

               nextBroadcastAt = Time.realtimeSinceStartup + RebroadcastIntervalSeconds;
            }

            // 非阻塞收包：Available 为 0 就下一帧继续等
            while (socket.Available > 0)
            {
               IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
               string json;
               try
               {
                  json = Encoding.UTF8.GetString(udp.Receive(ref remote));
               }
               catch (Exception e)
               {
                  Debug.LogWarning("[HotUpdate] 广播回包读取失败：" + e.Message);
                  break;
               }

               sawReply = true;
               var httpPort = ParseIntField(json, "httpPort");
               if (httpPort <= 0)
               {
                  Debug.LogWarning("[HotUpdate] 忽略无效广播回包：" + json);
                  continue;
               }

               // 多网卡机器上回包的源地址不一定是客户端能连的（Hyper-V / VPN / 虚拟网卡），
               // 所以服务端会把所有局域网 IPv4 一并回过来，逐个校验，谁通就用谁。
               var candidates = new List<string>();
               foreach (var address in ParseStringArrayField(json, "addresses"))
               {
                  candidates.Add($"http://{address}:{httpPort}");
               }

               candidates.Add($"http://{remote.Address}:{httpPort}");

               foreach (var candidate in candidates)
               {
                  var ok = false;
                  yield return Validate(candidate, channel, platform, value => ok = value);
                  Debug.Log($"[HotUpdate] 广播发现候选：{candidate}{(ok ? " 可用" : " 不可用")}（{json}）");

                  if (!ok) continue;

                  found = candidate;
                  break;
               }

               if (found != null)
               {
                  break;
               }
            }

            yield return null;
         }

         try
         {
            udp.Close();
         }
         catch
         {
            /* 忽略 */
         }

         if (found != null)
         {
            onResolved?.Invoke(found);
         }
         else
         {
            onFailed?.Invoke(sawReply ? "有应答但清单不可用" : $"{Mathf.RoundToInt(timeoutSeconds * 1000)}ms 内无应答");
         }
      }

      /// <summary>
      /// 广播目标：`255.255.255.255` + 每块在用网卡的子网广播地址。
      /// 部分路由会丢掉受限广播，给子网广播地址能提高命中率。
      /// </summary>
      private static List<IPEndPoint> BuildBroadcastTargets(int discoveryPort)
      {
         var targets = new List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, discoveryPort) };

         try
         {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
               if (nic.OperationalStatus != OperationalStatus.Up) continue;
               if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

               foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
               {
                  if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                  var mask = unicast.IPv4Mask;
                  if (mask == null) continue;

                  var addressBytes = unicast.Address.GetAddressBytes();
                  var maskBytes = mask.GetAddressBytes();
                  if (addressBytes.Length != 4 || maskBytes.Length != 4) continue;

                  var broadcast = new byte[4];
                  for (var i = 0; i < 4; i++)
                  {
                     broadcast[i] = (byte)(addressBytes[i] | (~maskBytes[i] & 0xFF));
                  }

                  var endpoint = new IPEndPoint(new IPAddress(broadcast), discoveryPort);
                  if (!targets.Exists(t => t.Address.Equals(endpoint.Address)))
                  {
                     targets.Add(endpoint);
                  }
               }
            }
         }
         catch (Exception e)
         {
            Debug.LogWarning("[HotUpdate] 枚举网卡失败（只用 255.255.255.255）：" + e.Message);
         }

         return targets;
      }

      /// <summary>
      /// 从无依赖的极简 JSON 里抠一个字符串数组字段（形如 `"addresses":["1.2.3.4","5.6.7.8"]`）。
      /// 只做够用的解析，避免为一个地址列表引入 JSON 库。
      /// </summary>
      private static List<string> ParseStringArrayField(string json, string field)
      {
         var result = new List<string>();
         if (string.IsNullOrEmpty(json)) return result;

         var key = "\"" + field + "\"";
         var index = json.IndexOf(key, StringComparison.Ordinal);
         if (index < 0) return result;

         var open = json.IndexOf('[', index + key.Length);
         var close = open < 0 ? -1 : json.IndexOf(']', open + 1);
         if (open < 0 || close < 0) return result;

         var body = json.Substring(open + 1, close - open - 1);
         foreach (var part in body.Split(','))
         {
            var value = part.Trim().Trim('"').Trim();
            if (value.Length > 0)
            {
               result.Add(value);
            }
         }

         return result;
      }

      /// <summary>从无依赖的极简 JSON 里抠一个整数字段（避免为了一个端口引入解析器）。</summary>
      private static int ParseIntField(string json, string field)
      {
         if (string.IsNullOrEmpty(json)) return -1;

         var key = "\"" + field + "\"";
         var index = json.IndexOf(key, StringComparison.Ordinal);
         if (index < 0) return -1;

         var colon = json.IndexOf(':', index + key.Length);
         if (colon < 0) return -1;

         var value = 0;
         var hasDigit = false;
         for (var i = colon + 1; i < json.Length; i++)
         {
            var c = json[i];
            if (c >= '0' && c <= '9')
            {
               value = value * 10 + (c - '0');
               hasDigit = true;
               continue;
            }

            if (hasDigit) break;
            if (c == ' ' || c == '\t' || c == '"') continue;
            return -1;
         }

         return hasDigit ? value : -1;
      }
   }
}
