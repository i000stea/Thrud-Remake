# 本地进度监控网页

这是一个零依赖的本地网页工具，使用方式如下：

1. 双击 `start-progress-web.cmd`
2. 命令行会启动 Node.js 本地服务
3. 浏览器会自动打开网页
4. 在网页里新增、编辑、删除任务
5. 数据会写回 `data/progress.json`
6. 关闭命令行窗口后，Node 服务会随之停止

## 代码地图（第一版）

启动同一个服务后访问 `http://127.0.0.1:3210/code-atlas.html`，即可查看 `Assets/02_Scripts` 的自动扫描结果：

- 程序集依赖：从各 `.asmdef` 的 `references` 生成方向关系
- 文件目录：按路径、程序集、namespace、类型和行数筛选
- 文件详情：点击文件查看元信息，沿程序集和 namespace 回到 IDE 阅读
- 重新扫描：每次请求实时读取磁盘，不产生缓存文件，也不修改 Unity 资产

接口：`GET /api/code-atlas`。当前扫描器刻意只分析项目自己的 `Assets/02_Scripts`；QFramework 等第三方源码作为外部依赖保留，后续可按阅读需要增加“展开依赖”扫描。

## 目录说明

- `server.js`: 本地 Node 服务，负责读取与写入 `json`
- `public/`: 网页前端文件
- `data/progress.json`: 进度数据
- `data/backups/`: 每次写入前自动生成历史备份
- `start-progress-web.cmd`: 一键启动脚本

## 接口

- `GET /api/progress`
- `POST /api/tasks`
- `PATCH /api/tasks/:id`
- `DELETE /api/tasks/:id`

## 备注

- 默认端口是 `3210`
- 如果端口冲突，可修改 `server.js` 顶部的 `PORT`
- 首次打开后即可直接在前端编辑，保存会自动写入本地文件
- **写盘时机**：行内改动（状态 / 优先级 / 排序拖拽条）走 **5 秒防抖** —— 停止操作 5 秒后才统一写入，
  期间再有新改动会重新计时，同一任务的多处改动合并成一次 `PATCH`。切换标签页 / 关闭页面 /
  点「立即保存」/ 点「重新读取」时立即写入；弹窗新增与编辑、删除任务仍是立即写入。
- 任务字段 `sortOrder`（排序）：范围 `0~10`，数值越小越靠前，同一优先级内按它排序；
  `0` 视为「特别紧急」（红色标记 + 独立动画，且该行动画不被「已完成」抑制）。
  旧数据没有该字段时按默认值 `5` 处理，只有改动过的任务才会写入该字段。

## ⚠️ 与 `sync-doc-tasks.js` 的关系（重要）

- `data/progress.json` **已于 2026-09-15 人工重排**（按 P1A → P1B → P1C → P2 → P3 阶段组织，
  任务标题带 `【P1A】`/`【P1B】` 等前缀，id 用 `p1a-1` 这类稳定可读值，而非随机 UUID）。
- `sync-doc-tasks.js` 是**整体覆写**式生成器：它从 `Docs/99_下一步行动/README.md` 重新抽取任务，
  **每次运行都会重新分配随机 id 并丢弃当前 `progress.json` 的全部人工整理结果**（包括状态、优先级、
  sortOrder 与描述）。所以**不要在这个文件已经人工整理过之后直接跑它**。
- 如果确实要从文档重新生成，请先手动备份当前的 `data/progress.json`；
  或先修改 `sync-doc-tasks.js` 让它按 `raw` 字段合并（保留既有 id / status / priority / sortOrder），再运行。
- 行内编辑写入时，`server.js` 的 `normalizeTask` 只会保留
  `id / title / taskType / description / status / priority / sortOrder / owner / updatedAt` 这九个字段，
  其余自定义字段（如 `source`、`raw`）会在该任务被编辑后丢失。
