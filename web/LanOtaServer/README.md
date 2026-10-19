# Thrud 局域网 OTA 服务 + 控制台（`web/LanOtaServer`）

`Docs/03_专项设计与重构/23_ResKit场景热更新与局域网OTA_自动化执行指导.md` **§7 局域网后台**的第一期实现：
一个零依赖、离线可用的静态 HTTP 服务，把磁盘上已有的**版本清单 + 热更 DLL + ResKit AssetBundle**按约定路径发出去，
并自带一个**前端控制台网页**用来看日志与状态（双击 `.cmd` 即开，不用翻控制台历史，也不用开 Unity）。

## 快速开始

双击 `start-lan-ota.cmd`（等价于 `npm start`）：

1. 起服务，默认监听 **`0.0.0.0:8080`**；
2. 自动打开控制台 <http://127.0.0.1:8080/>；
3. **窗口保持打开**，日志同时进控制台与命令行；`Ctrl+C` 或关窗即停止。

页面分 5 块：

| 区块 | 内容 |
|---|---|
| 顶部状态条 | SSE 连接状态（实时连接 / 轮询兜底）、已运行时长、端口 |
| 内容目录 | root、清单 `contentVersion`、可下发文件数、channel/platform；未就绪时标红，并就地给出「创建空目录骨架」按钮 |
| 真机取用地址 | 本机所有局域网 IPv4 + 完整清单 URL + 一键复制 |
| 内容树 | `channel/platform → HotUpdate / AssetBundles`，含文件大小与 `manifest.json` 解析结果，文件可直接点开下载 |
| 实时日志 + 最近请求 | SSE 推流（`snapshot/log/request/status`）、级别过滤、自动滚动、清空；请求表带状态码着色，默认隐藏控制台自身的轮询 |

**目录缺失不再一启动就退出**：服务照样起来，控制台照常可用，内容路由返回 `503` 并在页面上写明缺什么、怎么补。
这是刻意的——双击 cmd 只闪一行报错就退出，比报错本身更浪费时间。

## 内容目录

```text
web/LanOtaServer/content/            ← 目录整体被 .gitignore 忽略，是构建产物副本
└── lan/                             ← channel
    └── android/                     ← platform（大小写不敏感，android / Android 都行）
        ├── manifest.json            ← 清单，字段见 23 文档 §4
        ├── HotUpdate/               ← 热更 DLL 与 hotfix.json
        │   ├── hotfix.json
        │   └── TeaGame.Menu.dll
        └── AssetBundles/            ← ResKit AssetBundle
            ├── main_menupanel_prefab
            └── main_scene_0002_main
```

页面上的「创建空目录骨架」只建这三个目录，**不生成任何内容或清单**。

两个必须注意的点：

1. **AB 与 DLL 的原始文件名一个字符都不能改**。23 文档 §7 里的 `*.bundle` 是示意写法，工程实际产物**没有扩展名**（`main_menupanel_prefab`、`asset_bundle_config.bin`…）。ResKit 按清单里的名字精确匹配，改名即「资源未命中」。
2. **清单里的 `path` 相对于平台目录**：`HotUpdate/TeaGame.Menu.dll`、`AssetBundles/Android/main_scene_0002_main` 都合法，只要磁盘上确实存在该文件——服务端只判「路径在平台目录内 + 命中白名单」，不校验清单内容。

内容来源（手工拷贝，本工具不代劳）：

| 内容 | 来源 |
|---|---|
| 热更 DLL + `hotfix.json` | `Assets/StreamingAssets/HotUpdate/`（`Thrud/HotUpdate/1` 导出的基线）或 `Library/HybridCLR/HotUpdateDlls/Android/` |
| AssetBundle | `Assets/StreamingAssets/AssetBundles/Android/` 或 `AssetBundles/Android/`（重建后的产物） |

拷贝时**跳过 `.meta` 文件**。

## 路由

### 内容（给客户端，局域网可达）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET / HEAD | `/<channel>/<platform>/manifest.json` | 版本清单，`application/json` + `no-store`，**字节原样下发** |
| GET / HEAD | `/<channel>/<platform>/HotUpdate/<file>` | 热更 DLL / `hotfix.json` |
| GET / HEAD | `/<channel>/<platform>/AssetBundles/<file>` | AssetBundle |
| GET | `/<channel>/<platform>/` | 纯 HTML 目录列表页（无 JS 环境兜底，也便于真机浏览器核对） |

状态码：`200` 正常；`HEAD` 只有响应头（`Content-Length` 为真实大小）；`403` 路径穿越 / 反斜杠 / NUL / 越出 root；
`404` 文件不存在、落在白名单外、或 channel/platform 不存在；`405` 非 `GET`/`HEAD`；**`503` 当前 root 不可用**。

