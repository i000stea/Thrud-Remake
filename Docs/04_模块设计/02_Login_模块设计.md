# Login 模块设计

## 模块定位

`Login` 是单机阶段的进入页与玩家资料入口。当前阶段不做真实账号系统，但必须把未来账号、云存档、联机鉴权的接口预留好。

## 职责边界

负责：

- 开始游戏 / 继续游戏 / 设置 / 退出
- 玩家昵称、头像、存档槽位入口
- 本地资料选择与加载
- 未来账号接口预留

不负责：

- 启动初始化
- 主城导航
- 存档读写细节
- 热更逻辑

## 首版功能要点

- 显示当前本地可用存档槽位
- 支持“新游戏 / 继续游戏 / 切换存档槽”
- 支持昵称、头像等轻量资料页
- 提供未来账号接口占位，不接真实服务

## 建议页面结构

- 开始游戏
- 继续游戏
- 存档槽位列表
- 资料页
- 设置入口

## 数据所有权

- 玩家当前显示资料来自 `Profile`
- 存档槽位列表来自 `Save`
- 版本与构建信息可由 `Boot` 注入显示

## 关键接口

- `ILoginFlowService`
- `IAccountGateway`（占位）
- `ICloudSaveGateway`（占位）

## 状态流

```text
Boot -> Login
Login
  -> 选择存档槽
  -> 加载 Profile
  -> 进入 MainCity
```

## 依赖关系

- 依赖：`Profile`、`Save`
- 被依赖：`Boot`

## 首版验收

- 至少支持一个新档创建与已有档继续
- 切换存档槽后，进入主城的玩家状态正确
- 未来账号接口存在，但关闭时不影响单机流程

## 后续扩展

- 真实账号绑定
- 云存档同步
- 联机鉴权与好友入口

---

## 实现记录（2026-09-01，重写为 QFramework + SceneFlow）

### 状态

已按当前框架（QFramework 分层 + SceneFlow 场景管理，决策 D25）重写 `1LoginScene` 的旧 `LoginControl`：

| 文件（`Assets/02_Scripts/01_Login/`，命名空间 `TeaGame.Login`） | 职责 |
|------|------|
| `LoginArchitecture.cs` | 模块架构入口，注册 LoginModel / LoginSystem |
| `LoginModel.cs` | 登录流程状态（防重入 IsBusy） |
| `LoginSystem.cs` | 登录逻辑：防重入 → **热更检查（占位）** → 经 `SceneFlow` 切换主城 |
| `LoginEvents.cs` | `LoginCompletedEvent`（登录完成，表现层订阅收尾） |
| `LoginViewController.cs` | 场景表现层：遮罩渐隐/音频淡入淡出演出、开始按钮转发到 System |
| `Editor/LoginSceneBinder.cs` | 迁移工具：菜单 `Thrud/Bind LoginScene`，把场景中旧组件换为新组件并回填字段 |

### 行为（v1）

```
开始按钮点击
 → LoginSystem.BeginLogin(mainSceneKey)     [防重入]
 → 热更检查（占位：M0 接入 YooAsset 后执行版本比对→下载→HybridCLR 加载）
 → SendEvent<LoginCompletedEvent>            [表现层音频淡出]
 → SceneFlow.RequestLoad("2MainScene")       [名称 key，additive，卸载登录场景]
```

- 目标场景 key 为 `LoginViewController.mainSceneKey`（Inspector 可配置，默认 `2MainScene`）
- `SceneFlow` 增加静态 `Instance`（常驻 ConfigScene，供任意模块请求切换）

### 与设计文档的差异

- 首版聚焦"开始 → 热更检查 → 主城"最小链路；存档槽位、资料页、设置入口暂未实现（依赖 `Save`/`Profile`，M2 落地）
- 账号/云存档接口占位（`IAccountGateway`/`ICloudSaveGateway`）随 M2 数据层实现
- 原"不负责热更逻辑"边界已调整：登录场景承担**资源热更检查**（用户决策），M0 接入 YooAsset 后替换占位

### 迁移步骤（用户执行）

1. Unity 打开 `1LoginScene` → 菜单 `Thrud/Bind LoginScene`（自动换绑组件、回填演出参数与目标 key）
2. 若场景无 Button 组件，工具会警告，需在 Inspector 手动给 `startButton` 赋值
3. Play 验证：从 `0ConfigScene` 启动 → 自动加载 `1LoginScene` → 点开始 → 热更检查日志 → 切 `2MainScene`
4. 迁移完成后旧 `LoginControl.cs`（`Assets/02_Scripts/Before/00Login/`）无引用，可删除
