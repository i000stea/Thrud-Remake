// 局域网 OTA 静态内容服务 + 控制台（第一期后端，见 Docs/03_专项设计与重构/23 文档 §7）。
//
// 两块职责，边界都很清楚：
//   1. 内容下发（给真机 / 打包后的客户端）：/<channel>/<platform>/…，白名单 + 穿越拦截；
//   2. 控制台（给人看）：/ 前端页面 + /api/* 只读状态与日志流，用来在不开 Unity、不翻控制台
//      历史的情况下看清「服务在不在、内容齐不齐、刚才请求成没成」。
//
// 刻意不做：清单生成、Range 断点续传、鉴权、上传。也没有第三方依赖（纯 node:http）。
//
// 关键行为：内容根目录缺失**不再阻止启动**。服务照常起来、控制台照常可用，只是在页面上
// 标红「内容未就绪」，给出一键创建目录骨架的入口——避免双击 cmd 只闪一行报错就退出。
//
// 路由（channel / platform 默认 lan / android，大小写不敏感）：
//   GET  /                                     控制台页面
//   GET  /api/health                           状态快照
//   GET  /api/tree                             内容目录树（含文件大小/时间）
//   GET  /api/logs                             历史日志（环形缓冲）
//   GET  /api/stream                           SSE：log / request / status 实时推送
//   POST /api/root                             切换内容根目录（仅本机回环）
//   POST /api/root/skeleton                    按约定结构创建空目录（仅本机回环）
//   GET|HEAD /<channel>/<platform>/manifest.json
//   GET|HEAD /<channel>/<platform>/HotUpdate/<file>
//   GET|HEAD /<channel>/<platform>/AssetBundles/<file>
//   GET  /<channel>/<platform>/                纯文本目录列表页（无 JS 环境时的兜底）

const http = require("node:http");
const fs = require("node:fs");
const fsp = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");

const SERVER_DIR = __dirname;
const PUBLIC_DIR = path.join(SERVER_DIR, "public");

// 与 23 文档 §7 的第一期约定一致：局域网 + Android。
const DEFAULT_CHANNEL = "lan";
const DEFAULT_PLATFORM = "android";

// 平台目录下允许暴露的顶层条目（大小写不敏感）。
const ALLOWED_TOP_LEVEL = new Set(["manifest.json", "hotupdate", "assetbundles"]);

// 控制台数据结构与上限：宁可丢老日志，也不让内存随运行时间增长。
const MAX_LOGS = 500;
const MAX_REQUESTS = 200;
const MAX_BODY_BYTES = 64 * 1024;

const args = parseArgv(process.argv.slice(2));

const PORT = Number(args.port || process.env.LAN_OTA_PORT || 8080);
const HOST = String(args.host || process.env.LAN_OTA_HOST || "0.0.0.0");

// UDP 广播发现：客户端不必知道本机 IP，广播一句就拿到 httpPort。
// 与 HTTP 用同一个端口号不冲突（UDP 与 TCP 相互独立）。--discovery-port=0 关闭。
const DISCOVERY_PORT = Number(
  args["discovery-port"] || process.env.LAN_OTA_DISCOVERY_PORT || 8080,
);
const DISCOVERY_MAGIC = "THRUD-OTA-DISCOVER/1";
let ROOT = path.resolve(String(args.root || process.env.LAN_OTA_ROOT || path.join(SERVER_DIR, "content")));

// realpath 之后的 root，用于「请求路径必须落在 root 内」的最终校验（防符号链接逃逸）。
// null 表示当前 root 不可用（缺失 / 不是目录），此时内容路由返回 503 而不是 500。
let realRoot = null;

// 上一次已经报告过的 root 问题（"路径|原因"）。内容路由每个请求都会检查 root，
// 不记这一笔就会把同一条错误刷满日志。
let lastRootProblem = null;

// 控制台状态：环形缓冲 + SSE 订阅者。
const logs = [];
const requests = [];
const sseClients = new Set();
const startedAtMs = Date.now();

function parseArgv(argv) {
  const parsed = {};
  for (const raw of argv) {
    const match = /^--([a-zA-Z][\w-]*)(?:=(.*))?$/.exec(raw);
    if (!match) continue;
    parsed[match[1]] = match[2] === undefined ? true : match[2];
  }
  return parsed;
}

// ---------------------------------------------------------------- 日志与推流

function pushLog(level, message) {
  const entry = { t: new Date().toISOString(), level, message };
  logs.push(entry);
  if (logs.length > MAX_LOGS) logs.shift();

  const line = `[lan-ota] ${message}`;
  if (level === "error" || level === "warn") console.error(line);
  else console.log(line);

  broadcast("log", entry);
}

function log(message) {
  pushLog("info", message);
}

function warn(message) {
  pushLog("warn", message);
}

function fail(message) {
  pushLog("error", message);
}

