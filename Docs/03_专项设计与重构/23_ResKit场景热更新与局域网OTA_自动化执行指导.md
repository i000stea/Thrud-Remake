# ResKit 场景热更新与局域网 OTA · 自动化执行指导

> 状态：方案已确认；**S0–S6 与客户端 OTA 层已落地（2026-10-19），S7 真机链路待验**。落地记录、验证证据与新旧场景名对照见 §10。
> 适用对象：能够读取仓库、运行 Unity Editor/构建菜单、执行验证命令的自动化 AI 工作流。  
> 本文是执行规格，不是建议列表；执行代理必须按阶段闸门推进，闸门未通过时停止，不得跨阶段猜测修复。

## 1. 已确认决策

### 1.1 资源与代码方案

本项目继续使用：

```text
代码热更       HybridCLR + HotUpdateLoader
资源运行时     QFramework ResKit AssetBundle
场景加载       ResKit 场景 AssetBundle
内容获取       可替换 HTTP 后端
首期服务       局域网临时静态文件服务器
未来服务       CDN HTTP/HTTPS 接口
```

不迁移 YooAsset。ResKit 已经能够加载当前项目的 Prefab、AssetBundle 和场景；本任务要补的是版本清单、下载、校验、缓存和后端切换层，而不是替换资源运行时。

### 1.2 场景迁移范围

第一阶段只迁移原 `2MainScene`，目标名称为 `scene_0002_main`。它必须作为可独立替换的场景 AssetBundle 由 ResKit 加载，不能再通过 Build Settings 中的普通场景路径加载。

所有场景统一采用以下命名规则，重命名是独立的资产迁移步骤，不能与场景加载代码改造混在同一个提交：

| 当前场景 | 目标场景名 | 第一阶段是否迁移 |
|---|---|---|
| `0ConfigScene` | `scene_0000_config` | 否，继续作为 AOT 入口 |
| `1LoginScene` | `scene_0001_login` | 否，继续走当前入口链 |
| `2MainScene` | `scene_0002_main` | **是** |
| `3GachaScene` | `scene_0003_gacha` | 否 |
| `BattleGraybox` | `scene_0004_battle_graybox` | 否 |
| `4BattleScene` | `scene_0005_battle_legacy` | 否 |
| `5BattleScene-Test0` | `scene_0006_battle_test0` | 否 |
| `5BattleScene-Test1` | `scene_0007_battle_test1` | 否 |

重命名后，所有场景 key、代码常量、场景 AssetBundle 资产名、Build Settings 和文档引用必须同步；不能只改文件名。

### 1.3 当前包体事实

当前 Android 包仍然包含基线 `HotUpdate` DLL 和基线 ResKit AssetBundle。它们随包存在是当前实现事实，不代表 OTA 链路已经完成。后续排查时必须先记录“运行的是包内基线还是远端覆盖版本”，再判断代码或资源是否生效。

## 2. 目标运行链路

```text
scene_0000_config（Build Settings，AOT）
  ├─ HotUpdateLoader.LoadAll()
  ├─ ResKit.InitAsync()
  └─ SceneFlow 加载 scene_0001_login
       └─ 登录完成
            └─ SceneFlow 通过 ResKit 加载 scene_0002_main
                 ├─ main_scene_0002_main AssetBundle
                 ├─ 场景内菜单对象
                 └─ Main_MenuPanel 等 UI AssetBundle
```

场景热更新不是把 DLL 直接塞进场景，而是让远端提供新的场景 AssetBundle；代码 DLL 和场景资源通过同一份版本清单协调，但仍由不同加载器处理。

## 3. 后端抽象边界

执行代理必须先建立后端接口，再实现局域网版本。ResKit 不负责 HTTP 下载，也不应直接访问 CDN。

建议抽象为以下职责（名称可按现有代码风格调整，但职责不可合并）：

```text
IHotUpdateContentBackend
  GetManifestAsync(channel, platform)
  DownloadFileAsync(fileId, destination, progress)

HotUpdateManifestService
  解析版本清单
  比较本地与远端版本
  计算需要更新的文件

HotUpdateCacheService
  管理 persistentDataPath/HotUpdate 与 AssetBundles 缓存
  原子写入临时文件后改名
  校验 hash/size
  清理不再引用的版本

ResKitContentLocator
  将已验证的本地缓存路径交给 ResKit
```

第一期实现：

```text
LocalHttpBackend → http://局域网地址/{channel}/{platform}/{file}
```

