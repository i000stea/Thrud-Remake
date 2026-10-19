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
   /// 2. 上次成功解析的地址（PlayerPrefs 缓存，下次启动免探测）；
   /// 3. **UDP 广播**：向 `255.255.255.255` 与各网卡的子网广播地址发 `THRUD-OTA-DISCOVER/1`，
   ///    服务端（`web/LanOtaServer`）回一段带 `httpPort` 的 JSON，据此拼出 `http://&lt;服务端IP&gt;:&lt;端口&gt;`。
   /// 4. `http://127.0.0.1:&lt;discoveryPort&gt;`——仅作为最后兜底，真机上它指向手机自身。
   ///
   /// 每个候选都用 `HEAD /&lt;channel&gt;/&lt;platform&gt;/manifest.json` 验证——**只认能真的取到清单的地址**，
   /// 不是「能连上」就算命中。这样广播回来的错误地址、残留缓存都能被自动淘汰。
   /// 未来换 CDN 时把 CDN 地址写进配置即可，本类完全不参与。
   ///
   /// **实现约定（改动前先读）**：
   /// - 全部用**协程**（`IEnumerator` + `yield return`），不引入 `async/await` / `Task`：调用方是
   ///   Unity 的生命周期 / 协程链，`Action` 回调只在主线程触发，不需要考虑线程切换与锁。
   /// - 任何一步失败都**不抛异常**：冒出去的异常只会让协程静默结束，UI 永远停在「正在定位资源服务器」，
   ///   所以每个外部调用（`UnityWebRequest`、`UdpClient`、网卡枚举）都被 `try/catch` 包住，
   ///   转成 `Debug.LogWarning` + `onFailed` 里的一条可读原因。
   /// - `onResolved` / `onFailed` **恰好回调一个**，且都在协程末尾：调用方可以据此推进状态机，
   ///   不会出现两次成功或既成功又失败。
   /// - 探测顺序与 `BuildCandidates` 强耦合：`127.0.0.1` **不在**候选列表里，而是排在广播之后的兜底，
   ///   原因见 `ResolveAsync` 中该阶段的注释。
   /// - 命中判定只看「`HEAD` 能否取到清单 200」，不看延迟、网卡名或端口特征，
   ///   这样广播回来的错误地址、残留缓存里的旧地址都会被自动淘汰。
   /// </summary>
   public static class LanOtaDiscovery
   {
      /// <summary>
      /// 广播探测报文内容，必须与服务端 `DISCOVERY_MAGIC`（`web/LanOtaServer/server.js`）**一字不差**；
      /// 服务端把不匹配的包当垃圾丢弃，改了这里就必须同时改服务端，否则表现为「有发无收」。
      /// </summary>
      public const string DiscoveryMagic = "THRUD-OTA-DISCOVER/1";

      /// <summary>缓存键（PlayerPrefs）。带命名空间前缀避免与其他键冲突；改键会让已有缓存失效（可接受但别随手改）。</summary>
      private const string CacheKey = "Thrud.Ota.BaseUrl";

      /// <summary>
      /// 单个候选地址的校验超时（秒）。局域网 RTT 通常 &lt; 10ms，2s 足够判死；
      /// 设太大（如 10s）会让「所有候选都不通」的真机场景白等十几秒，而这段等待没有任何反馈。
      /// </summary>
      private const float ValidateTimeoutSeconds = 2f;

      /// <summary>
      /// 广播重发间隔（秒）。UDP 广播会被部分路由/省电策略丢弃，单发一轮命中率不高；
      /// 在等待窗口内每 0.4s 重发一轮，4s 窗口约能发 10 轮。默认等待窗口见调用方的 `timeoutSeconds`。
      /// </summary>
      private const float RebroadcastIntervalSeconds = 0.4f;

      /// <summary>上次成功解析到的地址（没有则为空）。</summary>
      public static string CachedBaseUrl => PlayerPrefs.GetString(CacheKey, string.Empty);

      /// <summary>
      /// 记住成功地址，下次启动直接复用。
      /// **只在清单校验通过后才写**：缓存是「上次真的能用」的地址，写脏了会让下次启动先白等 2s。
      /// 每次写后立刻 `PlayerPrefs.Save()`，因为 Android 上进程可能被系统随时回收，不落盘等于没写。
      /// </summary>
      public static void CacheBaseUrl(string baseUrl)
      {
         if (string.IsNullOrEmpty(baseUrl)) return;
         PlayerPrefs.SetString(CacheKey, baseUrl);
         PlayerPrefs.Save();
      }

      /// <summary>
      /// 清掉缓存（地址变了又自动发现失败时手工用）。
      /// 注意：清缓存不是必需的——缓存只是候选之一，校验不通过会被自动跳过；
      /// 这里保留是为了让「排查现场」能回到干净状态。
      /// </summary>
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
         // tried 只用于**失败时的可读诊断**（拼进最后一条 LogError 与 onFailed），不做状态判断。
         var tried = new List<string>();
         // resolved 由下面各阶段通过闭包写入；命中后立刻 break / 短路，保证「先便宜后贵」不被后面的阶段推翻。
         var resolved = (string)null;
         // url → 来源说明。用字典而不是元组列表，是因为 Validate 回调里要按下标取来源拼日志，
         // 且需要按 url 去重（同一地址被多个来源给出时只校验一次）。
         var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

         // ---- 阶段 1：静态候选（显式配置 → 上次成功地址），顺序即优先级 ----
         // 这一步只有 HTTP HEAD，成本最低，所以无条件先跑。
         foreach (var candidate in BuildCandidates(explicitBaseUrl, discoveryPort, sources))
         {
            var ok = false;
            // 注意：Validate 是协程，onResult 在它 yield 完之后才被调用，故先声明 ok 再 yield。
            yield return Validate(candidate, channel, platform, value => ok = value);
            tried.Add($"{candidate}（{sources[candidate]}）{(ok ? " 命中" : " 不通")}");

            if (ok)
            {
               resolved = candidate;
               break;
            }
         }

         // ---- 阶段 2：UDP 广播发现 ----
         // 只有在阶段 1 全灭、且配置允许时才做：广播要等秒级窗口，是整条链上最贵的一步。
         if (resolved == null && allowBroadcast)
         {
            yield return DiscoverByBroadcast(channel, platform, discoveryPort, timeoutSeconds,
               url => resolved = url, reason => tried.Add("UDP 广播：" + reason));
         }

         // ---- 阶段 3：本机回环兜底 ----
         // 回环地址只对编辑器/本机服务有意义，必须排在局域网自动发现之后。
         // 历史回归：早期它排在广播之前（候选列表里有 `127.0.0.1:port`），真机上该地址指向**手机自己**，
         // 既拿不到清单、又要白等一次 2s 超时，所以现在挪到广播之后、且在 BuildCandidates 里彻底移除。
         if (resolved == null && discoveryPort > 0)
         {
            var loopback = $"http://127.0.0.1:{discoveryPort}";
            var ok = false;
            yield return Validate(loopback, channel, platform, value => ok = value);
            tried.Add($"{loopback}（本机回环兜底）{(ok ? " 命中" : " 不通")}");
            if (ok) resolved = loopback;
         }

         // 命中后才写缓存：缓存永远指向「刚刚被清单验证过」的地址。
         if (resolved != null)
         {
            CacheBaseUrl(resolved);
            Debug.Log($"[HotUpdate] 资源服务器已定位：{resolved}");
            onResolved?.Invoke(resolved);
            yield break;
         }

         // 全灭：把每个候选的失败原因拼一条日志，真机上这条就是唯一的排查线索。
         var detail = tried.Count == 0 ? "没有可用的候选地址" : string.Join("；", tried.ToArray());
         Debug.LogError($"[HotUpdate] 无法定位内容服务器：{detail}。" +
                        "检查手机与 PC 是否同一网段（部分路由会隔离广播），或在 ConfigScene 的 AppBootstrapper 手填 ota.baseUrl。");
         onFailed?.Invoke(detail);
      }

      /// <summary>
      /// 按「先便宜后贵」的顺序拼候选地址（列表保证顺序，字典记录来源用于日志）。
      ///
      /// 规则：
      /// - 空 / 只含空白的地址直接跳过（未配置时 `ota.baseUrl` 就是空串）；
      /// - 统一去掉结尾 `/`，避免拼出 `http://host//lan/android/manifest.json`；
      /// - 用 `sources` 的键去重（`OrdinalIgnoreCase`，`http://HOST:8080` 与 `http://host:8080` 视为同一个），
      ///   同一地址只校验一次；
      /// - **不包含 `127.0.0.1`**：回环是最后的兜底阶段，放在 `ResolveAsync` 里，见那里的注释。
      /// </summary>
      private static List<string> BuildCandidates(string explicitBaseUrl, int discoveryPort,
         Dictionary<string, string> sources)
      {
         var list = new List<string>();

         // 局部函数：集中处理「空值跳过 + 去尾斜杠 + 去重 + 记录来源」，避免三处重复代码走样。
         void Add(string url, string source)
         {
            if (string.IsNullOrEmpty(url)) return;
            url = url.TrimEnd('/');
            if (sources.ContainsKey(url)) return;
            list.Add(url);
            sources[url] = source;
         }

         Add(explicitBaseUrl, "显式配置");

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
      ///
      /// **判定标准**：`Result.Success` 且 `responseCode == 200`。只认 200，是因为
      /// 404（路径拼错）、403（静态服务目录没开）、301/302（地址少/多了结尾斜杠）都不代表「有可用的清单」，
      /// 而 `Result.Success` 本身对 3xx 也可能为 true，所以必须两个条件都满足。
      /// **用 HEAD 而不是 GET**：只要响应头，不下清单体；服务端 `server.js` 对 HEAD 同样返回正确状态码。
      /// </summary>
      private static IEnumerator Validate(string baseUrl, string channel, string platform, Action<bool> onResult)
      {
         // 校验路径固定为 `/{channel}/{platform}/manifest.json`，与服务端 content 目录结构一一对应。
         var url = $"{baseUrl.TrimEnd('/')}/{channel}/{platform}/manifest.json";
         UnityWebRequest request = null;

         try
         {
            // 构造阶段就可能抛（HTTP 被策略禁止时），所以单独 try，不能和 SendWebRequest 合并。
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
            // timeout 以秒为单位、需整型；取上整保证不会因 2f→1s 而过于激进。
            request.timeout = Mathf.CeilToInt(ValidateTimeoutSeconds);

            UnityWebRequestAsyncOperation operation;
            try
            {
               // SendWebRequest 也可能抛（例如协议不支持），抛了就必须当作「该候选不通」，
               // 否则异常穿出协程会让整条发现流程静默中断。
               operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
               Debug.LogWarning($"[HotUpdate] 校验请求发送失败 {url}：{e.Message}");
               onResult?.Invoke(false);
               yield break;
            }

            // 只等这一个请求完成（含超时），期间不阻塞主线程。
            yield return operation;

            var ok = request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
            if (!ok)
            {
               // 这里用 Log 而非 LogWarning：候选不通是**预期内**的常态（多网卡、DHCP 漂移），
               // 只作为 trace 留痕，不应该在真机上刷黄色警告。
               Debug.Log($"[HotUpdate] 校验未通过 {url}：{request.result} {request.responseCode} {request.error}");
            }

            // using(request) 的 Dispose 在协程 yield 期间不会提前触发（编译器把它包进 try/finally），
            // 所以这里回调一次即可；onResult 必须**恰好调用一次**，调用方靠它推进状态。
            onResult?.Invoke(ok);
         }
      }

      /// <summary>
      /// UDP 广播探测：发 `THRUD-OTA-DISCOVER/1`，收服务端回来的 JSON（含 httpPort），再校验。
      ///
      /// **循环形态**（一轮 = 一次重发 + 把所有已到手回包读空）：
      /// - 时钟用 `Time.realtimeSinceStartup` 而不是 `Time.time`：不受 `Time.timeScale` 影响，
      ///   加载期即使 timeScale 为 0 也能正常走到超时；
      /// - 到点（`nextBroadcastAt`）才重发，避免每帧刷包把网络与日志打满；
      /// - 收包用 `socket.Available` 轮询 + `udp.Receive`，**不阻塞**主线程（UdpClient 的同步 Receive 会卡帧）；
      /// - 一旦有候选通过清单校验就 `break` 出双层循环，命中即止。
      ///
      /// **已知取舍**：若外部把本协程 `StopCoroutine` 掉，`UdpClient` 要等 GC 才释放（没有 try/finally 兜底）；
      /// 正常路径（超时/命中/初始化失败）都会走到末尾的 `udp.Close()`。
      /// </summary>
      private static IEnumerator DiscoverByBroadcast(string channel, string platform, int discoveryPort,
         float timeoutSeconds, Action<string> onResolved, Action<string> onFailed)
      {
         // 端口在这里再校验一次：广播端口同时用于「发往哪」和「回环兜底拼 URL」，非法值没有兜底意义。
         if (discoveryPort <= 0 || discoveryPort > 65535)
         {
            onFailed?.Invoke("未配置合法端口");
            yield break;
         }

         UdpClient udp = null;
         try
         {
            // 端口取系统分配的临时端口（不显式 Bind）：回包是**单播**到"发广播的那个端口"，
            // 所以不能绑服务端的 8080 之类端口，否则会和同机进程抢绑定。
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

         // 整个发现窗口不超过 timeoutSeconds（下限 0.5s，避免调用方传 0 导致一轮都跑不完）。
         var realtimeDeadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, timeoutSeconds);
         var nextBroadcastAt = 0f; // 0 表示"立刻发第一轮"
         var found = (string)null;
         var sawReply = false; // 用于区分"完全没收到回包"和"收到了但清单不可用"两种失败原因

         while (Time.realtimeSinceStartup < realtimeDeadline && found == null)
         {
            // ---- 到点重发 ----
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
                     // 单个目标失败（例如虚拟网卡的子网广播不可达）不影响其它目标，只记警告继续。
                     Debug.LogWarning($"[HotUpdate] 广播到 {target} 失败：{e.Message}");
                  }
               }

               nextBroadcastAt = Time.realtimeSinceStartup + RebroadcastIntervalSeconds;
            }

            // ---- 非阻塞收包：Available 为 0 就下一帧继续等 ----
            while (socket.Available > 0)
            {
               // remote 是回包源地址；服务端是多网卡机器时，它自报的 addresses 比源地址更可靠。
               IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
               string json;
               try
               {
                  json = Encoding.UTF8.GetString(udp.Receive(ref remote));
               }
               catch (Exception e)
               {
                  // 读失败（如 ICMP 端口不可达）不代表整个发现失败，跳出内层 while 继续重发。
                  Debug.LogWarning("[HotUpdate] 广播回包读取失败：" + e.Message);
                  break;
               }

               sawReply = true;
               var httpPort = ParseIntField(json, "httpPort");
               if (httpPort <= 0)
               {
                  // 端口非法说明不是我们的服务端（或版本不匹配），丢弃这枚包。
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

               // 源地址作为最后一位候选：服务端没报 addresses 时（老版本/极简实现）仍有机会命中。
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

            // 让出主线程：一轮最多跑一次（含若干帧的收包），保证 UI 不卡。
            yield return null;
         }

         // 无论命中与否都要关 socket：否则真机上这个临时端口会一直占着。
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
            // 区分两种失败：收到了回包但清单取不到（多半是服务端 content 目录没生成/路径不对），
            // 与彻底没有回包（多半是网段隔离 / 防火墙 / 没启动服务）。
            onFailed?.Invoke(sawReply ? "有应答但清单不可用" : $"{Mathf.RoundToInt(timeoutSeconds * 1000)}ms 内无应答");
         }
      }

      /// <summary>
      /// 广播目标：`255.255.255.255` + 每块在用网卡的子网广播地址。
      /// 部分路由会丢掉受限广播（255.255.255.255 常被 ACL 拦），给子网广播地址能明显提高命中率。
      ///
      /// 过滤规则：只取 `OperationalStatus.Up`、非 Loopback、IPv4（`InterNetwork`）且有掩码的网卡——
      /// IPv6 广播语义不同、Loopback 没有意义，混进来只会多出无用的发送目标。
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
                  if (mask == null) continue; // 部分平台（含老版 Android）拿不到掩码

                  var addressBytes = unicast.Address.GetAddressBytes();
                  var maskBytes = mask.GetAddressBytes();
                  if (addressBytes.Length != 4 || maskBytes.Length != 4) continue;

                  // 子网广播 = 主机位全置 1：broadcast[i] = ip[i] | (~mask[i])。
                  // 例：192.168.1.37 / 255.255.255.0 → 192.168.1.255。
                  var broadcast = new byte[4];
                  for (var i = 0; i < 4; i++)
                  {
                     broadcast[i] = (byte)(addressBytes[i] | (~maskBytes[i] & 0xFF));
                  }

                  var endpoint = new IPEndPoint(new IPAddress(broadcast), discoveryPort);
                  // 按地址去重：同一网段的多条地址（或虚拟网卡复用网段）会算出同一个广播地址。
                  if (!targets.Exists(t => t.Address.Equals(endpoint.Address)))
                  {
                     targets.Add(endpoint);
                  }
               }
            }
         }
         catch (Exception e)
         {
            // 枚举失败不是致命错误：仍然保留 255.255.255.255，很多网络下单这一条就够。
            Debug.LogWarning("[HotUpdate] 枚举网卡失败（只用 255.255.255.255）：" + e.Message);
         }

         return targets;
      }

      /// <summary>
      /// 从无依赖的极简 JSON 里抠一个字符串数组字段（形如 `"addresses":["1.2.3.4","5.6.7.8"]`）。
      /// 只做够用的解析，避免为一个地址列表引入 JSON 库。
      ///
      /// **能力边界（够用即可，不要当通用解析器用）**：不支持嵌套对象/数组、不支持转义字符、
      /// 不支持元素间的空对象；只按 `"key"` → 第一个 `[` → 第一个 `]` 取子串再按 `,` 切分。
      /// 之所以可以这么简单：这段 JSON 完全由 `web/LanOtaServer/server.js` 生成，格式可控。
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
            // 三层 Trim：先去掉元素两侧空白，再剥引号，再清掉剥引号后暴露出来的空白（`" 1.2.3.4 "`）。
            var value = part.Trim().Trim('"').Trim();
            if (value.Length > 0)
            {
               result.Add(value);
            }
         }

         return result;
      }

      /// <summary>
      /// 从无依赖的极简 JSON 里抠一个整数字段（避免为了一个端口引入解析器）。
      /// 解析到第一个非数字字符即停（兼容 `"httpPort":8080}` / `"httpPort": 8080 ` 两种写法）；
      /// 遇到数字之前的非空白、非引号字符直接判为失败，避免把 `"httpPort":"abc"` 之类读成 0。
      /// 找不到字段或没有数字时统一返回 -1。
      /// </summary>
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

            // 数字之后遇到的第一个非数字字符（通常是 `}` 或 `,`）即结束；这里不再校验它是否闭合，
            // 因为调用方只关心「端口是否 &gt; 0」，不做结构合法性断言。
            if (hasDigit) break;
            // 数字之前的空白与引号跳过（`"httpPort": 8080` / `"httpPort":"8080"` 都吃）。
            if (c == ' ' || c == '\t' || c == '"') continue;
            // 数字之前出现真实字符（如 `"httpPort":"abc"`）→ 视为无效字段。
            return -1;
         }

         return hasDigit ? value : -1;
      }
   }
}