function broadcast(event, data) {
  if (sseClients.size === 0) return;
  const payload = `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`;
  for (const client of sseClients) {
    try {
      client.write(payload);
    } catch {
      sseClients.delete(client);
    }
  }
}

async function broadcastStatus() {
  if (sseClients.size === 0) return;
  try {
    broadcast("status", await buildHealth());
  } catch {
    /* 状态推送失败不影响主流程 */
  }
}

// ---------------------------------------------------------------- 通用工具

function getContentType(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  if (ext === ".json") return "application/json; charset=utf-8";
  if (ext === ".html") return "text/html; charset=utf-8";
  if (ext === ".css") return "text/css; charset=utf-8";
  if (ext === ".js") return "text/javascript; charset=utf-8";
  // AB / DLL 一律按二进制流下发，客户端只按字节校验，不看 Content-Type。
  return "application/octet-stream";
}

function sendText(response, statusCode, text, headers = {}) {
  response.writeHead(statusCode, {
    "Content-Type": "text/plain; charset=utf-8",
    "Cache-Control": "no-store",
    ...headers,
  });
  response.end(text);
}

function sendHtml(response, statusCode, html) {
  response.writeHead(statusCode, {
    "Content-Type": "text/html; charset=utf-8",
    "Cache-Control": "no-store",
  });
  response.end(html);
}

function sendJson(response, statusCode, data) {
  response.writeHead(statusCode, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
  });
  response.end(JSON.stringify(data));
}

function escapeHtml(text) {
  return String(text)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}

/** 本机所有可用于真机访问的 IPv4 地址（排除回环）。 */
function listLanAddresses() {
  const result = [];
  const interfaces = os.networkInterfaces();
  for (const [name, list] of Object.entries(interfaces)) {
    for (const info of list || []) {
      if (info.family !== "IPv4" || info.internal) continue;
      result.push({ name, address: info.address });
    }
  }
  return result;
}

/**
 * UDP 广播发现应答：客户端广播 `THRUD-OTA-DISCOVER/1`，本服务回一段 JSON，
 * 客户端据此拼出 `http://<本机IP>:<PORT>` —— 真机就不用再手填 PC 的 IP 了。
 *
 * 只回给发起广播的那个地址，不带任何内容信息；内容版本由随后的 manifest.json 请求给出。
 * 端口绑定失败（被占用）不影响 HTTP 服务，只让「自动发现」失效。
 */
function startDiscoveryResponder() {
  if (!Number.isInteger(DISCOVERY_PORT) || DISCOVERY_PORT <= 0 || DISCOVERY_PORT > 65535) {
    log("UDP 发现已关闭（discovery-port 非法或为 0）。");
    return null;
  }

  const dgram = require("node:dgram");
  const socket = dgram.createSocket({ type: "udp4", reuseAddr: true });

  socket.on("message", (message, remote) => {
    if (message.toString("utf8").trim() !== DISCOVERY_MAGIC) return;

    const payload = JSON.stringify({
      service: "lan-ota",
      protocol: 1,
      httpPort: PORT,
      channel: DEFAULT_CHANNEL,
      platform: DEFAULT_PLATFORM,
      hostname: os.hostname(),
      // 多网卡机器（Hyper-V / VPN / 虚拟网卡）时，回包的源地址不一定是客户端能连的那个。
      // 把所有局域网 IPv4 都列出来，客户端逐个校验，谁通就用谁。
      addresses: listLanAddresses().map((item) => item.address),
    });

    socket.send(payload, remote.port, remote.address, (error) => {
      if (!error) log(`UDP 发现应答 -> ${remote.address}:${remote.port}`);
    });
  });

  socket.on("error", (error) => {
    fail(
      `UDP 发现端口 ${DISCOVERY_PORT} 不可用（${error.code || error.message}）：` +
        "真机将无法自动发现本机，请用 --discovery-port=<端口> 换一个。",
    );
    try {
      socket.close();
    } catch {
      /* 忽略 */
    }
  });

  socket.bind(DISCOVERY_PORT, "0.0.0.0", () => {
    try {
      socket.setBroadcast(true);
    } catch {
      /* 某些平台不允许，忽略 */
    }
    log(`UDP 发现已就绪：端口 ${DISCOVERY_PORT}（客户端广播 ${DISCOVERY_MAGIC}）`);
  });

  return socket;
}

/** 请求是否来自本机（只有本机允许改 root）。 */
function isLoopback(request) {
  const remote = request.socket.remoteAddress || "";
  return remote === "127.0.0.1" || remote === "::1" || remote === "::ffff:127.0.0.1";
}

async function readJsonBody(request) {
  const chunks = [];
  let size = 0;
  for await (const chunk of request) {
    size += chunk.length;
    if (size > MAX_BODY_BYTES) throw Object.assign(new Error("请求体过大"), { statusCode: 413 });
    chunks.push(chunk);
  }
  const raw = Buffer.concat(chunks).toString("utf8").trim();
  if (!raw) return {};
  try {
    return JSON.parse(raw);
  } catch {
    throw Object.assign(new Error("请求体不是合法 JSON"), { statusCode: 400 });
  }
}