未来 CDN 只替换 `IHotUpdateContentBackend` 实现，清单格式、缓存目录、ResKit 加载代码和场景 key 不变。

禁止执行代理把 `UnityWebRequest` 调用散落到 `SceneFlow`、面板或业务脚本中；下载必须集中在后端/缓存层。

## 4. 清单格式与版本规则

清单至少包含：

```json
{
  "schemaVersion": 1,
  "contentVersion": "0.1.0",
  "channel": "lan",
  "platform": "Android",
  "files": [
    {
      "id": "code/hotfix",
      "kind": "hotUpdateDll",
      "path": "HotUpdate/TeaGame.Menu.dll",
      "size": 32768,
      "sha256": "...",
      "required": true
    },
    {
      "id": "scene/scene_0002_main",
      "kind": "assetBundle",
      "path": "AssetBundles/Android/main_scene_0002_main",
      "size": 123456,
      "sha256": "...",
      "required": true
    }
  ]
}
```

规则：

1. `schemaVersion` 变更必须兼容读取或明确拒绝；不能静默按旧格式解析。
2. 文件先下载到 `.part`，校验通过后原子替换；失败不得覆盖当前可用版本。
3. 清单、DLL、AssetBundle 必须支持同一 `contentVersion` 绑定，避免代码和场景跨版本组合。
4. 首包可以携带基线文件；远端版本只有在完整校验通过后才覆盖基线。
5. 回滚至少保留上一个完整版本目录。

## 5. 场景 AssetBundle 迁移步骤

### S0 · 只读基线

执行代理先记录：

- `2MainScene.unity` 当前路径、GUID、场景名；
- 场景中所有 `m_Script` GUID 及对应程序集；
- 当前 Build Settings 顺序；
- 当前 ResKit AssetBundle 清单；
- `SceneFlow`、`LoginSystem`、`BattleSceneGate` 的场景请求入口；
- PC SimulationMode 与 Android 真 AB 的现状。

退出条件：能够列出所有会请求原 `2MainScene` 的代码位置，且工作区无业务改动。

### S1 · 场景统一命名设计

先生成“旧名 → 新名”报告，不立即改资产。报告必须覆盖：

- `.unity` 文件名；
- 场景内部名称；
- Build Settings；
- `SceneRequest.SceneName` 字符串；
- `SceneAnchor`/登录/战斗回跳常量；
- 文档和验收脚本；
- 场景 AssetBundle 资产名。

退出条件：报告中没有未决的旧名称引用。命名迁移独立提交。

### S2 · 标记 `scene_0002_main` 场景 AB

为 `scene_0002_main.unity` 设置明确 Bundle 名：

```text
main_scene_0002_main
```

执行代理必须使用 Editor API 或现有 ResKit 构建菜单写入标记，不能直接手改生成的 Bundle 文件。构建后回读 `asset_bundle_config.bin`，确认存在：

```text
main_scene_0002_main / scene_0002_main
```

退出条件：PC SimulationMode 和 Android 真 AB 都能解析该场景资源。

### S3 · 从 Build Settings 移除主菜单场景

`scene_0000_config` 保持 Build Settings 第 0 项。第一阶段 `scene_0001_login` 仍保留在 Build Settings；`scene_0002_main` 从 Build Settings 移除，避免同一场景有两条加载路径。

退出条件：没有任何运行时代码依赖 `SceneManager.LoadSceneAsync("scene_0002_main")` 作为主菜单入口。

### S4 · 扩展场景请求模型

在 `TeaGame.App` 的 AOT 层增加场景来源字段：

```text
BuildSettings
AssetBundle
```

场景请求至少携带：

```text
SceneName
Source
AssetBundleName
ShowLoading
KeepPrevious
UnloadFirst
Payload
```

禁止在 `SceneFlow` 中写死 `if (sceneName == "scene_0002_main")`。

### S5 · 改造 SceneFlow 的 ResKit 场景加载分支

AssetBundle 分支必须使用 ResKit 的场景接口：

```text
ResLoader.Allocate()
LoadSceneAsync(bundleName, sceneName, Additive, callback)
等待 AsyncOperation
allowSceneActivation = false
等待 progress >= 0.9
激活并 SetActiveScene
卸载旧玩法场景
保留新场景 ResLoader
```

`ResLoader` 生命周期必须覆盖场景生命周期。新场景成功激活前，不能回收新 loader；旧场景卸载完成后，才能回收旧 loader。

失败时必须输出明确日志：

```text
[SceneFlow] AssetBundle 场景请求
[SceneFlow] bundle 命中/未命中
[SceneFlow] scene asset 命中/未命中
[SceneFlow] 加载失败原因
```

