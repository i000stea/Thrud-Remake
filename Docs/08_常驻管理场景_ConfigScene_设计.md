# 常驻管理场景（ConfigScene）设计

> 本文档定义工程的多场景常驻管理架构：独立的配置管理场景 `ConfigScene` 作为应用入口与常驻层，承担全局配置持有与全部场景切换管理。本文档由多轮对话确认的需求演化而来，决策编号 D15-D23。

## 背景与目标

原架构中，配置对象散落在各玩法场景（如 `BattlePlayerConfig` 依赖 `PlayerControl` 场景装配），场景切换各自为政，全局对象靠 `DontDestroyOnLoad` 维持。目标：

- 配置独立成对象、独立成场景，与玩法场景彻底解耦
- 应用启动时检查管理场景加载状态，未加载则用 additive 方式加载
- 后续所有场景切换均由该场景统一控制，玩法场景作为叠加层切换，管理场景永不卸载

## 架构总览（多场景常驻模式）

```
Build Settings:
[0] ConfigScene      ← 入口场景，常驻，永不卸载
[1] LoginScene       ← additive 加载
[2] MainScene        ← additive 加载（主城 2D 功能页）
[3] GachaScene       ← additive 加载
[4] BattleScene      ← additive 加载
[5] BattleGraybox    ← additive 加载（灰盒测试盒，替代 4BattleScene-Test0/1 或并列）
```

原则：**ConfigScene 之外的场景均为 additive 玩法场景，切换时旧玩法场景卸载、新场景叠加加载**；ConfigScene 从启动到退出始终存活。

## ConfigScene 承载内容

| 对象/组件 | 职责 | 来源 |
|-----------|------|------|
| `GameRoot` | 场景根对象，承载以下全部组件 | 新建 |
| `AppBootstrapper` | 启动编排：日志/埋点初始化 → 全局配置加载 → 全局架构初始化 → 初始化 SceneFlow → 加载首个玩法场景 | 新建 |
| `SceneFlow` | 场景切换控制器（请求对象模式，见下） | 新建 |
| `ConfigSceneGuard` | 懒加载守卫：防重复加载；编辑器单场景调试时自动 additive 加载 | 新建 |
| `UIRoot` | UIKit 根画布，跨场景常驻，所有模块 UI 面板叠加其上 | 由 UIKit 资源置入 |
| `LoadingGroup` | 全局加载遮罩实例（进度/转场） | 从 `Assets/Resources/Prefabs/LoadingGroup.prefab` 置入，替代运行时 DontDestroyOnLoad 实例化 |
| `StoreBattleRoleData` | 战斗角色/武器数据存储（去 DontDestroyOnLoad，由场景常驻保证存活） | 从旧单例迁入 |
| `BattleRoleRepository` | 战斗运行时数据仓库（同上） | 从旧对象迁入 |
| 全局配置引用集合 | `BattlePlayerConfig` 等 ScriptableObject 引用，统一挂载 | 各模块配置资产 |

迁入规则：所有原 `DontDestroyOnLoad` 全局对象迁入 ConfigScene 后**移除 `DontDestroyOnLoad` 调用**，避免"场景常驻 + 对象常驻"双重常驻产生重复实例。

## 启动流程

```
App 启动（Build Settings[0] = ConfigScene）
 ├─ AppBootstrapper.Awake
 │    ├─ 初始化日志/埋点（TelemetryLog，M0 后接入，先行 Debug 占位）
 │    ├─ 加载全局配置（挂载的 SO 引用）
 │    ├─ 初始化全局架构（AppArchitecture：注册全局 Model/System）
 │    └─ 初始化 SceneFlow
 ├─ AppBootstrapper.Start
 │    └─ SceneFlow.RequestLoad(Login)   ← 首个玩法场景 additive 加载
 └─ 此后所有场景切换均走 SceneFlow
```

全局架构（`AppArchitecture`）与模块架构（如 `BattleArchitecture`）分层：

- 全局架构在 ConfigScene 的 GameRoot 上初始化，持有跨模块共享状态（配置、场景流程、全局事件）
- 模块架构仍由各模块自己管理（战斗架构在战斗场景/战斗模块初始化），注册模块内 System/Model
- 模块架构可通过全局架构访问跨模块数据，方向单向：全局 → 模块，模块间不互引

## 懒加载守卫（ConfigSceneGuard）

用户需求的"检查该 scene 是否存在"落点为**检查是否已加载**：