/** 在目录下按名字找子目录，大小写不敏感（同时容忍 lan/LAN、android/Android）。 */
async function findChildDirectory(parentDir, name) {
  let entries;
  try {
    entries = await fsp.readdir(parentDir, { withFileTypes: true });
  } catch {
    return null;
  }
  const lowered = name.toLowerCase();
  const hit = entries.find((entry) => entry.isDirectory() && entry.name.toLowerCase() === lowered);
  return hit ? path.join(parentDir, hit.name) : null;
}

// ---------------------------------------------------------------- 内容根目录

/** 解析并缓存 realRoot；不抛异常，返回状态对象供控制台显示。 */
async function refreshRoot() {
  try {
    const stat = await fsp.stat(ROOT);
    if (!stat.isDirectory()) {
      realRoot = null;
      return { ok: false, reason: "路径存在但不是目录" };
    }
    realRoot = await fsp.realpath(ROOT);
    lastRootProblem = null;
    return { ok: true, reason: "" };
  } catch (error) {
    realRoot = null;
    return { ok: false, reason: error.code === "ENOENT" ? "目录不存在" : `无法访问（${error.code || error.message}）` };
  }
}

/** 按 23 文档 §7 的约定补出空目录骨架（只建目录，不放任何内容）。 */
async function createSkeleton(baseDir) {
  const created = [];
  const targets = [
    path.join(baseDir, DEFAULT_CHANNEL, DEFAULT_PLATFORM),
    path.join(baseDir, DEFAULT_CHANNEL, DEFAULT_PLATFORM, "HotUpdate"),
    path.join(baseDir, DEFAULT_CHANNEL, DEFAULT_PLATFORM, "AssetBundles"),
  ];
  for (const target of targets) {
    await fsp.mkdir(target, { recursive: true });
    created.push(target);
  }
  return created;
}

/** 期望结构说明（控制台与日志共用一份，避免两处文案漂移）。 */
function expectedLayout(baseDir) {
  return [
    path.join(baseDir, DEFAULT_CHANNEL, DEFAULT_PLATFORM, "manifest.json"),
    path.join(baseDir, DEFAULT_CHANNEL, DEFAULT_PLATFORM, "HotUpdate", "*.dll | hotfix.json"),
    path.join(baseDir, DEFAULT_CHANNEL, DEFAULT_PLATFORM, "AssetBundles", "*"),
  ];
}

// ---------------------------------------------------------------- 内容路由

/**
 * 把请求 URL 拆成 channel / platform / 平台内剩余片段（全部已解码）。
 * 路径形如 `/<channel>/<platform>/[...]`，与 23 文档 §3 的
 * `http://局域网地址/{channel}/{platform}/{file}` 及 §7 的 `/lan/android/...` 一致。
 */
function parseContentPath(urlPath) {
  const segments = urlPath.split("/").filter((segment) => segment.length > 0);
  if (segments.length === 0) {
    return null;
  }
  return {
    channel: segments[0],
    platform: segments.length > 1 ? segments[1] : null,
    inner: segments.slice(2),
  };
}

/**
 * 解析受支持的响应：返回 { kind, absolutePath } 或 { error: { status, message } }。
 * kind: "manifest" | "listing" | "file"
 */
async function resolveTarget(parsed) {
  const channelDir = await findChildDirectory(ROOT, parsed.channel);
  if (!channelDir) {
    return { error: { status: 404, message: `channel 目录不存在：${parsed.channel}` } };
  }

  // /<channel>/ → 该 channel 下的平台列表
  if (parsed.platform === null) {
    return { kind: "listing", dir: channelDir, level: "channel" };
  }

  const platformDir = await findChildDirectory(channelDir, parsed.platform);
  if (!platformDir) {
    return { error: { status: 404, message: `platform 目录不存在：${parsed.platform}` } };
  }

  // /<channel>/<platform>/ → 平台目录列表（无 JS 环境时的兜底）
  if (parsed.inner.length === 0) {
    return { kind: "listing", dir: platformDir, level: "platform" };
  }

  const top = parsed.inner[0];
  if (!ALLOWED_TOP_LEVEL.has(top.toLowerCase())) {
    return {
      error: {
        status: 404,
        message: `未开放的平台内路径：${top}（只放行 manifest.json / HotUpdate/** / AssetBundles/**）`,
      },
    };
  }

  const target = path.join(platformDir, ...parsed.inner);
  return { kind: "file", absolutePath: target, isManifest: top.toLowerCase() === "manifest.json" };
}