### S6 · 修改登录入口

登录成功后必须构造 AssetBundle 来源的 `SceneRequest`，不能只传场景名。主菜单路径应使用统一 AOT 场景目录常量，避免 `TeaGame.App` 引用 `TeaGame.Menu` 类型。

### S7 · 重建与 OTA 覆盖

每次修改 `scene_0002_main` 或其依赖资产后：

1. 重建 Android ResKit AssetBundle；
2. 生成文件 size/hash；
3. 更新局域网服务器清单；
4. 启动 App；
5. 下载新场景 Bundle；
6. 清理旧场景缓存前先保留上一版本；
7. 进入主菜单确认新内容生效。

## 6. 热更 MonoBehaviour 的验证边界

场景改为 AssetBundle 后，不能直接假设热更脚本一定能恢复。必须分别验证：

1. `Main_Root` 是否能从 AssetBundle 场景恢复；
2. `MainMenu_Runtime` 是否能执行 `Start`；
3. 场景依赖的热更 Prefab 是否能加载；
4. `Main_MenuPanel` 是否能由 ResKit 打开；
5. 卸载并重新进入场景后脚本是否仍有效。

如果 AssetBundle 场景仍出现 `missing MonoBehaviour`，停止继续堆补丁，转入备用结构：

```text
scene_0002_main AssetBundle
└── AOT MainSceneHost
    └── 运行时加载热更 Menu Controller/Prefab
```

这不是本阶段默认方案，只有 S6 真机验证失败时才启用。

## 7. 局域网后台验收

自动化工作流应提供一个最小静态 HTTP 服务，不引入数据库或账号系统：

```text
/lan/android/manifest.json
/lan/android/HotUpdate/*.dll
/lan/android/AssetBundles/*.bundle
```

验收顺序：

1. 首包无网络时使用包内基线正常启动；
2. 局域网可达时获取 manifest；
3. 只替换 `scene_0002_main` Bundle，主菜单内容改变；
4. 只替换一个热更 DLL，代码行为改变；
5. 下载中断后重启，`.part` 不被当成可用文件；
6. hash 错误时保留旧版本并打印失败原因；
7. 切换 CDN URL 后，不改 SceneFlow 和 ResKit 调用代码即可重复同一验收。

### 7.1 落地记录：静态服务 + 控制台已就绪（2026-10-19）

服务本体已实现于 **`web/LanOtaServer/`**（Node，零第三方依赖，`start-lan-ota.cmd` 一键启动，默认 `0.0.0.0:8080`）。
双击 `.cmd` 不再只是「跑一个黑窗口就退出」：服务起来后自动打开**前端控制台**，窗口保持打开，日志同时进页面与命令行。

**控制台（给人看，本机可写、局域网只读）**

| 路由 | 内容 |
|---|---|
| `GET /` | 控制台页面（`public/index.html` + `app.js` + `styles.css`，零依赖、无 CDN） |
| `GET /api/health` | root / rootState / 期望结构 / 真机地址 / channel 概况 / 文件计数 / SSE 客户端数 |
| `GET /api/tree` | 内容树：platform → `manifest.json` 解析结果 + HotUpdate / AssetBundles 文件 |
| `GET /api/logs` | 环形缓冲历史日志（500 条）与最近请求（200 条） |
| `GET /api/stream` | SSE：首帧 `snapshot` 全量，之后 `log` / `request` / `status` 增量 + 15s 心跳，断线自动重连 |
| `POST /api/root`、`POST /api/root/skeleton` | 运行时切换内容根目录 / 按约定结构建空目录（**仅 127.0.0.1 / ::1**，局域网来源 `403`） |

页面区块：顶部状态条（SSE 连接、运行时长、端口）、内容目录（未就绪标红 + 一键建骨架）、真机取用地址（可复制）、
内容树（文件可直接点开下载）、实时日志（级别过滤 / 自动滚动 / 清空）、最近请求（状态码着色，默认隐藏控制台自身轮询）。

**内容下发（给客户端）**

| 路由 | 内容 |
|---|---|
| `GET /<channel>/<platform>/manifest.json` | 版本清单，`application/json` + `no-store`，字节原样下发 |
| `GET /<channel>/<platform>/HotUpdate/<file>` | 热更 DLL / `hotfix.json` |
| `GET /<channel>/<platform>/AssetBundles/<file>` | ResKit AssetBundle |
| `GET /<channel>/<platform>/` | 纯 HTML 目录列表页（无 JS 环境兜底） |

