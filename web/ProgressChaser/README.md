# Thrud 本地工具：进度监控 + 代码知识图谱画布

零配置的本地网页工具，一条 `.cmd` 启动，离线可用。包含两个页面：

- `http://127.0.0.1:3210/`：进度监控面板；
- `http://127.0.0.1:3210/code-atlas.html`：代码知识图谱画布（Cytoscape.js + ELK.js）。

## 一键启动

双击 `start-progress-web.cmd`。脚本会：

1. 检查 `node` / `npm` 是否可用；
2. 调用 `scripts/ensure-deps.js` 检查依赖：
   - `node_modules` 齐全且安装戳等于 `package-lock.json` 哈希时直接跳过；
   - 否则执行 `npm ci`（没有锁文件时才用 `npm install`）；
   - 安装后写入 `node_modules/.thrud-install-stamp`，避免重复安装；
3. 执行 `node server.js`，服务就绪后自动打开浏览器。

关闭命令行窗口即停止服务。首次使用需要一次网络安装；之后日常启动不访问网络。

> **维护提示**：`start-progress-web.cmd` 必须保持**纯 ASCII**。中文 Windows 的 `cmd.exe` 按 GBK 解析批处理文件，文件里的 UTF-8 中文会乱码并破坏 `if (...)` 语句块；中文提示一律由 Node 输出，脚本开头的 `chcp 65001` 保证控制台正确显示。修改后可用 `node scripts/smoke-cmd.js` 验证。

## 代码画布（Code Atlas）

启动同一个服务后访问 `code-atlas.html`：

- **程序集图**：`.asmdef` 的引用方向，项目内程序集与第三方边界样式不同，点击项目内程序集进入类图；
- **类图**：当前程序集的类型 + 实际指向的外部类型/程序集，点击类高亮一跳邻居（可切两跳），点击连线在右侧查看方法、事件、读写证据与源码片段；
- **关系类型**：`assembly` / `class` / `call` / `event` / `data`，同一方向聚合为一条边，边上的数字是明细数量；
- **交互**：滚轮围绕鼠标缩放（10%–400%）、拖拽空白平移、拖拽节点保存坐标、自动布局（ELK Worker）、撤销、重置、适应画布、搜索定位；
- **滚轮力度**：左栏「视图」滑块实时调节 `wheelSensitivity`（**0.05 ~ 10，对数刻度**，默认 0.18，往右越猛），设置存 `localStorage`，刷新后保留；
- **性能模式**：缩放级别自动分级显示（LOD）；布局计算在 Web Worker，超过 3 秒出现「取消布局」，取消或失败时保留上次位置。

### 依赖与渲染

| 依赖 | 作用 | 版本（`package-lock.json` 锁定） |
|---|---|---|
| `cytoscape` | Canvas 渲染、缩放/平移/拖拽/筛选 | 3.34.3 |
| `elkjs` | 分层布局计算；浏览器端用官方 `elk-api.js` + `elk-worker.min.js`，**ELK 在真正的 Web Worker 中运行** | 0.12.0 |
| `cytoscape-elk` | 仅用于 Node 端 headless 布局测试（`atlas-view.test.js`），**不在浏览器运行** | 2.3.0 |

浏览器端依赖全部由本地服务通过 `/vendor/*` 白名单提供，**不使用 CDN**，离线启动不依赖网络。

> ⚠️ 浏览器端**不要**加载 `elk.bundled.js`：它的 `ELKNode` 一旦检测到全局 `Worker` 就会执行 `new Worker(undefined)`（无效的内层嵌套 Worker），必然报错并导致布局失败。运行时统一走 `elk-api.js` + `elk-worker.min.js` 这套官方组合。

### 数据契约

三份 JSON 各司其职，互不覆盖：

| 文件 | 维护方 | 说明 |
|---|---|---|
| `data/code-atlas.generated.json` | 扫描器整体重建 | 自动关系与源码证据；每次扫描前备份为 `.bak`，扫描失败不动旧文件 |
| `data/code-atlas.manual.json` | **人工维护** | 人工关系与注释；扫描绝不写入 |
| `data/code-atlas.layout.json` | 页面自动保存 | 节点坐标与视口；防抖保存，带 `revision` + `token` 冲突检查 |

`generated` 的 `confidence`：`declared`（asmdef 显式声明）、`candidate`（词法静态候选）、`manual`（人工）。静态候选不是完整语义调用图，页面会显示置信度。

`layout` 读入时兼容 `schemaVersion` 1/2，保存时统一写 2。

### 手工补充人工关系

编辑 `data/code-atlas.manual.json`：

```json
{
  "schemaVersion": 1,
  "relations": [
    {
      "id": "manual-role-hud",
      "source": "TeaGame.Battle::TeaGame.BattleRoleSystem",
      "target": "TeaGame.Battle::TeaGame.BattleHudState",
      "kind": "data",
      "label": "角色切换时同步 HUD",
      "note": "用于理解角色选择、阵亡接管和 HUD 更新的业务链"
    }
  ],
  "notes": [
    { "node": "TeaGame.Battle::TeaGame.BattleRoleSystem", "text": "推荐先读 SelectRole，再读 HandleRoleDowned。" }
  ]
}
```