/** 校验最终路径确实落在 root 内（防 ../ 与符号链接逃逸）。 */
async function assertInsideRoot(absolutePath) {
  if (!realRoot) return false;
  let real;
  try {
    real = await fsp.realpath(absolutePath);
  } catch {
    return false;
  }
  const withSep = realRoot.endsWith(path.sep) ? realRoot : realRoot + path.sep;
  return real === realRoot || real.startsWith(withSep);
}

async function serveListing(parsed, response) {
  const target = await resolveTarget(parsed);
  if (target.error) {
    return sendText(response, target.error.status, target.error.message);
  }

  const level = target.level;
  const rows = [];
  const dirs = [];
  const files = [];

  let entries = [];
  try {
    entries = await fsp.readdir(target.dir, { withFileTypes: true });
  } catch (error) {
    return sendText(response, 500, `读取目录失败：${error.message}`);
  }

  for (const entry of entries.sort((a, b) => a.name.localeCompare(b.name))) {
    const full = path.join(target.dir, entry.name);
    if (entry.isDirectory()) {
      let count = 0;
      try {
        count = (await fsp.readdir(full, { withFileTypes: true })).filter((e) => e.isFile()).length;
      } catch {
        count = 0;
      }
      dirs.push({ name: entry.name, count });
      continue;
    }
    if (!entry.isFile()) continue;
    let size = 0;
    let mtime = null;
    try {
      const stat = await fsp.stat(full);
      size = stat.size;
      mtime = stat.mtime;
    } catch {
      size = 0;
    }
    files.push({ name: entry.name, size, mtime });
  }

  const baseUrl = `/${encodeURIComponent(parsed.channel)}`;
  for (const dir of dirs) {
    // channel 层的子目录是平台，平台层的子目录是 HotUpdate / AssetBundles，链接规则相同。
    rows.push(
      `<tr><td><a href="${escapeHtml(`${baseUrl}/${encodeURIComponent(dir.name)}/`)}">${escapeHtml(dir.name)}/</a></td>` +
        `<td>—</td><td>${dir.count} 个文件</td></tr>`,
    );
  }
  // channel 层下的散落文件不在受支持路由里，只列名字不给链接。
  const fileBase = level === "channel" ? null : `${baseUrl}/${encodeURIComponent(parsed.platform)}`;
  for (const file of files) {
    const name = escapeHtml(file.name);
    const label = fileBase ? `<a href="${escapeHtml(`${fileBase}/${encodeURIComponent(file.name)}`)}">${name}</a>` : name;
    rows.push(
      `<tr><td>${label}</td>` +
        `<td>${formatSize(file.size)}</td>` +
        `<td>${file.mtime ? file.mtime.toISOString().replace("T", " ").slice(0, 19) : "—"}</td></tr>`,
    );
  }

  const title = level === "channel" ? `channel: ${parsed.channel}` : `平台目录：${path.basename(target.dir)}`;
  const html = `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8">
<title>LAN OTA · ${escapeHtml(title)}</title>
<style>
  body{font-family:Segoe UI,Microsoft YaHei,sans-serif;margin:32px;color:#1f2328;background:#f6f8fa}
  h1{font-size:18px;margin:0 0 4px} .meta{color:#59636e;font-size:12px;margin-bottom:20px}
  table{border-collapse:collapse;background:#fff;box-shadow:0 1px 2px rgba(0,0,0,.08);min-width:560px}
  th,td{padding:7px 12px;border-bottom:1px solid #eaeef2;font-size:13px;text-align:left}
  th{background:#f0f3f6;color:#59636e;font-weight:600}
  a{color:#0969da;text-decoration:none} a:hover{text-decoration:underline}
  code{background:#eef1f4;padding:1px 5px;border-radius:4px}
</style></head><body>
<h1>局域网 OTA 内容目录</h1>
<div class="meta">root = <code>${escapeHtml(ROOT)}</code> · 节点 <code>${escapeHtml(title)}</code> · <a href="/">返回控制台</a></div>
<table><thead><tr><th>条目</th><th>大小</th><th>说明 / 修改时间</th></tr></thead><tbody>
${rows.join("\n") || '<tr><td colspan="3">（空目录）</td></tr>'}
</tbody></table>
</body></html>`;
  return sendHtml(response, 200, html);
}

