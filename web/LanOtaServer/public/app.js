// 局域网 OTA 控制台前端：零依赖，SSE 收实时日志/请求/状态，HTTP 轮询兜底。
"use strict";

const MAX_LOG_LINES = 500;
const MAX_REQ_ROWS = 200;

const el = (id) => document.getElementById(id);

const ui = {
  conn: el("conn"),
  uptime: el("uptime"),
  portPill: el("portPill"),
  readyBadge: el("readyBadge"),
  rootPath: el("rootPath"),
  contentVersion: el("contentVersion"),
  servedFiles: el("servedFiles"),
  channels: el("channels"),
  rootProblem: el("rootProblem"),
  rootProblemText: el("rootProblemText"),
  layout: el("layout"),
  rootInput: el("rootInput"),
  btnApplyRoot: el("btnApplyRoot"),
  btnSkeleton: el("btnSkeleton"),
  createWithRoot: el("createWithRoot"),
  rootResult: el("rootResult"),
  addresses: el("addresses"),
  tree: el("tree"),
  btnRefreshTree: el("btnRefreshTree"),
  logs: el("logs"),
  logLevel: el("logLevel"),
  autoScroll: el("autoScroll"),
  btnClearLogs: el("btnClearLogs"),
  requests: el("requests"),
  showNoisy: el("showNoisy"),
  clientStats: el("clientStats"),
};

let health = null;
const logLines = [];
const reqRows = [];

// ------------------------------------------------------------------ 工具

function fmtTime(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toTimeString().slice(0, 8);
}

function fmtSize(bytes) {
  if (!bytes && bytes !== 0) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}