- 内容根目录 `web/LanOtaServer/content/<channel>/<platform>/`（`.gitignore` 忽略，由构建产物手工拷贝；AB 与 DLL **保持原始文件名，不加 `.bundle`**）。
- 白名单只放行上述三类路径；路径穿越 / 反斜杠 / `%2e%2e` / 越出 root 一律 `403`，未知路径 `404`，非 `GET`/`HEAD` `405`。
- **root 缺失不再阻止启动**：服务照常起来、控制台照常可用，内容路由返回 `503` 并在响应体与控制台页面上写明期望结构与处理方式——避免双击即退、看不到任何可操作信息。
- 本服务**只做下发**：不生成 / 不改写清单，不实现 Range 断点续传与鉴权。清单的 `size` / `sha256` 由构建侧提供。
- 已实测（临时 fixture，两套共 **52 项断言全通过**）：
  - 控制台 34 项：root 缺失时 `/api/health` 仍 200 且 `rootState.ok=false`、内容路由 `503`、`/api/tree` 不抛错、页面与静态资源 200、建骨架真实落盘（3 个目录）、切换 root 后 manifest/DLL/20 MB AB 的 `sha256` 与磁盘一致、内容树读到 `contentVersion`、穿越仍 `403`、**非本机来源写操作 `403`**、SSE 四类事件（`snapshot`/`log`/`request`/`status`）均收到。
  - 下发 18 项：`HEAD` 无 body、缺失文件/白名单外/未知 channel 的 `404`、platform 大小写不敏感、内容路由 `POST 405`、4 种穿越写法 `403`、列表页、`/api/logs` 字段完整、前端 26 处 `el(id)` 引用与 `index.html` 完全对应。
- 未覆盖：验收项 1、5、6 属客户端 OTA 层（§3 的 `IHotUpdateContentBackend` / 缓存 / 校验），随 S4–S7 实现后再验；控制台的**浏览器视觉呈现**由用户肉眼确认（自动断言只覆盖接口与静态资源可达）。

## 8. 自动化 AI 工作流纪律

执行代理必须遵守：

- 先读本文、09 交接日志、16 热更计划和 17 代码地图；
- 每次只推进一个 S 步骤；
- 资产改动必须通过 Unity Editor API/菜单完成，不能优先手改 Unity YAML；
- 代码、场景命名、AB 标记、构建产物分开提交；
- 每一步输出改动文件、验证命令、日志证据和回滚点；
- 编译或真机验证失败时停在当前步骤，不得把失败状态写成完成；
- 不迁移 YooAsset，不同时引入第二套资源加载 API；
- 不把 HTTP 下载逻辑写入业务面板或 `SceneFlow`；
- 不删除包内基线热更文件，直到 OTA 覆盖和回滚验收完成。

## 9. 完成定义

本专项只有同时满足以下条件才算完成：

1. 所有场景采用 `scene_0000_xxx` 命名并完成引用迁移；
2. `scene_0002_main` 不在 Build Settings，能由 ResKit 场景 AB 加载；
3. `0ConfigScene` 仍是 AOT 入口，HybridCLR 日志为 9/9、6/6；
4. PC SimulationMode 与 Android 真 AB 链路一致；
5. 局域网 manifest 能覆盖主菜单场景资源；
6. 断点、hash 错误、回滚和无网络基线均有验证证据；
7. CDN 只需替换后端地址/实现，不需改场景加载和业务代码；
8. Docs/09、Docs/16、Docs/99 与本文状态同步。

---

## 10. 落地记录（2026-10-19）

### 10.1 提交

| 提交 | 范围 | 说明 |
|---|---|---|
| `99073a2e` | S1 命名迁移 | 8 个场景经 `AssetDatabase.MoveAsset` 改名，GUID/`.meta` 保留，Build Settings 路径与顺序自动同步；两处场景内序列化字段经 Editor 改写 |
| `c25489cb` | S2 场景 AB | `main_scene_0002_main` 标记完成，SimulationMode 与 Android 真 AB 双向回读通过 |
| `2888b054` | S4 请求模型 | `SceneSource` + `SceneRequest.Source/AssetBundleName` + `SceneCatalog`（AOT 场景目录） |
| `3f492980` | S5 加载通路 | `SceneFlow` 增加 ResKit 场景 AB 分支 + `ResLoader` 生命周期管理 |
| `86c7fb93` | S6 登录入口 | 经 `SceneCatalog.CreateRequest` 构造 AB 来源请求 |
| `8246519f` | S3 移除 Build Settings | 同时新增 `SceneCatalog.CreateRequest` 并接管 `BattleSceneGate` / `AppBootstrapper` 的请求构造 |
| `f2b61977` | OTA 客户端层 | `IHotUpdateContentBackend` / `LocalHttpBackend` / `HotUpdateManifestService` / `HotUpdateCacheService` / `ResKitContentLocator` / `HotUpdateOtaService` |