async function serveFile(parsed, request, response) {
  const target = await resolveTarget(parsed);
  if (target.error) {
    return sendText(response, target.error.status, target.error.message);
  }

  // 先 stat：不存在的文件必须是 404，不能因为 realpath 失败被误判成 403。
  let stat;
  try {
    stat = await fsp.stat(target.absolutePath);
  } catch {
    return sendText(response, 404, `文件不存在：${path.basename(target.absolutePath)}`);
  }

  if (stat.isDirectory()) {
    return sendText(response, 404, "目标是目录，请访问其父级列表页");
  }

  // 再核对最终真实路径：命中的若是指向 root 之外的符号链接，这里拦下。
  const inside = await assertInsideRoot(target.absolutePath);
  if (!inside) {
    warn(`拒绝越界访问：${target.absolutePath}`);
    return sendText(response, 403, "拒绝访问：路径超出内容根目录");
  }

  response.writeHead(200, {
    "Content-Type": getContentType(target.absolutePath),
    "Content-Length": stat.size,
    // 开发期一律不缓存：清单/包体被替换后，真机必须立刻拿到新字节。
    "Cache-Control": "no-store, must-revalidate",
    "Last-Modified": stat.mtime.toUTCString(),
  });

  if (request.method === "HEAD") {
    return response.end();
  }

  const stream = fs.createReadStream(target.absolutePath);
  stream.on("error", (error) => {
    warn(`读取失败 ${target.absolutePath}：${error.message}`);
    response.destroy(error);
  });
  stream.pipe(response);
}

// ---------------------------------------------------------------- 控制台接口

async function buildHealth() {
  const rootState = await refreshRoot();
  const channels = [];
  let totalFiles = 0;

  let rootEntries = [];
  if (rootState.ok) {
    try {
      rootEntries = await fsp.readdir(ROOT, { withFileTypes: true });
    } catch {
      rootEntries = [];
    }
  }

  for (const channelEntry of rootEntries.filter((e) => e.isDirectory())) {
    const channelDir = path.join(ROOT, channelEntry.name);
    let platformEntries = [];
    try {
      platformEntries = await fsp.readdir(channelDir, { withFileTypes: true });
    } catch {
      platformEntries = [];
    }
    const platforms = [];
    for (const platformEntry of platformEntries.filter((e) => e.isDirectory())) {
      const platformDir = path.join(channelDir, platformEntry.name);
      const manifestPath = path.join(platformDir, "manifest.json");
      let manifest = null;
      try {
        const stat = await fsp.stat(manifestPath);
        let contentVersion = null;
        let fileEntries = null;
        try {
          const parsed = JSON.parse(await fsp.readFile(manifestPath, "utf8"));
          contentVersion = parsed.contentVersion ?? null;
          if (Array.isArray(parsed.files)) {
            fileEntries = parsed.files.map((file) => ({
              id: file?.id ?? null,
              kind: file?.kind ?? null,
              path: file?.path ?? null,
              size: file?.size ?? null,
            }));
          }
        } catch {
          contentVersion = "(解析失败)";
        }
        manifest = { size: stat.size, mtime: stat.mtime, contentVersion, files: fileEntries };
      } catch {
        manifest = null;
      }

      let fileCount = 0;
      for (const tree of ["HotUpdate", "AssetBundles"]) {
        try {
          fileCount += (
            await fsp.readdir(path.join(platformDir, tree), { withFileTypes: true, recursive: true })
          ).filter((e) => e.isFile()).length;
        } catch {
          /* 目录不存在，记 0 */
        }
      }
      totalFiles += fileCount;
      platforms.push({ platform: platformEntry.name, manifest, servedFiles: fileCount });
    }
    channels.push({ channel: channelEntry.name, platforms });
  }

  return {
    ok: true,
    service: "lan-ota",
    pid: process.pid,
    startedAt: new Date(startedAtMs).toISOString(),
    uptime: Number(((Date.now() - startedAtMs) / 1000).toFixed(1)),
    time: new Date().toISOString(),
    root: ROOT,
    rootState,
    expectedLayout: expectedLayout(ROOT),
    port: PORT,
    host: HOST,
    addresses: listLanAddresses().map((item) => ({
      ...item,
      manifestUrl: `http://${item.address}:${PORT}/${DEFAULT_CHANNEL}/${DEFAULT_PLATFORM}/manifest.json`,
    })),
    channels,
    servedFiles: totalFiles,
    sseClients: sseClients.size,
    logCount: logs.length,
    requestCount: requests.length,
  };
}

