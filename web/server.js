const http = require("node:http");
const fs = require("node:fs/promises");
const path = require("node:path");
const { spawn } = require("node:child_process");
const { randomUUID } = require("node:crypto");
const { generateProgressData } = require("./sync-doc-tasks");

const HOST = "127.0.0.1";
const PORT = 3210;
// 排序拖拽条范围：0 表示「特别紧急」，数值越小越靠前
const SORT_ORDER_MIN = 0;
const SORT_ORDER_MAX = 10;
const DEFAULT_SORT_ORDER = 5;
const ROOT_DIR = __dirname;
const PUBLIC_DIR = path.join(ROOT_DIR, "public");
const DATA_DIR = path.join(ROOT_DIR, "data");
const BACKUP_DIR = path.join(DATA_DIR, "backups");
const DATA_FILE = path.join(DATA_DIR, "progress.json");

let browserOpened = false;

const DEFAULT_DATA = {
  meta: {
    title: "Thrud Remake 进度监控表",
    updatedAt: new Date().toISOString(),
  },
  tasks: [
    {
      id: "task-demo-1",
      title: "整理战斗 HUD 字段",
      taskType: "模块开发",
      description: "确认需要展示的生命、技能、切人、关卡信息",
      status: "进行中",
      priority: "高",
      owner: "Tea",
      updatedAt: new Date().toISOString(),
    },
    {
      id: "task-demo-2",
      title: "补全真机回归记录",
      taskType: "文档整理",
      description: "把关键问题与验证结论同步到文档",
      status: "未开始",
      priority: "中",
      owner: "Tea",
      updatedAt: new Date().toISOString(),
    },
  ],
};

async function ensureDataFile() {
  await fs.mkdir(DATA_DIR, { recursive: true });
  try {
    await fs.access(DATA_FILE);
  } catch {
    try {
      const generated = await generateProgressData();
      await fs.writeFile(DATA_FILE, JSON.stringify(generated, null, 2), "utf8");
    } catch {
      await fs.writeFile(DATA_FILE, JSON.stringify(DEFAULT_DATA, null, 2), "utf8");
    }
  }
}

async function readData() {
  await ensureDataFile();
  const content = await fs.readFile(DATA_FILE, "utf8");
  const parsed = JSON.parse(content);
  if (!parsed.meta) parsed.meta = {};
  if (!Array.isArray(parsed.tasks)) parsed.tasks = [];
  return parsed;
}

async function createBackup(content) {
  await fs.mkdir(BACKUP_DIR, { recursive: true });
  const stamp = new Date().toISOString().replace(/[:.]/g, "-");
  const backupPath = path.join(BACKUP_DIR, `progress.${stamp}.json`);
  await fs.writeFile(backupPath, content, "utf8");
}

async function writeData(data) {
  const normalized = {
    meta: {
      ...data.meta,
      updatedAt: new Date().toISOString(),
    },
    tasks: Array.isArray(data.tasks) ? data.tasks : [],
  };

  let previousContent = "";
  try {
    previousContent = await fs.readFile(DATA_FILE, "utf8");
  } catch {
    previousContent = JSON.stringify(DEFAULT_DATA, null, 2);
  }

  await createBackup(previousContent);
  await fs.writeFile(DATA_FILE, JSON.stringify(normalized, null, 2), "utf8");
  return normalized;
}

function sendJson(response, statusCode, data) {
  response.writeHead(statusCode, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
  });
  response.end(JSON.stringify(data));
}

function sendText(response, statusCode, text) {
  response.writeHead(statusCode, {
    "Content-Type": "text/plain; charset=utf-8",
  });
  response.end(text);
}

function getContentType(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  const mapping = {
    ".html": "text/html; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".json": "application/json; charset=utf-8",
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
    ".svg": "image/svg+xml; charset=utf-8",
    ".ico": "image/x-icon",
  };
  return mapping[ext] || "application/octet-stream";
}

async function parseBody(request) {
  const chunks = [];
  for await (const chunk of request) {
    chunks.push(chunk);
  }
  const raw = Buffer.concat(chunks).toString("utf8");
  if (!raw) return {};
  try {
    return JSON.parse(raw);
  } catch {
    throw new Error("请求体不是合法 JSON");
  }
}

function normalizeSortOrder(value) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return DEFAULT_SORT_ORDER;
  return Math.min(SORT_ORDER_MAX, Math.max(SORT_ORDER_MIN, Math.round(numeric)));
}