```csharp
public sealed class ConfigSceneGuard : MonoBehaviour
{
    // 运行时防重复：任何路径尝试再次加载 ConfigScene 时，已加载则跳过
    public static bool IsLoaded()
    {
        return UnityEngine.SceneManagement.SceneManager
            .GetSceneByName(SceneNames.ConfigScene).IsValid();
    }

    // 编辑器守卫：直接 Play 玩法场景时自动 additive 加载 ConfigScene
#if UNITY_EDITOR
    [InitializeOnLoadMethod] 或 [RuntimeInitializeOnLoadMethod]
    // 检测当前活动场景不是 ConfigScene 且未加载 → LoadScene(additive) 并设为常驻
#endif
}
```

- 运行时正常路径：ConfigScene 是入口，必然已加载，守卫为恒真（防重复加载的保险）
- 编辑器路径：直接打开 `BattleGraybox` 等玩法场景点 Play → 守卫自动 additive 加载 ConfigScene，保证配置/SceneFlow 可用
- 场景名集中维护于 `SceneNames` 常量类，避免魔法字符串

## SceneFlow 设计（切换请求对象模式）

```csharp
public enum GameScene { Login, MainCity, Gacha, Battle, BattleGraybox }

public sealed class SceneRequest
{
    public GameScene Scene;
    public object Payload;          // 跨场景参数（如关卡 ID、编队数据、回跳目标）
    public bool ShowLoading = true; // 是否显示全局 Loading 遮罩
}

public sealed class SceneFlow : MonoBehaviour
{
    public void RequestLoad(SceneRequest request);
    public T GetPayload<T>();       // 目标场景在 OnSceneLoaded 时读取参数

    // 内部流程：请求 → LoadingGroup 遮罩 → additive 加载 → 设 active
    //        → 目标场景完成回调 → 卸载旧玩法场景 → 关闭遮罩
}
```

- **切换策略**：卸载旧玩法场景（除 ConfigScene 外全部卸载），additive 加载新玩法场景
- **active scene 管理**：加载完成后将新场景设为 active，避免 UI 事件/相机顺序错乱
- **参数传递**：`SceneRequest.Payload` 由 SceneFlow 暂存，目标场景通过 `GetPayload<T>()` 读取；同一时间仅一份请求
- **Loading 遮罩**：统一使用 ConfigScene 内 LoadingGroup 实例，替代现有 `LoadingControl` 的 `Resources.Load + DontDestroyOnLoad` 运行时实例化（`LoadingControl` 保留为旧场景过渡兼容，新代码一律走 SceneFlow）

## 与现有系统的衔接

| 现有元素 | 处理 |
|----------|------|
| `LoadingControl`（Before） | 过渡期保留；新流程由 ConfigScene 内 LoadingGroup 实例控制，旧场景逐步弃用 |
| `StoreBattleRoleData`（DontDestroyOnLoad 单例） | 迁入 ConfigScene，去 DontDestroyOnLoad；`Singleton` 基类保留访问能力 |
| `BattleRoleRepository`（DontDestroyOnLoad 可选） | 迁入 ConfigScene，去 DontDestroyOnLoad |
| `BattleArchitecture` + 7 System | 保持模块级，战斗场景加载时初始化；不迁移到 ConfigScene |
| `PlayerControl` 场景装配 | 保持现有职责（战斗场景内装配），不再承担跨场景管理 |
| UIKit | 接入后 UIRoot 放 ConfigScene，面板跨场景常驻 |

## 决策记录（追加 D15-D23）

| 编号 | 决策项 | 结论 |
|------|--------|------|
| D15 | 常驻管理场景 | 新建 ConfigScene 独立承载配置与场景切换，作为应用入口与常驻层 |
| D16 | 存在检查语义 | "检查是否已加载"（懒加载守卫，防重复 + 编辑器兜底） |
| D17 | 管理场景职责 | 配置对象 + 场景切换控制器（SceneFlow），不吞全局系统 |
| D18 | 旧单例去留 | StoreBattleRoleData / BattleRoleRepository 迁入 ConfigScene，去 DontDestroyOnLoad |
| D19 | 场景来源 | 先 Build Settings，热更接入后切 YooAsset 场景分组 |
| D20 | 切换策略 | 卸载旧玩法场景，ConfigScene 永驻 |
| D21 | Loading 遮罩 | LoadingGroup 预制件并入 ConfigScene 作全局遮罩 |
| D22 | UIRoot 归属 | 放管理场景（跨场景常驻） |
| D23 | 实施节奏 | 先固化文档，后续按依赖顺序实施 |

## 实施任务（后续按序执行）