/** 内容树：channel → platform → manifest 摘要 + HotUpdate / AssetBundles 文件。 */
async function buildTree() {
  const rootState = await refreshRoot();
  if (!rootState.ok) {
    return { ok: false, rootState, root: ROOT, channels: [] };
  }

  const channels = [];
  let rootEntries = [];
  try {
    rootEntries = await fsp.readdir(ROOT, { withFileTypes: true });
  } catch {
    rootEntries = [];
  }

  async function listFiles(dir, depth) {
    const out = [];
    if (depth > 3) return out;
    let entries = [];
    try {
      entries = await fsp.readdir(dir, { withFileTypes: true });
    } catch {
      return out;
    }
    for (const entry of entries.sort((a, b) => a.name.localeCompare(b.name))) {
      if (entry.name.endsWith(".meta")) continue;
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        out.push({ name: `${entry.name}/`, directory: true });
        out.push(...(await listFiles(full, depth + 1)).map((item) => ({ ...item, name: `${entry.name}/${item.name}` })));
        continue;
      }
      if (!entry.isFile()) continue;
      let size = 0;
      let mtime = null;
      try {
        const stat = await fsp.stat(full);
        size = stat.size;
        mtime = stat.mtime.toISOString();
      } catch {
        /* 读取失败的条目仍然列出，只是没有大小 */
      }
      out.push({ name: entry.name, size, mtime, directory: false });
    }
    return out;
  }

  for (const channelEntry of rootEntries.filter((e) => e.isDirectory())) {
    const channelDir = path.join(ROOT, channelEntry.name);
    const platforms = [];
    let platformEntries = [];
    try {
      platformEntries = await fsp.readdir(channelDir, { withFileTypes: true });
    } catch {
      platformEntries = [];
    }
    for (const platformEntry of platformEntries.filter((e) => e.isDirectory())) {
      const platformDir = path.join(channelDir, platformEntry.name);
      const trees = {};
      for (const tree of ["HotUpdate", "AssetBundles"]) {
        trees[tree] = await listFiles(path.join(platformDir, tree), 0);
      }
      let manifest = null;
      try {
        const raw = await fsp.readFile(path.join(platformDir, "manifest.json"), "utf8");
        const stat = await fsp.stat(path.join(platformDir, "manifest.json"));
        let parsed = null;
        let parseError = null;
        try {
          parsed = JSON.parse(raw);
        } catch (error) {
          parseError = error.message;
        }
        manifest = {
          exists: true,
          size: stat.size,
          mtime: stat.mtime.toISOString(),
          parseError,
          schemaVersion: parsed?.schemaVersion ?? null,
          contentVersion: parsed?.contentVersion ?? null,
          channel: parsed?.channel ?? null,
          platform: parsed?.platform ?? null,
          files: Array.isArray(parsed?.files) ? parsed.files : [],
        };
      } catch {
        manifest = { exists: false };
      }
      platforms.push({ platform: platformEntry.name, manifest, trees });
    }
    channels.push({ channel: channelEntry.name, platforms });
  }

  return { ok: true, rootState, root: ROOT, channels };
}

/** 处理 /api/* 的写操作（仅本机回环）。 */
async function handleApiWrite(request, response, pathname) {
  if (!isLoopback(request)) {
    warn(`拒绝来自非本机的写操作：${pathname}（${request.socket.remoteAddress || "?"}）`);
    return sendJson(response, 403, { ok: false, message: "写操作只允许在本机（127.0.0.1）发起" });
  }

  const body = await readJsonBody(request);

  if (pathname === "/api/root") {
    const nextRoot = String(body.root || "").trim();
    if (!nextRoot) return sendJson(response, 400, { ok: false, message: "root 不能为空" });

    const resolved = path.resolve(nextRoot);
    let stat = null;
    try {
      stat = await fsp.stat(resolved);
    } catch {
      stat = null;
    }

    if (!stat) {
      if (body.createSkeleton) {
        const created = await createSkeleton(resolved);
        log(`已创建目录骨架：${resolved}（${created.length} 个目录）`);
      } else {
        return sendJson(response, 400, {
          ok: false,
          message: `目录不存在：${resolved}（可勾选「同时创建空目录骨架」）`,
        });
      }
    } else if (!stat.isDirectory()) {
      return sendJson(response, 400, { ok: false, message: `不是目录：${resolved}` });
    }

    ROOT = resolved;
    const state = await refreshRoot();
    log(`内容根目录切换为：${ROOT}（${state.ok ? "就绪" : state.reason}）`);
    await broadcastStatus();
    return sendJson(response, 200, { ok: true, root: ROOT, rootState: state });
  }

  if (pathname === "/api/root/skeleton") {
    const target = body.root ? path.resolve(String(body.root)) : ROOT;
    const created = await createSkeleton(target);
    ROOT = target;
    const state = await refreshRoot();
    log(`已按约定结构创建空目录：${target}（${created.length} 个目录）`);
    await broadcastStatus();
    return sendJson(response, 200, { ok: true, root: ROOT, rootState: state, created });
  }

  return sendJson(response, 404, { ok: false, message: "接口不存在" });
}

function serveDashboard(response, pathname) {
  const fileMap = {
    "/": "index.html",
    "/index.html": "index.html",
    "/app.js": "app.js",
    "/styles.css": "styles.css",
  };
  const fileName = fileMap[pathname];
  if (!fileName) return false;

  const full = path.join(PUBLIC_DIR, fileName);
  return fsp
    .readFile(full)
    .then((content) => {
      response.writeHead(200, {
        "Content-Type": getContentType(full),
        "Cache-Control": "no-store",
      });
      response.end(content);
      return true;
    })
    .catch(() => {
      sendText(response, 500, `控制台静态文件缺失：${full}（请确认 public/ 目录完整）`);
      return true;
    });
}

// ---------------------------------------------------------------- 请求处理

/** UDP 发现应答套接字（关闭时用；绑定失败保持 null）。 */
let discoverySocket = null;