保存后点页面「重新读取 JSON」即可生效。校验失败会在页面显示具体文件与字段，不会破坏上一份可用图。

### 重新扫描

点页面「重新扫描代码」或执行 `npm run scan`。扫描器只分析项目自己的 `Assets/02_Scripts`，第三方源码作为外部边界保留；被删除的类型作为 `orphan` 保留坐标与注释。

## 接口

- `GET  /api/health`：就绪探针（启动脚本/自动化用）
- `GET  /api/progress` / `POST /api/tasks` / `PATCH /api/tasks/:id` / `DELETE /api/tasks/:id`
- `GET  /api/code-atlas`：读取 generated + manual + layout
- `POST /api/code-atlas/scan`：重新扫描（只重建 generated）
- `PUT  /api/code-atlas/layout`：保存布局（409 表示 token/revision 冲突）
- `GET  /api/code-atlas/source?file=...`：源码片段（路径白名单）
- `GET  /vendor/*`：浏览器端依赖白名单

## 常用命令

```bash
npm start                       # 启动服务（等价于双击 .cmd）
npm test                        # 自动测试（node --test）
npm run scan                    # 重建 generated.json
npm run baseline                # K0 基准：备份三份 JSON 并统计图规模
node scripts/perf-atlas.js      # 合成大图性能取证（默认 500 节点 / 2000 边）
node scripts/check-elk.js        # 用真实 JSON 跑通全部视图的 ELK 布局（定位坏图）
node scripts/smoke-server.js    # 启动冒烟（health / vendor / code-atlas）
node scripts/smoke-cmd.js       # 一键启动脚本端到端冒烟（真实调用 .cmd 后关闭）
node scripts/verify-layout-roundtrip.js   # 布局读写与冲突拒绝闭环
```

## 进度面板：写盘时机与排序

- 默认端口 `3210`；端口冲突可改 `server.js` 顶部的 `PORT`，或设环境变量 `PROGRESS_PORT`。
- **写盘时机**：行内改动（状态 / 优先级 / 排序拖拽条）走 **5 秒防抖** —— 停止操作 5 秒后才统一写入，期间再有改动会重新计时，同一任务的多处改动合并成一次 `PATCH`。切换标签页 / 关闭页面 / 点「立即保存」/ 点「重新读取」时立即写入；弹窗新增与编辑、删除任务仍是立即写入。
- 任务字段 `sortOrder`（排序）：范围 `0~10`，数值越小越靠前，同一优先级内按它排序；`0` 视为「特别紧急」（红色标记 + 独立动画，且该行动画不被「已完成」抑制）。旧数据没有该字段时按默认值 `5` 处理，只有改动过的任务才会写入该字段。

## 已知限制

- 扫描器是词法静态分析：不解析重载、动态派发、反射、依赖注入和条件编译；
- 单图超过约 1,000 节点 / 5,000 聚合边时，若 Cytoscape 经降级后仍无法满足交互目标，再评估 WebGL 渲染器（见设计文档 §2.3 / §11）；
- 页面视觉与帧率需按设计文档 §10.3 由用户人工验收；
- `sync-doc-tasks.js` 是整体覆写式生成器，`data/progress.json` 已人工整理，勿直接运行（见下）。
- `npm test`（即 `node --test`）在**受限 shell / 沙箱**里会报 `spawn EPERM`：它需要把每个测试文件的 stdout 走管道，而受限环境禁止命名管道。此时改为逐文件执行 `node atlas.test.js`、`node atlas-schema.test.js`、`node atlas-view.test.js`、`node atlas-elk.test.js`（共 11 项），结果一致。

## ⚠️ 与 `sync-doc-tasks.js` 的关系（重要）

- `data/progress.json` 已于 2026-09-15 人工重排（P1A→P1B→P1C→P2→P3，id 为 `p1a-1` 这类稳定值）。
- `sync-doc-tasks.js` 每次运行都会**重新分配随机 id 并丢弃**当前 `progress.json` 的人工整理结果（状态、优先级、sortOrder、描述）。**不要在人工整理后直接运行。**
- 如确需从文档重生成，先备份 `data/progress.json`，或先改造它按 `raw` 字段合并（保留既有 id / status / priority / sortOrder）。
- `server.js` 的 `normalizeTask` 只保留 `id / title / taskType / description / status / priority / sortOrder / owner / updatedAt`，其余字段（`source`、`raw`）在该任务被编辑后丢失。

## 目录说明

- `server.js`：本地 Node 服务（进度接口 + 代码图谱接口 + 静态资源）
- `atlas-core.js`：扫描器、JSON 校验、原子写入、布局冲突检查
- `public/`：前端文件（`code-atlas.*`、`atlas-model.js`、`atlas-view.js`、`elk-worker.js`）
- `scripts/`：依赖检查、扫描、基准、性能、冒烟与闭环验证脚本
- `data/`：进度、三份图谱 JSON、`backups/` 历史备份
- `start-progress-web.cmd`：一键启动脚本