### 10.2 与规格的偏差（均为必要修正）

1. **S3 排在 S4–S6 之后执行**。S3 的退出条件是「没有运行时代码依赖 `SceneManager.LoadSceneAsync("scene_0002_main")` 作为主菜单入口」，而这条在 S5/S6 落地前不可能满足；先移除 Build Settings 会立刻打断登录→主菜单链路。
2. **新增 `SceneCatalog`（规格只要求「统一 AOT 场景目录常量」）**。移除 Build Settings 后暴露出一个连带风险：战斗结束回主菜单走 `BattleTeamSession.ReturnSceneName`，若仍只传场景名就会加载失败。因此把「场景 → 通路」的判定收进场景目录，`BattleSceneGate`、`AppBootstrapper`、`LoginSystem` 全部改为经 `SceneCatalog.CreateRequest` 构造请求，而不是各自 `new SceneRequest`。
3. **清单增加 `abSetComplete` 字段**。§4 的「至少包含」允许扩展。没有它就无法同时支持「只替换一个 bundle」（保持读包内基线清单 + persistent 优先逐文件覆盖）与「整套替换」（切 `persistent`）。两种语义混用会直接让 App 缺资源。
4. **ResKit 侧零改动**。`AssetBundleSettings.AssetBundleName2Url` 与 `HotUpdateLoader.ReadBytes` 本来就是「persistent 优先、逐文件回退 StreamingAssets」，所以 OTA 落点天然就是这两个 persistent 目录；`ResKitContentLocator` 只在 `abSetComplete=true` 且清单文件确实存在时才动 `LoadAssetResFromStreamingAssetsPath`。

### 10.3 验证证据

**编辑器 SimulationMode（Play）**

- 改名后引导链正常：`scene_0000_config` → `scene_0001_login`，`SceneFlow.Instance` 可用。
- 主菜单 AB 加载：日志链 `[LoginSystem] 主菜单走场景 AssetBundle` → `[SceneFlow] AssetBundle 场景请求` → `scene asset 命中` → `[SceneFlow] 场景切换完成 -> scene_0002_main`。
- 热更 MonoBehaviour 恢复（§6 四项）：`Main_Root`、`MainMenu_Runtime` 均解析为真实类型（无 missing MonoBehaviour）；`MainMenu_Runtime.Start` 执行并拉起 `Main_MenuPanel`（`State=Opening`）；`UIRoot` 解析到新场景。
- 移出 Build Settings 后再次验证，并做战斗往返：`main → scene_0004_battle_graybox(BuildSettings) → main(AssetBundle)` 成功；`[SceneFlow] 已回收场景 ResLoader -> scene_0002_main` 说明旧 loader 在新场景激活、旧场景卸载后才回收；重进场景脚本仍有效。尾部无 NullReference / MissingReference / `error CS`。
- **这是 SimulationMode（AssetDatabase 实时清单），不等于真 AB 链路。**

**Android 真 AB（仅清单层）**

`Thrud/Demo/Android/2. Build AssetBundles (Android)` 构建后 `ResDatas.LoadFromFile` 回读 `asset_bundle_config.bin`：命中 `main_scene_0002_main / scene_0002_main`，`AssetType=2(ABScene)`，既有 `main_menupanel_prefab` 等编目未受影响。**运行期真 AB 未验。**

**OTA 客户端（真实 HTTP，对接现存 `web/LanOtaServer`）**

| 验收项 | 结果 |
|---|---|
| 2 局域网可达取 manifest | ✅ 200，解析出 `contentVersion` 与 3 个条目 |
| 3 只替换一个 bundle | ✅ 单 bundle 落到 `persistentDataPath/AssetBundles/Android/`，状态与备份正确 |
| 4 只替换一个热更 DLL | ✅ `TeaGame.Data.dll` 与 `hotfix.json` 落到 `persistentDataPath/HotUpdate/` |
| 5 `.part` 不当可用文件 | ✅ 下载失败即删；上次中断残留的 `.part` 在下次启动被 `CleanupStaleParts` 清掉 |
| 6 hash 错误保留旧版本 | ✅ 篡改清单 `sha256` 后：下载完成 → 校验失败 → 整次作废 → 磁盘文件与 `ota-state.json` 均未变 |
| 幂等 | ✅ 相同 `contentVersion` 重启直接「已是最新」跳过 |
| 回滚 | ✅ 成功更新后 `previousContentVersion` 与 `_backup/<旧版本>/` 生成，`RollbackToPrevious` 还原文件并把 `contentVersion` 退回旧值 |
| 1 无网络基线启动 | ✅ OTA 默认 `enabled=false`；任何网络/校验/解析失败都不写状态、不阻断启动 |