const server = http.createServer(async (request, response) => {
  const startedAt = process.hrtime.bigint();
  let counted = false;

  response.on("finish", () => {
    if (counted || request.url === "/favicon.ico") return;
    counted = true;

    const ms = Number(process.hrtime.bigint() - startedAt) / 1e6;
    const entry = {
      t: new Date().toISOString(),
      method: request.method,
      path: request.url,
      status: response.statusCode,
      ms: Number(ms.toFixed(1)),
      bytes: Number(response.getHeader("content-length") || 0),
    };

    // 控制台自己的轮询/日志流不刷进请求表，否则真实请求会被淹没。
    const noisy = entry.path.startsWith("/api/") || entry.path === "/" || entry.path.startsWith("/app.js") || entry.path.startsWith("/styles.css");
    requests.push({ ...entry, noisy: Boolean(noisy) });
    if (requests.length > MAX_REQUESTS) requests.shift();
    broadcast("request", { ...entry, noisy: Boolean(noisy) });

    if (!entry.path.startsWith("/api/")) {
      log(`${entry.status} ${entry.method} ${entry.path} ${entry.ms}ms ${entry.bytes ? formatSize(entry.bytes) : ""}`.trim());
    }
  });

  try {
    if (request.method !== "GET" && request.method !== "HEAD" && request.method !== "POST") {
      return sendText(response, 405, "只支持 GET / HEAD / POST", { Allow: "GET, HEAD, POST" });
    }

    // 原始 URL 先过一遍：WHATWG `new URL()` 会把 `..`、`%2e%2e`、反斜杠当作
    // 点段规范化掉（`/lan/android/../../server.js` → `/server.js`），规范化之后就
    // 看不出这是一次穿越尝试了。所以必须在解析前按原始字符串显式拒绝。
    const rawUrl = request.url || "";
    const looksLikeTraversal =
      /(^|\/)\.\.(\/|\\|$)/.test(rawUrl) ||
      /%2e%2e|\.\.%2f|%2f%2e|%2e%2f|%2e%5c/i.test(rawUrl) ||
      rawUrl.includes("\\") ||
      rawUrl.includes("%5c") ||
      rawUrl.includes("%00");
    if (looksLikeTraversal) {
      warn(`拒绝可疑路径（原始 URL）：${rawUrl}`);
      return sendText(response, 403, "拒绝访问：非法路径");
    }

    const hostHeader = request.headers.host || `${HOST}:${PORT}`;
    let url;
    try {
      url = new URL(rawUrl, `http://${hostHeader}`);
    } catch {
      return sendText(response, 400, "非法 URL");
    }

    if (url.pathname === "/favicon.ico") {
      return sendText(response, 404, "");
    }

    // ---- 控制台接口
    if (url.pathname === "/api/health" && request.method === "GET") {
      return sendJson(response, 200, await buildHealth());
    }

    if (url.pathname === "/api/tree" && request.method === "GET") {
      return sendJson(response, 200, await buildTree());
    }

    if (url.pathname === "/api/logs" && request.method === "GET") {
      return sendJson(response, 200, { logs, requests });
    }

    if (url.pathname === "/api/stream" && request.method === "GET") {
      response.writeHead(200, {
        "Content-Type": "text/event-stream; charset=utf-8",
        "Cache-Control": "no-store",
        Connection: "keep-alive",
        "X-Accel-Buffering": "no",
      });
      // 首帧带全量快照，刷新页面不用等下一次变化。
      response.write(`retry: 3000\n\n`);
      response.write(`event: snapshot\ndata: ${JSON.stringify({ logs, requests, health: await buildHealth() })}\n\n`);
      sseClients.add(response);
      broadcastStatus();

      const heartbeat = setInterval(() => {
        try {
          response.write(`: ping\n\n`);
        } catch {
          clearInterval(heartbeat);
        }
      }, 15000);

      request.on("close", () => {
        clearInterval(heartbeat);
        sseClients.delete(response);
      });
      return;
    }

    if (url.pathname.startsWith("/api/")) {
      if (request.method === "POST") {
        return await handleApiWrite(request, response, url.pathname);
      }
      return sendJson(response, 404, { ok: false, message: "接口不存在" });
    }

    // ---- 控制台页面与静态资源
    if (["/", "/index.html", "/app.js", "/styles.css"].includes(url.pathname)) {
      if (args["no-index"]) return sendText(response, 404, "控制台已关闭（--no-index）");
      await serveDashboard(response, url.pathname);
      return;
    }

    // ---- 内容路由
    if (!realRoot) {
      const state = await refreshRoot();
      const problem = `${ROOT}|${state.reason}`;
      if (lastRootProblem !== problem) {
        lastRootProblem = problem;
        fail(`内容根目录不可用（${state.reason}）：${ROOT}  → 内容路由返回 503`);
      }
      return sendText(
        response,
        503,
        `内容未就绪：${ROOT} ${state.reason}\n` +
          `期望结构：\n${expectedLayout(ROOT)
            .map((line) => `  ${line}`)
            .join("\n")}\n` +
          `处理：打开控制台 http://127.0.0.1:${PORT}/ 一键创建目录骨架，或用 --root=<目录> 指定。`,
      );
    }

    // 再解码一次做纵深防御（规范化之后仍可能出现 `%2f` 之类）。
    let decoded;
    try {
      decoded = decodeURIComponent(url.pathname);
    } catch {
      return sendText(response, 400, "URL 含非法百分号编码");
    }

    if (decoded.includes("\0") || decoded.includes("\\") || decoded.split("/").some((s) => s === ".." || s === ".")) {
      warn(`拒绝可疑路径：${decoded}`);
      return sendText(response, 403, "拒绝访问：非法路径");
    }

    const parsed = parseContentPath(decoded);
    if (!parsed) {
      return sendText(response, 404, `未找到：${decoded}（支持 /、/api/health、/<channel>/<platform>/…）`);
    }

    if (request.method === "POST") {
      return sendText(response, 405, "内容路由只支持 GET / HEAD", { Allow: "GET, HEAD" });
    }

    // 结尾带 / 或只给到 channel 层，都走列表页；否则按文件下发。
    if (decoded.endsWith("/") || parsed.platform === null) {
      return serveListing(parsed, response);
    }

    return await serveFile(parsed, request, response);
  } catch (error) {
    const status = error.statusCode || 500;
    fail(`请求处理异常：${error.stack || error.message}`);
    return sendText(response, status, status === 500 ? "服务器内部错误" : error.message);
  }
});

