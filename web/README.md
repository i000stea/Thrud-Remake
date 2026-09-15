# 本地进度监控网页

这是一个零依赖的本地网页工具，使用方式如下：

1. 双击 `start-progress-web.cmd`
2. 命令行会启动 Node.js 本地服务
3. 浏览器会自动打开网页
4. 在网页里新增、编辑、删除任务
5. 数据会写回 `data/progress.json`
6. 关闭命令行窗口后，Node 服务会随之停止

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