### 10.4 待验 / 下一步（S7）

1. **真机真 AB 场景加载**：SimulationMode 走 `EditorSceneManager.LoadSceneAsyncInPlayMode`，真机走 `SceneManager.LoadSceneAsync`（依赖已加载 bundle 注册场景名）。需出包确认 `scene_0002_main` 在不在 Build Settings 都能起。
2. **真实场景 bundle 的 OTA 替换**（本次用 16 KB 的 `main_formationpanel_prefab` 作探针，未用 56 MB 的 `main_scene_0002_main` 走完整流程）：替换后需确认主菜单内容真的发生变化，且 `missing MonoBehaviour` 不出现。
3. **`allowSceneActivation=false` 在真机上是否生效**：SimulationMode 下已观察过渡时序正常，真机需看日志确认「预加载 0.9 → 保持 → 激活」仍然成立。
4. **整包替换模式**（`abSetComplete=true`）：代码支持但未跑通，需一份包含全部 bundle + `asset_bundle_config.bin` 的完整清单。
5. HybridCLR 日志 9/9、6/6 的回归（真机）。

### 10.5 旧名 → 新名对照

| 旧名 | 新名 | 备注 |
|---|---|---|
| `0ConfigScene` | `scene_0000_config` | Build Settings[0]，AOT 入口 |
| `1LoginScene` | `scene_0001_login` | Build Settings 保留 |
| `2MainScene` | `scene_0002_main` | **唯一走场景 AssetBundle**，已移出 Build Settings |
| `3GachaScene` | `scene_0003_gacha` | disabled |
| `BattleGraybox` | `scene_0004_battle_graybox` | Build Settings 保留 |
| `4BattleScene` | `scene_0005_battle_legacy` | disabled |
| `5BattleScene-Test0` | `scene_0006_battle_test0` | disabled |
| `5BattleScene-Test1` | `scene_0007_battle_test1` | disabled |

**文档里的旧名未批量替换**：历史日结/存档按当时事实保留，操作类文档按本表换算。代码、资产、Build Settings 已无旧名字面量（仅注释中仍以旧名指代场景，属阅读性表述）。

### 10.6 追加：内容发布工具 + 登录进度条（同日）

| 提交 | 内容 |
|---|---|
| `d7ee2f61` | 登录场景资源更新进度条 + OTA 拆成「启动应用缓存」/「登录联网检查」 |
| `5ed6eb70` | `Thrud/HotUpdate/3. 发布 OTA 内容到局域网服务目录`（镜像产物 + 生成 `manifest.json`） |
| `f6fd1583` | 重新 `CompileDll` 并导出热更 DLL 基线（S1/S3–S6 改过 `TeaGame.Battle.Contracts`） |
| `b9f6fc6c` | 补提交 S3 遗漏的 `EditorBuildSettings.asset`（Unity 在提交前重新序列化覆盖了暂存） |
| `298d71de` | `.gitignore` 忽略 `/Screenshots/` |

**4.1 一条设计变更：OTA 从「启动时联网」改为「登录时联网」**

原实现把联网检查放在 `AppBootstrapper.Start()`，结果是「启动变慢 + 进度不可见」。现在拆成两段：

- `HotUpdateOtaService.ApplyCachedContentAtBoot()`：启动时**不联网**，只清残留 `.part`、读缓存状态、
  决定 ResKit 读哪套 AB 清单。位置仍在 `HotUpdateLoader.LoadAll()` / `ResKit.InitAsync()` 之前（落点顺序不能反）。
- `HotUpdateOtaService.RunCheck()`：由登录场景触发，负责取清单、比对、下载、校验、提交状态，
  过程中持续写 `Status`（Stage/Message/Progress/版本/文件数/Error），进度**按字节加权**。

这不只是 UI 需要，也符合既有分工：`LoginSystem.StartHotUpdateCheck` 本来就是「登录场景承担热更检查」的占位。
代价写清楚：登录阶段下到的新 AB **清单**要下次启动才生效（`ResKit.InitAsync` 启动时已定型），
但**单个 bundle 是逐文件解析的**，本会话后续场景加载即刻命中。