function normalizeTask(input, oldTask = null) {
  const now = new Date().toISOString();
  return {
    id: oldTask?.id || input.id || randomUUID(),
    title: String(input.title || oldTask?.title || "").trim(),
    taskType: String(input.taskType || oldTask?.taskType || "其他").trim(),
    description: String(input.description || oldTask?.description || "").trim(),
    status: String(input.status || oldTask?.status || "未开始").trim(),
    priority: String(input.priority || oldTask?.priority || "中").trim(),
    // 用 ?? 而不是 ||：0 是合法值（特别紧急）
    sortOrder: normalizeSortOrder(input.sortOrder ?? oldTask?.sortOrder),
    owner: String(input.owner || oldTask?.owner || "").trim(),
    updatedAt: now,
  };
}

async function handleApi(request, response, url) {
  if (request.method === "GET" && url.pathname === "/api/progress") {
    const data = await readData();
    return sendJson(response, 200, data);
  }

  if (request.method === "POST" && url.pathname === "/api/tasks") {
    const body = await parseBody(request);
    if (!body.title || !String(body.title).trim()) {
      return sendJson(response, 400, { message: "任务标题不能为空" });
    }

    const data = await readData();
    const task = normalizeTask(body);
    data.tasks.unshift(task);
    const saved = await writeData(data);
    return sendJson(response, 201, { task, meta: saved.meta });
  }

  if (url.pathname.startsWith("/api/tasks/")) {
    const taskId = decodeURIComponent(url.pathname.replace("/api/tasks/", ""));
    const data = await readData();
    const index = data.tasks.findIndex((task) => task.id === taskId);

    if (index === -1) {
      return sendJson(response, 404, { message: "未找到对应任务" });
    }

    if (request.method === "PATCH") {
      const body = await parseBody(request);
      const nextTask = normalizeTask(body, data.tasks[index]);
      if (!nextTask.title) {
        return sendJson(response, 400, { message: "任务标题不能为空" });
      }
      data.tasks[index] = nextTask;
      const saved = await writeData(data);
      return sendJson(response, 200, { task: nextTask, meta: saved.meta });
    }

    if (request.method === "DELETE") {
      const [deleted] = data.tasks.splice(index, 1);
      const saved = await writeData(data);
      return sendJson(response, 200, { task: deleted, meta: saved.meta });
    }
  }

  return false;
}

async function serveStatic(response, requestPath) {
  const safePath = requestPath === "/"
    ? path.join(PUBLIC_DIR, "index.html")
    : path.join(PUBLIC_DIR, requestPath.replace(/^\/+/, ""));

  const normalized = path.normalize(safePath);
  if (!normalized.startsWith(PUBLIC_DIR)) {
    sendText(response, 403, "禁止访问");
    return;
  }

  try {
    const content = await fs.readFile(normalized);
    response.writeHead(200, {
      "Content-Type": getContentType(normalized),
      "Cache-Control": "no-store",
    });
    response.end(content);
  } catch {
    sendText(response, 404, "文件不存在");
  }
}

function openBrowser(url) {
  if (browserOpened) return;
  browserOpened = true;

  spawn("cmd", ["/c", "start", "", url], {
    cwd: ROOT_DIR,
    detached: true,
    stdio: "ignore",
  }).unref();
}

const server = http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url, `http://${request.headers.host}`);

    if (url.pathname.startsWith("/api/")) {
      const handled = await handleApi(request, response, url);
      if (handled !== false) return;
      return sendJson(response, 404, { message: "接口不存在" });
    }

    return serveStatic(response, url.pathname);
  } catch (error) {
    return sendJson(response, 500, {
      message: error.message || "服务器内部错误",
    });
  }
});

async function start() {
  await ensureDataFile();
  server.listen(PORT, HOST, () => {
    const url = `http://${HOST}:${PORT}`;
    console.log(`[progress-web] 服务已启动: ${url}`);
    console.log("[progress-web] 关闭此命令行窗口即可停止服务");
    openBrowser(url);
  });
}

process.on("SIGINT", () => {
  console.log("\n[progress-web] 正在停止服务...");
  server.close(() => process.exit(0));
});

process.on("SIGTERM", () => {
  server.close(() => process.exit(0));
});

start().catch((error) => {
  console.error("[progress-web] 启动失败:", error);
  process.exit(1);
});
