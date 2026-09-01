# Boot 模块设计

## 模块定位

`Boot` 是应用启动入口，不参与长期业务状态维护。它的职责是把运行环境准备好，然后把控制权交给 `Login`。`Boot` 不能成长为“总控中心”。

## 职责边界

负责：

- 本地环境初始化
- 热更检查与更新流程入口
- 日志、埋点、设置、设备信息初始化
- 异常恢复入口
- 跳转到 `Login`

不负责：

- 登录业务逻辑
- 主城 UI 逻辑
- 存档业务解释
- 战斗与养成逻辑

## 首版功能要点

- 启动时初始化 `TelemetryLog`、音频、画质默认值、输入设置、语言与设备信息
- 调用 `HotUpdate` 完成资源与热更代码检查
- 若更新失败，提供重试、跳过、清缓存、恢复默认资源等入口
- 若初始化成功，进入 `Login`

## 输入与输出

输入：

- 本地设置文件
- 本地资源版本信息
- 热更远端清单（开发阶段可为本地服务器）

输出：

- `BootContext`
- 初始化完成事件
- 跳转到 `Login` 的流程命令

## 建议状态流

```text
AppStart
 -> InitLocalEnvironment
 -> CheckHotUpdate
 -> ApplyPatchOrSkip
 -> ShowRecoveryIfFailed
 -> EnterLogin
```

## 关键接口

- `IBootInitializer`
- `IBootRecoveryService`
- `IStartupFlowService`

## 依赖关系

- 依赖：`HotUpdate`、`Config`、`Save`、`TelemetryLog`
- 被依赖：无，应用启动时最先执行

## UI/交互面

- 启动画面
- 更新进度条
- 错误恢复弹窗
- 开发模式下的“清缓存/重置本地资源”入口

## 首版验收

- 冷启动可稳定进入 `Login`
- 热更失败时可恢复，不直接卡死
- 初始化日志完整落盘
- 任何初始化失败都能给出明确提示与恢复路径

## 后续扩展

- 启动动画与品牌页
- 分渠道配置注入
- 更细的设备性能分档