**4.2 发布工具**

`Assets/02_Scripts/Tools/Editor/OtaContentPublisher.cs`：把 `StreamingAssets/AssetBundles/<Platform>/` 与
`StreamingAssets/HotUpdate/` 镜像到 `web/LanOtaServer/content/<channel>/<platform>/`（目标目录先清空），
逐文件算 size/sha256，写 `schemaVersion=1` 清单，`contentVersion = bundleVersion+时间戳`，发布完整集合时
`abSetComplete=true`。跳过 `.meta` / `*.manifest` / 与平台同名的 manifest bundle。
实测：28 个文件 / 154.9 MB（AB 149.8 MB），抽查 `asset_bundle_config.bin` 与 `TeaGame.Battle.Contracts.dll`
的 size+sha256 与清单一致；服务端 `manifest.json`、`HotUpdate/hotfix.json`、`AssetBundles/main_scene_0002_main` 全 200。

**4.3 登录场景进度条**

`scene_0001_login` 的 `MainCanvas` 下新增 `OTA_ResourcePanel`：状态文案、版本对比（本地 → 服务器）、进度条，
挂 `LoginOtaProgressView`。它只读 `HotUpdateOtaService.Status`，不含网络/下载代码，也不参与登录判定
（下载失败照样能开始游戏）。填充条用 `pivot.x=0` + 拉伸锚点，脚本只改 `localScale.x`。

实测（编辑器 + 真实局域网 HTTP）：登录场景自动检查 → 下载 25 个文件期间 `Status.Progress` 到 `0.982`、
文案显示 `14/25` → 完成后状态文案为「资源已更新到 alpha.0.50+…」、版本行本地与服务器同版本；
注入 `progress=0.42` 后 `fill.localScale.x` 精确为 `0.420`（482/1148 px）。截图见仓库根 `Screenshots/`（已忽略）。

**4.4 一条必须记住的边界**

本轮改动含 **`TeaGame.App`（AOT）内的新类型**（`SceneCatalog`、`HotUpdateOtaService`、`LoginOtaProgressView` 等），
AOT 变更**无法热更**：`StreamingAssets/HotUpdate/` 里重导出的 6 个热更 DLL 只覆盖热更程序集，
真机要生效必须**重新出 APK**。别把「导出了热更 DLL」当成「新代码上机了」。

**4.5 §10.4 待验清单的状态更新**

已由本轮解决：局域网 manifest 能覆盖主菜单场景资源（发布工具 + 真实下载 25 文件通过）；
「只替换一个 bundle / 一个 DLL」与整套替换（`abSetComplete=true`）两条路都跑通。

仍未验（需出包）：真机真 AB 下 `scene_0002_main` 加载；`allowSceneActivation=false` 真机是否生效；
HybridCLR 9/9、6/6 真机回归；AOT 新代码随新 APK 生效后的完整回归。

### 10.7 追加：下载可暂停/停止 + 只热更业务程序集

| 提交 | 内容 |
|---|---|
| `031eaaf4` | 下载期间锁住「开始游戏」+ 暂停/停止下载；修复编辑器里 OTA 被误判为未启用 |
| `9406e63a` | OTA 发布只下发业务热更程序集，剔除 AOT 补充元数据 DLL 并加硬校验 |

**7.1 下载期间的可交互面**

`running`（Checking / Downloading / Paused）期间：面板只显示「暂停下载」（切换式，暂停后变「继续下载」）与
「停止下载」；`开始游戏` 的 `TeaBaseButton` 组件被 `enabled=false` 并置灰。
**用组件禁用而不是 CanvasGroup**：`TeaBaseButton` 自己在 `LateUpdate` 里驱动 alpha，
外部改 alpha 会被它覆盖；禁用组件同时停掉 `IPointerClickHandler`。
`LoginViewController.OnStartButtonClicked` 里还有一层运行时拦截，防 UI 状态落后一帧。

**7.2 暂停/停止的真实语义（重要）**

服务端不支持 Range（无断点续传），已传的半个文件无法复用，因此：

- **暂停** = 中断当前传输 → 删 `.part` → 挂起（进度冻结）→ 恢复时**该文件从头重下**；
- **停止** = 中断 → 删 `.part` → 整轮结束 → **不写状态**（新版本不生效，旧文件仍由上一份状态兜住）。