function fmtUptime(seconds) {
  const s = Math.max(0, Math.floor(seconds || 0));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const rest = s % 60;
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m}m ${rest}s`;
  return `${rest}s`;
}

function statusClass(status) {
  if (!status) return "";
  if (status < 300) return "s-2xx";
  if (status < 400) return "s-3xx";
  if (status < 500) return "s-4xx";
  return "s-5xx";
}

async function copyText(text) {
  try {
    await navigator.clipboard.writeText(text);
    ui.clientStats.textContent = "已复制到剪贴板";
  } catch {
    ui.clientStats.textContent = "复制失败，请手动选择文本";
  }
  setTimeout(() => (ui.clientStats.textContent = ""), 2500);
}

function commandFor(input) {
  return `node server.js --root="${input}"`;
}

// ------------------------------------------------------------------ 状态渲染

function renderHealth(next) {
  health = next;

  ui.portPill.textContent = `:${next.port}`;
  ui.uptime.textContent = `运行 ${fmtUptime(next.uptime)}`;
  ui.rootPath.textContent = next.root || "—";

  const ready = Boolean(next.rootState && next.rootState.ok);

  const platformList = [];
  for (const ch of next.channels || []) {
    for (const pf of ch.platforms || []) {
      platformList.push(`${ch.channel}/${pf.platform}`);
    }
  }

  ui.channels.textContent = platformList.length ? platformList.join("、") : "（未发现 channel/platform）";
  ui.servedFiles.textContent = `${next.servedFiles || 0} 个（HotUpdate + AssetBundles）`;

  const version = (next.channels || [])
    .flatMap((ch) => ch.platforms || [])
    .map((pf) => pf.manifest && pf.manifest.contentVersion)
    .find((v) => v);
  ui.contentVersion.textContent = version || (ready ? "（无 manifest.json）" : "—");

  ui.readyBadge.className = `badge ${ready ? "badge-ok" : "badge-error"}`;
  ui.readyBadge.textContent = ready ? "就绪" : "未就绪";

  ui.rootProblem.hidden = ready;
  if (!ready) {
    ui.rootProblemText.textContent = `${next.root} —— ${(next.rootState && next.rootState.reason) || "不可用"}`;
  }

  ui.layout.textContent = (next.expectedLayout || []).join("\n");

  if (!ui.rootInput.value && next.root) ui.rootInput.placeholder = next.root;

  // 真机地址
  const addresses = next.addresses || [];
  ui.addresses.innerHTML = "";
  if (addresses.length === 0) {
    const li = document.createElement("li");
    li.className = "hint";
    li.textContent = "未检测到局域网 IPv4 地址：真机可能无法访问，请检查网络适配器。";
    ui.addresses.appendChild(li);
  }
  for (const addr of addresses) {
    const li = document.createElement("li");
    const nic = document.createElement("span");
    nic.className = "nic";
    nic.textContent = addr.name;

    const link = document.createElement("a");
    link.href = addr.manifestUrl;
    link.target = "_blank";
    link.rel = "noreferrer";
    link.textContent = addr.manifestUrl;

    const btn = document.createElement("button");
    btn.className = "btn btn-ghost";
    btn.textContent = "复制";
    btn.addEventListener("click", () => copyText(addr.manifestUrl));

    li.append(nic, link, btn);
    ui.addresses.appendChild(li);
  }

  renderConnState();
}

function renderConnState() {
  const pill = ui.conn;
  if (!pill) return;
  if (ui._sseOpen) {
    pill.className = "pill pill-ok";
    pill.textContent = "实时连接";
  } else {
    pill.className = "pill pill-warn";
    pill.textContent = "轮询兜底";
  }
}

// ------------------------------------------------------------------ 日志

function levelPass(level) {
  const filter = ui.logLevel.value;
  if (filter === "all") return true;
  if (filter === "error") return level === "error";
  if (filter === "warn") return level === "warn" || level === "error";
  return true;
}

function renderLogs() {
  const frag = document.createDocumentFragment();
  const visible = logLines.filter((line) => levelPass(line.level));
  for (const line of visible) {
    const row = document.createElement("div");
    row.className = `line ${line.level}`;
    const t = document.createElement("span");
    t.className = "t";
    t.textContent = fmtTime(line.t);
    const lv = document.createElement("span");
    lv.className = "lv";
    lv.textContent = line.level;
    const msg = document.createElement("span");
    msg.className = "msg";
    msg.textContent = line.message;
    row.append(t, lv, msg);
    frag.appendChild(row);
  }
  ui.logs.innerHTML = "";
  ui.logs.appendChild(frag);
  if (ui.autoScroll.checked) ui.logs.scrollTop = ui.logs.scrollHeight;
}

function appendLog(entry, { render = true } = {}) {
  logLines.push(entry);
  if (logLines.length > MAX_LOG_LINES) logLines.shift();
  if (!render) return;
  if (!levelPass(entry.level)) return;

  const row = document.createElement("div");
  row.className = `line ${entry.level}`;
  row.innerHTML = `<span class="t"></span><span class="lv"></span><span class="msg"></span>`;
  row.children[0].textContent = fmtTime(entry.t);
  row.children[1].textContent = entry.level;
  row.children[2].textContent = entry.message;
  ui.logs.appendChild(row);
  if (ui.logs.childElementCount > MAX_LOG_LINES) ui.logs.removeChild(ui.logs.firstElementChild);
  if (ui.autoScroll.checked) ui.logs.scrollTop = ui.logs.scrollHeight;
}

// ------------------------------------------------------------------ 请求表

function renderRequests() {
  const showNoisy = ui.showNoisy.checked;
  const rows = reqRows.filter((row) => showNoisy || !row.noisy).slice(-MAX_REQ_ROWS).reverse();

  ui.requests.innerHTML = "";
  for (const row of rows) {
    const tr = document.createElement("tr");
    if (row.noisy) tr.className = "noisy";
    tr.innerHTML = `<td></td><td></td><td class="path"></td><td class="${statusClass(row.status)}"></td><td></td><td></td>`;
    const cells = tr.children;
    cells[0].textContent = fmtTime(row.t);
    cells[1].textContent = row.method;
    cells[2].textContent = row.path;
    cells[3].textContent = row.status;
    cells[4].textContent = `${row.ms} ms`;
    cells[5].textContent = row.bytes ? fmtSize(row.bytes) : "—";
    ui.requests.appendChild(tr);
  }
  if (rows.length === 0) {
    ui.requests.innerHTML = `<tr><td colspan="6" class="hint">还没有请求（刷新真机/浏览器会看到）</td></tr>`;
  }
}

// ------------------------------------------------------------------ 内容树

function renderTree(data) {
  ui.tree.innerHTML = "";
  if (!data || !data.ok) {
    const p = document.createElement("p");
    p.className = "warn-inline";
    p.textContent = `内容根目录不可用：${(data && data.root) || "?"}（${(data && data.rootState && data.rootState.reason) || "未知原因"}）`;
    ui.tree.appendChild(p);
    return;
  }

  if (!data.channels || data.channels.length === 0) {
    const p = document.createElement("p");
    p.className = "hint";
    p.textContent = "根目录下还没有 channel 目录。点上方「创建空目录骨架」生成 lan/android 结构。";
    ui.tree.appendChild(p);
    return;
  }

  for (const ch of data.channels) {
    const chLine = document.createElement("div");
    chLine.className = "ch";
    chLine.textContent = `${ch.channel}/`;
    ui.tree.appendChild(chLine);

    for (const pf of ch.platforms || []) {
      const pfLine = document.createElement("div");
      pfLine.className = "pf";
      const manifest = pf.manifest || {};
      if (!manifest.exists) {
        pfLine.textContent = `${pf.platform}/　manifest.json 缺失`;
        pfLine.classList.add("warn-inline");
      } else if (manifest.parseError) {
        pfLine.textContent = `${pf.platform}/　manifest.json 解析失败：${manifest.parseError}`;
        pfLine.classList.add("warn-inline");
      } else {
        pfLine.textContent = `${pf.platform}/　manifest contentVersion=${manifest.contentVersion ?? "?"} · files=${(manifest.files || []).length}`;
      }
      ui.tree.appendChild(pfLine);

      const base = `/${encodeURIComponent(ch.channel)}/${encodeURIComponent(pf.platform)}`;
      for (const [treeName, items] of Object.entries(pf.trees || {})) {
        const dirLine = document.createElement("div");
        dirLine.className = "file";
        dirLine.innerHTML = `<span class="dir">${treeName}/</span>`;
        ui.tree.appendChild(dirLine);

        if (!items || items.length === 0) {
          const empty = document.createElement("div");
          empty.className = "file";
          empty.innerHTML = `<span class="dir">（空）</span>`;
          ui.tree.appendChild(empty);
          continue;
        }

        for (const item of items) {
          const line = document.createElement("div");
          line.className = "file";
          const nameWrap = document.createElement("span");
          if (item.directory) {
            nameWrap.className = "dir";
            nameWrap.textContent = `${item.name}/`;
          } else {
            const a = document.createElement("a");
            a.href = `${base}/${treeName}/${item.name.split("/").map(encodeURIComponent).join("/")}`;
            a.target = "_blank";
            a.rel = "noreferrer";
            a.textContent = item.name;
            nameWrap.appendChild(a);
          }
          const size = document.createElement("span");
          size.className = "size";
          size.textContent = item.directory ? "" : fmtSize(item.size);
          line.append(nameWrap, size);
          ui.tree.appendChild(line);
        }
      }
    }
  }
}

async function loadTree() {
  try {
    const res = await fetch("/api/tree", { cache: "no-store" });
    renderTree(await res.json());
  } catch (error) {
    ui.tree.innerHTML = `<p class="warn-inline">内容树加载失败：${error.message}</p>`;
  }
}

async function loadHealth() {
  try {
    const res = await fetch("/api/health", { cache: "no-store" });
    renderHealth(await res.json());
  } catch {
    ui.conn.className = "pill pill-error";
    ui.conn.textContent = "服务不可达";
  }
}

// ------------------------------------------------------------------ 动作

async function postJson(url, body) {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body || {}),
  });
  let data = null;
  try {
    data = await res.json();
  } catch {
    data = { ok: false, message: `HTTP ${res.status}` };
  }
  return { status: res.status, data };
}

ui.btnApplyRoot.addEventListener("click", async () => {
  const input = ui.rootInput.value.trim();
  if (!input) {
    ui.rootResult.textContent = "请先填写目标目录（留空不会改变当前 root）。";
    return;
  }
  ui.btnApplyRoot.disabled = true;
  ui.rootResult.textContent = "切换中…";
  try {
    const { data } = await postJson("/api/root", { root: input, createSkeleton: ui.createWithRoot.checked });
    if (data.ok) {
      ui.rootResult.textContent = `已切换到 ${data.root}（${data.rootState.ok ? "就绪" : data.rootState.reason}）`;
      await loadHealth();
      await loadTree();
    } else {
      ui.rootResult.textContent = `失败：${data.message}\n提示：可执行 ${commandFor(input)}`;
    }
  } catch (error) {
    ui.rootResult.textContent = `失败：${error.message}`;
  } finally {
    ui.btnApplyRoot.disabled = false;
  }
});

ui.btnSkeleton.addEventListener("click", async () => {
  const input = ui.rootInput.value.trim();
  ui.btnSkeleton.disabled = true;
  ui.rootResult.textContent = "创建中…";
  try {
    const { data } = await postJson("/api/root/skeleton", input ? { root: input } : {});
    if (data.ok) {
      ui.rootResult.textContent = `已在 ${data.root} 创建 ${(data.created || []).length} 个目录（lan/android/{,HotUpdate,AssetBundles}）`;
      await loadHealth();
      await loadTree();
    } else {
      ui.rootResult.textContent = `失败：${data.message}`;
    }
  } catch (error) {
    ui.rootResult.textContent = `失败：${error.message}`;
  } finally {
    ui.btnSkeleton.disabled = false;
  }
});

ui.btnRefreshTree.addEventListener("click", loadTree);
ui.logLevel.addEventListener("change", renderLogs);
ui.showNoisy.addEventListener("change", renderRequests);
ui.btnClearLogs.addEventListener("click", () => {
  logLines.length = 0;
  ui.logs.innerHTML = "";
});

// ------------------------------------------------------------------ SSE

function connectStream() {
  if (typeof EventSource === "undefined") {
    ui.conn.className = "pill pill-warn";
    ui.conn.textContent = "浏览器不支持 SSE（轮询中）";
    return;
  }

  const source = new EventSource("/api/stream");

  source.addEventListener("open", () => {
    ui._sseOpen = true;
    renderConnState();
  });

  source.addEventListener("snapshot", (event) => {
    const data = JSON.parse(event.data);
    logLines.length = 0;
    for (const line of data.logs || []) logLines.push(line);
    renderLogs();

    reqRows.length = 0;
    for (const row of data.requests || []) reqRows.push(row);
    renderRequests();

    if (data.health) renderHealth(data.health);
  });

  source.addEventListener("log", (event) => {
    appendLog(JSON.parse(event.data));
  });

  source.addEventListener("request", (event) => {
    const row = JSON.parse(event.data);
    reqRows.push(row);
    if (reqRows.length > MAX_REQ_ROWS) reqRows.shift();
    if (ui.showNoisy.checked || !row.noisy) renderRequests();
  });

  source.addEventListener("status", (event) => {
    renderHealth(JSON.parse(event.data));
    loadTree();
  });

  source.addEventListener("error", () => {
    ui._sseOpen = false;
    renderConnState();
    // EventSource 自带重连；这里只刷新一次状态，避免界面停在旧数据。
    loadHealth();
  });
}

// ------------------------------------------------------------------ 启动

async function init() {
  await loadHealth();
  await loadTree();
  connectStream();

  // 兜底轮询：SSE 断开或浏览器不支持时状态仍然新鲜。
  setInterval(loadHealth, 5000);
  setInterval(loadTree, 15000);
}

init();