### 控制台（页面 + API）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/`、`/app.js`、`/styles.css` | 控制台页面与静态资源 |
| GET | `/api/health` | 状态快照：root / rootState / 期望结构 / 真机地址 / channel 概况 / 文件计数 / SSE 客户端数 |
| GET | `/api/tree` | 内容树：platform → `manifest.json` 解析结果 + HotUpdate / AssetBundles 文件 |
| GET | `/api/logs` | 历史日志（环形缓冲 500 条）与最近请求（200 条） |
| GET | `/api/stream` | SSE：首帧 `snapshot` 全量，之后 `log` / `request` / `status` 增量 + 15s 心跳 |
| POST | `/api/root` | `{root, createSkeleton?}` 运行时切换内容根目录 |
| POST | `/api/root/skeleton` | `{root?}` 按约定结构创建空目录 |

> **写接口只允许本机发起**（`127.0.0.1` / `::1`）：服务必须绑定 `0.0.0.0` 才能让真机拉包，但局域网里的其他机器不该能改你的 root。
> 非本机来源的 `POST /api/*` 一律 `403`。

## 配置

| 项 | 命令行 | 环境变量 | 默认 |
|---|---|---|---|
| 端口 | `--port=8080` | `LAN_OTA_PORT` | `8080` |
| 绑定地址 | `--host=0.0.0.0` | `LAN_OTA_HOST` | `0.0.0.0`（真机可达） |
| 内容根目录 | `--root=<目录>` | `LAN_OTA_ROOT` | `./content` |
| 不自动开浏览器 | `--no-open` | — | 自动打开 |
| 关闭控制台页面 | `--no-index` | — | 开启 |

例：`start-lan-ota.cmd --port=8090 --root=D:\ota-content`

## 自检

```powershell
# 1) 状态：root 是否就绪、有几个 channel/platform、真机地址
Invoke-RestMethod http://127.0.0.1:8080/api/health | ConvertTo-Json -Depth 6

# 2) 控制台页面与静态资源
(Invoke-WebRequest http://127.0.0.1:8080/ -UseBasicParsing).StatusCode
(Invoke-WebRequest http://127.0.0.1:8080/app.js -UseBasicParsing).StatusCode

# 3) 包体下载后与本地源文件比 sha256（PS 5.1 / PS 7 都可用）
$tmp = Join-Path $env:TEMP "TeaGame.Menu.dll"
Invoke-WebRequest http://127.0.0.1:8080/lan/android/HotUpdate/TeaGame.Menu.dll -OutFile $tmp -UseBasicParsing
(Get-FileHash $tmp -Algorithm SHA256).Hash
(Get-FileHash web\LanOtaServer\content\lan\android\HotUpdate\TeaGame.Menu.dll -Algorithm SHA256).Hash
# 两行哈希必须相同；再与清单里该条的 sha256 字段对照

# 4) 穿越与白名单必须被拒（期望 403 / 404）
try { Invoke-WebRequest "http://127.0.0.1:8080/lan/android/../../server.js" -UseBasicParsing } catch { $_.Exception.Response.StatusCode }
try { Invoke-WebRequest "http://127.0.0.1:8080/lan/android/secret.txt" -UseBasicParsing } catch { $_.Exception.Response.StatusCode }

# 5) 目录骨架（等价于点页面上的按钮）
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:8080/api/root/skeleton -ContentType application/json -Body "{}"
```

清单里的 `size` / `sha256` 是**构建侧**写的，本服务不改写、不校验；比对方法：把来源文件自己算一遍 `sha256`，与清单字段对照。

## 职责边界

| 做 | 不做 |
|---|---|
| 按 `/<channel>/<platform>/…` 下发文件（`Content-Length` + `no-store`） | 生成 / 改写 `manifest.json`（构建侧提供） |
| 白名单 + 穿越/符号链接拦截 + 状态与日志控制台 | 断点续传（Range / 206）、鉴权、数据库、上传接口 |
| root 运行时切换与目录骨架（仅本机） | 任何 Unity / ResKit 侧行为；不读 `Assets/` |

客户端侧的后端抽象（`IHotUpdateContentBackend` / `LocalHttpBackend`）尚未实现，属于 23 文档 S4–S7 之后的工作，
所以**当前验收方式是浏览器 / `Invoke-WebRequest` / 真机浏览器**，不是「App 里点一下就更新」。

## 与验收条款（23 文档 §7）的对应

| §7 验收项 | 本服务覆盖情况 |
|---|---|
| 2 局域网可达时获取 manifest | ✅ 路由 + `no-store` + 真机地址列表 |
| 3 只替换 `scene_0002_main` Bundle | ✅ 换掉 `content/.../AssetBundles/` 里那一个文件即可 |
| 4 只替换一个热更 DLL | ✅ 同上 |
| 5 `.part` 不当作可用文件 | ⚠️ 客户端行为（服务端只保证字节完整下发） |
| 6 hash 错误保留旧版本 | ⚠️ 客户端行为 |
| 7 切 CDN 不改业务代码 | ✅ 服务端地址可换，客户端只需替换后端实现 |

1、5、6 属于客户端 OTA 层（23 文档 S4–S7）的验收，不在本工具范围内。