`IHotUpdateContentBackend.DownloadFileAsync` 新增 `shouldAbort` 回调，传输层只负责中断，
「这是用户暂停还是真失败」由调用方根据自己置的标记判断——这层语义不能塞进传输层。

**7.3 编辑器里「OTA 未启用」的真正原因（排查记录）**

现象：`ota.enabled` 明明是开的，日志却打印 `[HotUpdate] OTA 未启用或未配置服务器地址`，且 `Status.Stage` 停在 `Idle`。

根因是**启动顺序竞争**，不只是默认值：编辑器从 `scene_0001_login` 进 Play 时，ConfigScene 是之后被
`Editor_ConfigSceneGuard` 拉起的，于是登录场景的 `Start()` **早于** `AppBootstrapper.Start()`，
`RunCheck` 拿到的 `Settings` 还是 `null` → 直接判定未启用 → 随后 `ApplyCachedContentAtBoot` 又把 `Status` 重置成 `Idle`。

修复：`RunCheck` 先等 `sBootApplied`（上限 5s）再读配置；`ApplyCachedContentAtBoot` 只在 `Stage==Idle` 时写文案，
不再无条件改写状态；未启用时日志打印 `enabled` / `baseUrl` 与配置位置。
同时把 `scene_0000_config` 的 `AppBootstrapper.ota` **保存为启用**（`baseUrl=http://127.0.0.1:8080`、`channel=lan`），
编辑器开箱即可测。

**7.4 哪些 DLL 会热更（回答「HotUpdate 目录里为什么有 QFramework/DOTween」）**

`Assets/StreamingAssets/HotUpdate/` 下有两类东西，容易混：

| 类别 | 例子 | 是否热更 | 用途 |
|---|---|---|---|
| `hotfix.json` 的 `hotUpdate`（6 个） | `TeaGame.Menu.dll`、`TeaGame.Battle.dll`… | **是**，`Assembly.Load` | 业务代码热更 |
| `hotfix.json` 的 `aotMetadata`（9 个） | `mscorlib.dll`、`System.Core.dll`、`QFramework.dll`、`DOTween.dll`、`UIKit.dll`、`ResKit.dll`、`QFramework.CoreKit.dll`、`UnityEngine.CoreModule.dll`、`TeaGame.App.dll` | **否**，只 `RuntimeApi.LoadMetadataForAOTAssembly` | 给 AOT 泛型补元数据 |

`HybridCLR/Settings` 里 `hotUpdateAssemblyDefinitions` 只登记了 6 个 TeaGame asmdef、`hotUpdateAssemblies: []`，
所以不存在插件被热更的情况。但两者混在同一目录确实容易误解，因此发布侧做了两件事：

1. **AOT 元数据不进 OTA 清单**（`9406e63a`）：它们与已安装 APK 版本强耦合，通过 OTA 换掉会让元数据与 AOT 代码对不上。
   改为只随 APK 走；客户端读 `hotfix.json` 的 `aotMetadata` 时**逐文件回退到 StreamingAssets** 命中。
   发布后清单从 28 → 19 个文件，持久层 `HotUpdate` 目录只剩 `hotfix.json` + 6 个 `TeaGame.*.dll`。
2. **硬校验**：`hotfix.json` 的 `hotUpdate` 只允许 `TeaGame.*` 且不得含 `TeaGame.App`，否则发布直接失败并指向
   `HybridCLR/Settings` 的 `hotUpdateAssemblyDefinitions`。「只热更自己写的业务程序集」不再靠人记。

**7.5 本轮验证**

用 258 MB 探针文件 + 一个 run_script 装的进度驱动（跨过阈值自动触发）稳定复现，不靠人肉抢时间：

| 项 | 结果 |
|---|---|
| 下载中 UI 可交互面 | 暂停按钮可见、`startButton.enabled=false` |
| 暂停 | 25.1% 暂停 → `stage=Paused`、进度 1.2s 内完全冻结、文案变「继续下载」、开始仍锁定 |
| 继续 | 点「继续下载」→ 该文件从头重下并完成；完成后开始按钮解锁、按钮隐藏 |
| 停止 | 30.2% 停止 → `stage=Stopped`、开始立即解锁、`ota-state.json` 仍是旧版本、无残留 `.part` |
| 清理 | 重发布后再跑一轮，持久层探针被 `DeleteFilesNotIn` 回收 |
| 发布裁剪 | 清单 19 文件（12 AB + 1 hotfix.json + 6 热更 DLL），日志列出被跳过的 9 个 AOT DLL |

全程无 exception / `error CS`。
