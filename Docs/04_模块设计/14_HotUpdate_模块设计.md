# HotUpdate 模块设计

## 模块定位

`HotUpdate` 是独立技术模块，负责热更代码与资源更新流程。它不并入 `Boot` 或 `Data`，但会被 `Boot` 在启动时调用。

## 职责边界

负责：

- 版本检查
- 资源分组与清单管理
- 热更代码下载与加载
- 回滚与失败恢复
- 构建工具约束

不负责：

- 启动 UI 业务控制
- 业务配置解释
- 存档读写

## 首版功能要点

- HybridCLR 热更代码更新
- YooAsset 资源更新
- 本地模拟服务器调试
- 更新失败重试与清缓存恢复
- 版本信息写入日志

## 核心分层

- `VersionService`
- `PatchDownloadService`
- `HotfixAssemblyLoader`
- `PatchRecoveryService`

## 依赖关系

- 依赖：`TelemetryLog`
- 被依赖：`Boot`、`Config`

## 首版验收

- 能独立完成版本比对、下载、加载
- 失败时能恢复，不把异常吞进启动流程
- 开发阶段本地服务器即可演示完整更新链路

## 后续扩展

- 分渠道更新
- 更细的分包策略
- CDN 与灰度更新策略