// ---------------------------------------------------------------- 启动

function openBrowser(url) {
  // 与 web/ProgressChaser 相同的做法：由 Node 拉一次浏览器，双击 cmd 即可看到控制台。
  const { spawn } = require("node:child_process");
  const command =
    process.platform === "win32" ? ["cmd", ["/c", "start", "", url]] :
    process.platform === "darwin" ? ["open", [url]] :
    ["xdg-open", [url]];

  try {
    const child = spawn(command[0], command[1], { detached: true, stdio: "ignore", windowsHide: true });
    // spawn 失败是异步的 'error' 事件；不接住会直接把进程带崩。
    child.on("error", () => {});
    child.unref();
  } catch {
    /* 打不开浏览器不影响服务；控制台地址已在日志里打印 */
  }
}

async function start() {
  if (!Number.isInteger(PORT) || PORT < 1 || PORT > 65535) {
    console.error(`[lan-ota] 端口非法：${PORT}`);
    process.exit(1);
  }

  const rootState = await refreshRoot();

  server.listen(PORT, HOST, async () => {
    const url = `http://127.0.0.1:${PORT}/`;
    log(`服务已启动：${url}`);
    log(`内容根目录：${ROOT}`);

    if (rootState.ok) {
      log(`内容目录就绪。`);
    } else {
      // 注意：这里**不退出**。没有内容也能先把控制台打开，把问题说清楚。
      fail(`内容根目录不可用（${rootState.reason}）：${ROOT}`);
      fail("期望结构：");
      for (const line of expectedLayout(ROOT)) fail(`  ${line}`);
      fail(`处理：打开控制台 ${url} 点「创建空目录骨架」，或用 --root=<目录> 指定。`);
    }

    const addresses = listLanAddresses();
    if (addresses.length === 0) {
      warn("未检测到局域网 IPv4 地址，真机可能无法访问。");
    } else {
      for (const item of addresses) {
        log(`真机可用：http://${item.address}:${PORT}/${DEFAULT_CHANNEL}/${DEFAULT_PLATFORM}/  (${item.name})`);
      }
    }

    log("控制台已就绪；关闭窗口或按 Ctrl+C 停止服务。");
    if (!args["no-open"] && !args["no-launch"]) openBrowser(url);

    discoverySocket = startDiscoveryResponder();
  });
}

server.on("error", (error) => {
  if (error.code === "EADDRINUSE") {
    console.error(`[lan-ota] 端口 ${PORT} 已被占用：换端口用 --port=<端口> 或设 LAN_OTA_PORT。`);
  } else if (error.code === "EACCES") {
    console.error(`[lan-ota] 无权限绑定 ${HOST}:${PORT}：换端口或改用 127.0.0.1。`);
  } else {
    console.error("[lan-ota]", error.message);
  }
  process.exit(1);
});

process.on("SIGINT", () => {
  console.log("\n[lan-ota] 正在停止服务…");
  for (const client of sseClients) {
    try {
      client.end();
    } catch {
      /* ignore */
    }
  }
  server.close(() => process.exit(0));
});

process.on("SIGTERM", () => {
  server.close(() => process.exit(0));
});

start().catch((error) => {
  console.error("[lan-ota] 启动失败：", error);
  process.exit(1);
});