| 任务 | 说明 | 依赖 | 状态 |
|------|------|------|------|
| C1 新建 ConfigScene + GameRoot 骨架 | 空场景 + GameRoot + AppBootstrapper 占位 | 无 | 代码/工具完成，场景文件待执行菜单生成 |
| C2 Build Settings 调整 | ConfigScene 置 [0]，其余保持 additive | C1 | 工具完成（菜单一键置 [0]） |
| C3 SceneFlow 实现 | 请求对象、加载/卸载/active 管理、LoadingGroup 接线 | C2 | 已完成（编译通过） |
| C4 迁移全局对象 | StoreBattleRoleData / BattleRoleRepository / LoadingGroup / UIRoot 入 ConfigScene | C3 | 待执行 |
| C5 编辑器守卫 | ConfigSceneGuard + SceneNames | C4 | 已完成（代码随 C1 落地） |
| C6 场景链路改造 | Login/Main/Gacha/Battle 切换改造为 additive 请求模式 | C3 | 待执行 |
| C7 架构文档同步 | 更新本文件与 01 架构文档到实际实现 | C6 | 待执行 |

### 执行记录（2026-09-01，C1-C3）

- 新增运行时代码 `Assets/02_Scripts/00_App/`：`GameScene.cs`、`SceneNames.cs`、`SceneRequest.cs`、`SceneFlow.cs`、`AppBootstrapper.cs`、`ConfigSceneGuard.cs`（命名空间 `TeaGame.App`）
- 新增编辑器工具 `Assets/02_Scripts/00_App/Editor/ConfigSceneCreator.cs`：菜单 **`Thrud/Setup ConfigScene`** 一键完成——新建 ConfigScene 空场景 → 创建 GameRoot（挂 AppBootstrapper + SceneFlow）→ 实例化 LoadingGroup 预制件并接给 SceneFlow → 保存场景 → 置入 Build Settings[0]
- **编译验证通过**（Unity Roslyn：Assembly-CSharp exit=0，Assembly-CSharp-Editor exit=0）
- **待用户执行**：在 Unity 编辑器菜单运行 `Thrud/Setup ConfigScene` 生成场景文件并置 [0]，然后 Play 验证启动链路（ConfigScene 加载 → additive 加载 Login）
- SceneFlow 细节：卸载旧玩法场景（保留 ConfigScene）、active scene 管理、切换互斥（切换中忽略新请求）、LoadingGroup 场景内置优先 + Resources 回退（过渡，C4 移除回退）
- 守卫细节：编辑器直接 Play 玩法场景时自动 additive 加载 ConfigScene；AppBootstrapper 仅在 ConfigScene 为 active 入口时加载首个玩法场景，由守卫拉起时不打扰当前场景

### 执行记录（2026-09-01，SceneRefTable 改造 / 决策 D24）

- **废弃 `SceneNames` 硬编码常量**，新增 `SceneRefTable`（ScriptableObject，`Assets/Resources/SceneRefTable.asset`）：
  - 每个条目 = `GameScene` 逻辑枚举 ↔ `SceneAsset` 引用（**GUID 锚定**，重命名/移动场景文件不失效）↔ 自动同步的路径字符串
  - `SceneAsset` 字段 `#if UNITY_EDITOR` 包裹；运行时只读已序列化的路径，打包后引用字段自动消失、路径保留
  - `OnValidate` 自动同步路径：场景资产重命名/移动后，路径随引用自动刷新，**代码零改动**
  - ConfigScene 用独立字段（它是入口/常驻层，不属于玩法场景切换目标）
- `SceneFlow` / `AppBootstrapper` / `ConfigSceneGuard` 全部改为表驱动：场景路径、ConfigScene 名称均从表读取；表实例由 AppBootstrapper 注入（`SetCurrent`），未注入时 Resources 兜底（过渡，热更接入后迁移）
- 新增编辑器工具 **`Thrud/Setup SceneRefTable`**：确保表资产存在 → 扫描 `Assets/01_Scenes` 按文件名映射填充条目（一次性初始化便利）→ 同步 Build Settings（表内场景全部加入、ConfigScene 强制 [0]、保留表外已启用场景）
- `ConfigSceneCreator` 同步改造：创建 GameRoot 时经 SerializedObject 注入表引用到 `AppBootstrapper`/`SceneFlow`
- **编译验证通过**（Assembly-CSharp exit=0，Assembly-CSharp-Editor exit=0）
- **待用户执行**：Unity 编辑器执行 `Thrud/Setup SceneRefTable`（生成表资产并填充），再执行 `Thrud/Setup ConfigScene` 生成场景并置 [0]

## 风险与注意

- 场景切换统一走 additive，禁止单场景 `LoadScene`（会卸载 ConfigScene）；代码审查重点检查
- 卸载旧场景前需确保其 `OnDestroy`/`OnDisable` 正确反注册（QFramework `IUnRegister` 纪律）
- 编辑器直接 Play 玩法场景依赖守卫；守卫故障时表现为"缺配置报错"，排查先看 Console 警告
- `BattleGraybox` 与现有 `4BattleScene-Test0/1` 的关系：灰盒测试盒统一收敛，避免测试场景膨胀
