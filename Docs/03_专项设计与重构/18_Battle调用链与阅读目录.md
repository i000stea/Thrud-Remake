# Battle 调用链与阅读目录（P1A · S2 补充交付物）

> **2026-09-19 Actor 专项更新（已实现，编译/Play 待验）**：用户确认移动/传送 Actor、角色/武器/相机表现 Actor，InputSystem 保持原样；旧 BattleViewSystem 及 InitializeView 已删除，现为 **1 Model + 10 System + 4 Actor**。Aim 发布 BindableProperty 位置，主面板负责 UI 映射与 Lerp；血条/弹药/队伍 HUD 归主面板。当前入口、帧序、生命周期、资产操作与回归步骤见 [19 当前实施记录](19_表现层分层_View与Actor_设计.md)。**下文的 §7 事件表（行号与事件名）、§8 各功能链内的旧 System 名（ViewSystem / HealthSystem）为迁移前快照，不作为当前调用依据；§2 / §4 / §5 / §6.1 / §9 / §10 的行数与归属已于 2026-09-19 重测（最后一次重测在 `56c249bf` 之后）。**
>
> **2026-09-19 第二批更新（表现层分层续作 + 模型平铺）**：
> - **系统数 10 → 9**：`BattleHealthSystem` **已合并进 `BattleRoleSystem`**（`partial`：`Role/BattleRoleSystem.cs` + `Role/BattleRoleSystem.Health.cs`），消除了 `GetSystem<BattleRoleSystem>().HandleRoleDowned(...)` 跨系统调用；`BattleArchitecture` 注册 9 项。
> - **`BattleHudState` 已删除**：字段**平铺**进 `BattleStateModel`（12 个 `#region` 分区）；`SelectedRoleIndex`/`RoleEnergy` 由"仿真字段 + HUD 镜像"**合并为单一 `BindableProperty`**；重命名为 `CurrentMemberHealth`/`CurrentMemberMaxHealth`/`CurrentWeaponMagazine`/`CurrentWeaponMaxMagazine`/`CurrentWeaponReloading`/`CurrentWeaponReloadProgress`（**原始 0..1**，百分比由 Panel 自己算并做 View 侧节流）。
> - **`UI/BattleUiSystem.cs` → `Core/`**（`UI/` 现为纯 View 层：`BattleTeamHudView` + `Panels/*`）。
> - **`BattleMovementSystem` 不再持有 Actor**：`TickUpdate(basisForward)`/`TickFixedUpdate(moveDirection)` **只返回力**，宿主 `ApplyMovementForce` 交给 `BattleMovementActor`；`IsAvailable` 门控移到宿主。**全部 System 已无 View 字段（实测 0 处）**。
> - **`BattleStateModel` 不按"关卡/玩家/敌人"拆分**（评估结论，已跳过）：一个模型 = 一个生命周期 + 一组不变量；三域共享生命周期且 `LevelState`/`ControlTakenOver`/`Paused` 为横切字段。详见 `19` 文档状态块。
>
> **2026-09-19 第三批更新（宿主瘦身四步落地，见 [`21_战斗场景装配与宿主瘦身_设计与落地指导.md`](21_战斗场景装配与宿主瘦身_设计与落地指导.md)）**：
> - **宿主 559 → 314 行**（第 4 步完成时 309 行，随后 `248df6ca` 的失败路径修复 +5）：`BattleSceneRuntimeHost` 只剩「场景生命周期 + 装配调用 + 相位表」三类职责。**数据解析**搬进新文件 `Core/BattleSetupSystem.cs`（247 行）、**装配**搬进新文件 `Actor/BattleSceneAssembler.cs`（140 行，**组合根：普通 C# 类，不是 System**）、**每帧中间量**下沉进各 Actor。宿主不再有 `SetupBattleSystems()` / `ResolveLevel()` / `ResolveBattleDatas()`。
> - **系统数 9 → 10**：新增 `BattleSetupSystem`（**无状态**：关卡目录 / 出战编目按次作为参数传入），`BattleArchitecture` 注册顺序为 Setup → Role → Input → Movement → Camera → Aim → Weapon → Skill → LevelFlow → Ui（`BattleArchitecture.cs`，47 行）。
> - **`BattleStateModel.Controller` 的唯一写入者变为宿主 `Awake`**（`BattleRoleSystem.Initialize` 不再写，避免同一引用的第二份拷贝）；宿主新增只读桥接属性 `private Battle_PlayerControl Rig => StateModel.Controller;`。
> - **时机变化 1 处**：出生点传送由 `Awake` 移到装配器 `Build(...)`（Start 阶段，仍在一切 Update 之前）。
> - 六个提交：`9b4e7e69`（第 1 步）/ `a77eb347`（第 2 步）/ `69f851ab`（第 3 步）/ `50e509b5`（第 4 步）/ `fcb43173`（`BattleRoleActor` 的 Model 改**构造期注入**，`Actor/` 内 `GetModel<` 命中 0 处）/ `248df6ca`（修「搬运引入」的真实缺陷：`Awake` 必须丢弃 `TryResolveLevel` 失败的 `out` 值）。⚠️ **Unity 侧编译与 Play 回归尚未执行**（本轮验证是离线整程序集编译，见 §12 与 21 文档 §8.2）。
>
> **2026-09-19 第四批更新（桥接层退役 + 弹药发布）**：
> - **`BattleRuntimeBridge` 整个类与文件已删除**（`Data/BattleRuntimeBridge.cs` 61 行 + `.meta` 一并删，`Data/` 只剩 `BattleRuntimeData.cs`）。玩家目标改由 **`Core/BattleCombatTypes.cs` 的 `IBattlePlayerTarget` + `BattlePlayerRegistry`**（与敌人侧 `IBattleEnemyTarget`/`BattleEnemyRegistry` 同文件同形）提供，rig **`Awake` 自注册 / `OnDestroy` 自注销**（`Battle_PlayerControl.SyncRuntimeBridge()` 也已删除）。
> - **宿主相位表少一步**：4 处 `Rig.SyncRuntimeBridge()`（Update 暂停分支、Update 尾、FixedUpdate 尾、LateUpdate 尾）与装配器里那 1 处全部删除 → **相位表里再无"同步桥接"步骤**。
> - **弹药发布归武器系统**：宿主不再每帧 `PublishAmmo`，改由 `BattleWeaponSystem` 在**变更点**（换弹完成 / 手动换弹 / 开火 / 切人）发布，仅装填期间按帧发布进度；装配器保留一次作首帧初值。
> - 读者新写法（8 处 / 6 文件）与新入口见 §8.10；验证口径见 §12（**只有离线整程序集编译**）。
>
> **行数口径（本节所有数字均为 2026-09-19 实测）**：`(Get-Content <file> -Encoding UTF8).Count`；`03_Battle/` 范围**不含** `Contracts/` 与 `Editor/`。

| 原调用 | 当前归属 |
|---|---|
| InitializeView | Host 装配 Actor → RoleSystem.AttachActor → RoleActor.Attach/ShowMember/PlaySpawn |
| ViewSystem.TickUpdate | Host.TickActors（动画→FOV→模型→武器）；弹药读数改由 `BattleWeaponSystem` 在变更点发布（2026-09-19） |
| ViewSystem 血条/队伍列表 | RoleSystem 发布血量绑定；主面板渲染并持有队伍 HUD |
| AimSystem 准星 anchoredPosition | Hud.AimPosition → 主面板 TickAim，Host 在 Aim.TickLateUpdate 之后调用 |
| rig.TransformMoveDirection/AddForce | MovementActor.ResolveDirection/ApplyForce |


> 产出日期：**2026-09-18**（**§2 / §4 / §5 / §6.1 / §9 / §10 的行数与归属已于 2026-09-19 重测**，见上方第一/二/三/四批更新块）。定位：P1A 的 S2「现状地图」在 `03_Battle`（**70 个文件 / 9896 行**）上的补齐——**一份可以当目录用的调用链说明书**。
> **2026-09-18 二次修订**：主 HUD（技能行）已从 `Battle_PlayerControl` 迁至 `Battle_MainPanel`（`BindableProperty` 反向绑定），新增 `UI/BattleHudState.cs` 与 `UI/Panels/Battle_MainPanel.cs`(+Designer)；`Battle_PlayerControl` 285 → **206 行**。表现层的后续规划见 `19_表现层分层_View与Actor_设计.md`。
> 关系：域级地图见 `17_代码现状地图.md`；本文只覆盖 `03_Battle/`（不含 `Contracts/`、`Editor/`）。纪律见 `16_热更新方案选型与落地计划.md` §5A。
> **取值说明**：所有调用顺序、事件收发点、依赖边都由脚本从源码实测（§9 的行数用 `Get-Content | .Count` 口径，与行号一致）；凡标「推断」的是按方法职责判断、未逐行验证。

---

## 0. 怎么用这份文档（三条阅读路线）

| 路线 | 时长 | 走法 |
|---|---|---|
| **A · 10 分钟建立骨架** | 短 | §1 总览图 → §2 入口生命周期 → §3 每帧驱动顺序。读完应能回答"战斗每帧到底在跑什么、按什么顺序"。 |
| **B · 半天通读主干** | 中 | §2 → §4 启动装配 → §5 架构与 10 个 System → §6 状态模型 → §7 事件总线 → §8 逐条功能链（挑你关心的域） |
| **C · 按功能查** | 随时 | 直接跳 §8 的具体小节（移动/锁枪/武器/敌人/技能/关卡/UI/池），每节末尾给出"该看哪几个文件" |

§9 是**文件清单（就是你要的目录）**，§10 是推荐阅读顺序与"每步要回答的问题"，§11 是**读的时候一定会困惑的地方**。

---

## 1. 总览：一句话架构 + 数据流

**一句话**：`BattleGraybox` 场景上的 **`battle_runtime`（`BattleSceneRuntimeHost`，310 行）是唯一的「场景生命周期 + 装配调用 + 相位表」节点**（它自己不再含数据解析与装配体，两者分别在 `BattleSetupSystem` 与 `BattleSceneAssembler`）；它驱动 QFramework 的 **`BattleArchitecture`（1 个 Model + 10 个 System）**；各 System 通过 **共享状态模型**（读）+ **强类型事件**（写通知）协作，`Battle_PlayerControl`（rig）退化为**纯 View**。

```
                       ┌──────────────── BattleGraybox.unity ────────────────┐
   [PrefabInstance]    │  [PrefabInstance]                                   │
   UIRoot ────────────►│  Battle_PlayerControl.prefab (rig)  ← 纯 View       │
                       │  Damage-Texts (BattleEnvironmentEffectController)   │
                       │  battle_runtime (BattleSceneRuntimeHost) ★总入口    │
                       │  BattleStageDirector (波次/刷怪)                    │
                       │  Environment / Directional Light / volume           │
                       └────────────────────┬────────────────────────────────┘
                                            │ Awake: 确保架构 + rig 发布进 Model + 委托 SetupSystem 解析关卡 + 注入 Director
                                            │ Start: BattleSceneAssembler.Build(level)  ← 装配链
                                            │ Update/Fixed/Late: Tick 各 System
                                            ▼
                        BattleArchitecture (QFramework Architecture)
              ┌─────────────────────────────┬───────────────────────────────┐
              │ BattleStateModel (唯一 Model)│ 10 个 AbstractSystem           │
              │  Controller / BattleDatas    │  Setup Role Input Movement     │
              │  Input / Movement            │  Camera Aim Weapon Skill       │
              │  RoleEnergy / LevelState     │  LevelFlow Ui                  │
              │  LockEnemy / Paused …        │                               │
              └──────────────┬──────────────┴───────────────┬───────────────┘
                             │ 读（共享状态）                │ SendEvent / RegisterEvent
                             ▼                              ▼
              rig 的 Designer 引用（相机/枪口/Canvas/子弹 prefab/动画）   订阅方刷新表现
```

**核心取舍（读代码前先接受这三条）**

1. **数据流单向**：`Input → State → Systems → 事件 → 表现/UI`。System 之间**不互相直接调用**（只有 `battle_runtime` 顺序调用它们）。
2. **Model 是"可变共享黑板"**，不是不可变快照——所以"谁写谁读"必须靠纪律（§6 给了归属表）。
3. **`Battle_PlayerControl` 不再承担初始化/驱动**（2026-09-08 上移）；它只提供场景引用与工具方法。看到它没有 `Update` 不要惊讶——`Update` 在 `battle_runtime` 上。

---

## 2. 入口与生命周期（`03_Battle/BattleSceneRuntimeHost.cs`，**310 行**）

`BattleSceneRuntimeHost` 的对象名约定是 **`battle_runtime`**（`[DisallowMultipleComponent]`）。

> **2026-09-19「宿主瘦身」后的定稿**：本类只剩「**场景生命周期 + 装配调用 + 相位表**」三类职责。
> 判据（21 文档纪律 1）：「这段代码是否只在场景装配/卸载时才需要存在？」是 → 留；否 → 搬走。
> 因此**数据解析**在 `Core/BattleSetupSystem.cs`、**装配**在 `Actor/BattleSceneAssembler.cs`、**每帧中间量**在各 Actor 内，本类不残留其中任何一项。

### 2.1 Awake —— "确保有架构 + 把关卡数据落到场景"

| 顺序 | 调用 | 作用 |
|---|---|---|
| 1 | `ResKitBootstrap.EnsureInitialized()` | ResKit 初始化收口（正常路径已由 `0ConfigScene` 的 `AppBootstrapper` 异步初始化；此处幂等补 + 校验 AB 清单） |
| 2 | `_ = BattleArchitecture.Interface;` | **触发 QFramework 架构创建**（幂等）→ `Init()` 注册 Model + 10 System |
| 3 | `StateModel.Controller = playerControl;` | **把 rig 发布进 Model**（2026-09-19 第 1 步）：Model 成为**运行期唯一持有者**，序列化字段 `playerControl` 只是"场景绑定输入"（装配前提供引用）。此后本文件一律走只读桥接属性 `private Battle_PlayerControl Rig => StateModel.Controller;`，不再有"装配期用字段、运行期用 Model"的双路径 |
| 4 | `if (!Architecture.GetSystem<BattleSetupSystem>().TryResolveLevel(gameObject.scene.name, catalog, out level)) { level = null; }` | 解析本场关卡（**2026-09-19 第 2 步搬出**到 `Core/BattleSetupSystem.cs`）：`BattleLevelSession.Selected`（且 `ArtSceneName == 当前场景名`）优先；否则 `catalog.FindFirstByArtScene()` 走 D12 直连默认。**失败必须丢弃 out 值**（`248df6ca` 修掉的真实缺陷，见下） |
| 5 | `ApplyLevelToScene(level)` | **只剩"把数据交给场景固件"**：`stageDirector.Setup(level, catalog?.EnemyCatalog)` + 未变的 `[battle_runtime] 关卡 … 装配完成：…` 日志；`level == null` 直接 return。**出生点传送已不在此处**（见下） |

> ⚠️ **失败路径的 `out` 值必须丢弃（`248df6ca`）**：`TryResolveLevel` 返回 false 时，`out selected` 可能仍是**"属于别的美术场景"的会话关卡**（`catalog == null` 且会话关卡与本场景不符时的路径）——宿主若照用就会把错的关卡注入 `BattleStageDirector`。现在的写法是 `if (!...TryResolveLevel(..., out level)) { level = null; }`，并且 `TryResolveLevel` 自身在失败分支末尾**也置 `selected = null`**（双保险）。置空后场景保底运行（无波次数据），与搬运前"`return` 后不注入"的行为一致。
> ⚠️ 解析失败由 `BattleSetupSystem` 记 `LogError`（会话缓存关卡与本场景不符时另有一条 `LogWarning`）；此时 `level` 为空、`ApplyLevelToScene` 立即 return，场景**保底运行（无波次）**——不会崩，但也不会有敌人。
> ⚠️ **2026-09-19 唯一时机变化**：出生点传送 `MovementActor.Teleport(level.PlayerSpawnPosition, Quaternion.Euler(level.PlayerSpawnEuler))` 由 `Awake` 移到装配器 `Build(...)`（即 Start 阶段，**仍在一切 Update 之前**）。已静态核对：Awake→Start 之间没有任何读者依赖 rig 位置（`BattleStageDirector` 只在自身 `Update` 路径经 `BattlePlayerRegistry.CurrentTransform` 读玩家位置）——**Play 侧未回归验证**。
> 📌 **玩家目标不再由宿主每帧喂（2026-09-19 桥接层退役）**：rig 在 `Awake` **自注册**（`BattlePlayerRegistry.Register(this)`）、在 `OnDestroy` **自注销**（`UnRegister`，身份守卫在注册表内），敌人侧"找玩家"的 **8 处读者 / 6 个文件**（`BattleStageDirector` + 4 个 `IEnemyBehaviour` + `BattleEnemyEntity`）一律改读 `BattlePlayerRegistry.CurrentTransform`。详见 §8.10。

### 2.2 Start —— "整场装配"（见 §4）

```
Start() → assembler = new BattleSceneAssembler(Rig, StateModel,
                            Architecture.GetSystem<BattleSetupSystem>(), teamCatalog, applyPlayerSpawn)
        → battleReady = assembler.Build(level)                            // ① 战斗装配（组合根在装配器里）
        → if (battleReady) StartCoroutine(OpenBattleUiWhenSceneReady())    // ② 等 UIRoot 归属稳定后开战斗 UI
```

> 旧的宿主私有方法 **`SetupBattleSystems()` 已不存在**（2026-09-19 第 4 步整段搬进 `BattleSceneAssembler.Build`）；协程也从 `OpenTouchUiWhenSceneReady` 改名 `OpenBattleUiWhenSceneReady`（同一次打开主 HUD + 触屏层）。装配"必须在所有对象 `Awake` 之后"这条顺序依赖，由**在 `Start` 里调用**表达，装配器内不用协程/延时掩盖。

### 2.3 OnDestroy —— "离场收口"

1. `battleReady = false` + `assembler?.RoleActor?.Dispose()` —— 纯 C# 的 `BattleRoleActor` 没有 MonoBehaviour 销毁钩子，必须由持有者显式反注册其事件订阅
2. `if (StateModel.Controller) StateModel.Controller = null;` —— 清 rig；判据从"是否等于字段"退化为"是否已被本宿主发布过"（见 §6.1）
3. `Battle_MainPanel.CloseForBattle()` + `Battle_TouchInputPanel.CloseForBattle()`（同一段 try/catch，两者均幂等）
4. `UiPanelFlow.SanitizeRegistry()` —— 摘除 UIKit 表中已销毁的面板条目（**不摘除会导致下次进战斗面板打不开**，见 09 关键坑 29）

### 2.4 场景入口清单（实测）

| 场景对象 | 类型 | 挂载脚本 |
|---|---|---|
| `battle_runtime` | 普通根对象 | **`BattleSceneRuntimeHost`** ★ |
| `BattleStageDirector` | 普通根对象 | `BattleStageDirector` |
| `Damage-Texts` | 普通根对象 | `BattleEnvironmentEffectController` |
| `Environment` | 普通根对象 | （子对象 `Graybox_Ground` / `Graybox_Wall_*`） |
| `Directional Light` / `volume` | 普通根对象 | —— |
| **`UIRoot`** | **PrefabInstance** | —— |
| **rig** | **PrefabInstance**（`Assets/04_Prefabs/03Battle/Battle_PlayerControl.prefab`） | `Battle_PlayerControl` + Designer |

> 场景上共引用 6 个脚本 guid，其中 4 个属 `TeaGame.Battle`，2 个属 prefab/第三方。
> **`UIRoot` 与 rig 是 PrefabInstance**（不是普通根对象）——用"找根对象"的脚本扫场景时会漏掉它们，注意。

---

## 3. 每帧驱动顺序（**读代码最该记住的一张表**）

全部在 `BattleSceneRuntimeHost.Update / FixedUpdate / LateUpdate`。**唯一入口顺序如下**：

### Update（每帧）

| # | 调用 | 条件 |
|---|---|---|
| 0 | `!battleReady \|\| Rig == null` → return | 未装配成功（rig 缺失 / `Build` 返回 false）直接跳过 |
| 1 | `BattleRoleSystem.SetControlTakenOver(Rig.ControlTakeOff)` | 每帧把 rig 的开关**同步回** State（历史坑：不同步会导致死亡接管被写回 false） |
| 2 | `BattleInputSystem.Tick()` | 采集键鼠 **或** 触屏（二选一，不合并） |
| 3 | ESC → `SendEvent(new BattleEvent_PauseToggleRequested())` | 仅当 `LevelState ∉ {Victory, Defeat}` |
| 4 | `if (state.Paused) return;` | **暂停短路点在输入之后、其余系统之前（无其他副作用）** |
| 5 | `ApplyMovementForce(BattleMovementSystem.TickUpdate(assembler.MovementActor.Forward))` | 翻滚/冲刺状态机；**System 只算力、Actor 只施力**（2026-09-19 第 1 步同批） |
| 6 | `BattleCameraSystem.Tick()` | 相机 |
| 7 | `TickActors()` | 表现三件套：`RoleActor.Tick`（动画 + 模型朝向）/ `CameraActor.BlendFov` / `WeaponActor.Tick`（瞄准）。**顺序是逻辑**：必须在 `BattleWeaponSystem.Tick()` **之前**（后者同帧写 `FreeAimRotation` 并 `PressTrigger()`）。原 `BattleViewSystem.TickUpdate()` 已于 2026-09-19 删除；弹药读数已不在此处发布（见下方注） |
| 8 | `BattleWeaponSystem.Tick()` | 换弹进度 + 开火检测 |
| 9 | `BattleSkillSystem.Tick()` | 冷却读秒广播 + 读 E/Q 输入尝试施放 |

*（原第 10 步 `Rig.SyncRuntimeBridge()` 已删除：**宿主相位表里不再有任何"同步桥接"步骤**——桥接层于 2026-09-19 退役，玩家目标改由 rig 自注册，见 §8.10。）*

### FixedUpdate

`打桩：!battleReady || Rig == null 判空 → Paused 判空 →`
1. `movementActor.ResolveDirection(...)`（Actor 解析世界方向，不可用则给零向量）→ `BattleMovementSystem.TickFixedUpdate(moveDirection)`（**只算力**）→ `ApplyMovementForce(...)`（宿主交 `MovementActor` 施力）
2. `BattleAimSystem.TickFixedUpdate()`（锁敌搜索 + Tab 切换）

### LateUpdate

1. `BattleAimSystem.TickLateUpdate()`（锁敌准星平滑移动）
2. `Battle_MainPanel.TickForBattle(Time.deltaTime)`（主 HUD：准星位置 + 弹药/血量等 View 侧刷新）

> **注意 3 号点**：`BattleCameraSystem.Tick()` 在 **Update** 里，`BattleAimSystem` 在 **Fixed/Late** 里——所以"相机跟随"与"准星吸附"不在同一时间粒度上。
> **注意暂停语义**：`Paused` 用 `Time.timeScale = 0`（进程不暂停），UI 仍可点；`FixedUpdate/LateUpdate` 各自独立判 `Paused`。
> **注意弹药读数（2026-09-19）**：`PublishAmmo` **不再出现在宿主相位表里**——发布权归生产者 `BattleWeaponSystem`（变更点：换弹完成 / 手动 `StartReload` / `PressTrigger` / `OnRoleSwitched`），唯一连续量"装填进度"**仅在 `weaponMode.IsReloading` 期间**按帧发布；装配器 `Build` 里保留一次作**首帧初值**（§4 第 9 步）。顺带修掉一处真问题：原先宿主在 `WeaponSystem.Tick()` **之前**发布 → 开火/换弹结果慢一帧进 Model（HUD 滞后一帧），现在同帧可见。

---

## 4. 启动装配链（`BattleSceneAssembler.Build(...)`，逐行）

> 2026-09-19 第 4 步：原宿主私有方法 `SetupBattleSystems()` 整段搬进 **`Actor/BattleSceneAssembler.cs`（139 行）**——它是本场景的**组合根**（只在这里 `new` 具体对象、只在这里按顺序调用），**普通 C# 类、不是 System**，由宿主 `Start` 调用一次并只被宿主持有。

```
battle_runtime.Start() → assembler.Build(level)      // 只调用一次；返回「是否完成装配」
 ├─ 0. !rig ? → LogError 并 return false（提示用 Thrud/Battle/T3.1C/1 工具放固件）
 ├─ 1. state.Paused = false                     // 复位暂停态（架构常驻跨场景，必须复位）
 ├─ 2. MovementActor = new BattleMovementActor(rig.transform, rig.SelfRigidbody,
 │                        rig.ControlXAxis, rig.Control_Camera)
 │     BattleMovementSystem.Initialize(rig.PlayerConfig)   // System 只接配置，不再持有 Actor
 ├─ 3. resolution = setup.ResolveBattleDatas(teamCatalogFallback)   // ★ 数据管道，见下
 │     └─ resolution.RetreatToEntryScene → UiPanelFlow.CloseAllSafely()
 │        + BattleSceneGate.TransitionTo(BattleTeamSession.ReturnSceneName)
 │        （与搬运前一致：装配**不中断**，按空队继续跑，离场由场景切换收口）
 ├─ 4. BattleRoleSystem.Initialize(rig, datas)   // 注入 rig + 角色数据（成员级血量复位已并入本系统）
 ├─ 5. 三个表现 Actor：RoleActor / WeaponActor / CameraActor（每帧中间量在 Actor 内派生）
 ├─ 6. RoleActor.Initialize(datas)               // 挂点 + 自订阅切人/阵亡/翻滚事件 + 播出生动画
 ├─ 7. if (applyPlayerSpawn && level != null) → MovementActor.Teleport(
 │        level.PlayerSpawnPosition, Quaternion.Euler(level.PlayerSpawnEuler))
 ├─ 8. BattleAimSystem.ResetForBattle()          // 重置瞄准
 ├─ 9. state.PublishAmmo(state.CurrentBattleData)   // 只给首帧一个初值；此后由 BattleWeaponSystem 在变更点发布
 ├─ 10. BattleElementBulletLibrary.Register(冰/火/风/电 4 个子弹 prefab)  // T2.3 元素子弹
 ├─ 11. BattleSkillSystem.ResetForBattle()       // 重建冷却表 + 清零能量 + 广播首帧
 ├─ 12. BattleLevelFlow.StartLevel()             // 状态机 → Prepare
 └─ 13. Debug.Log("[battle_runtime] 战斗系统装配完成：出战 {n} 人。") → return true
```

> ⚠️ **第 7 步是相对搬运前唯一的时机变化**：出生点传送原先在宿主 `Awake`，现随装配链落在 Start 阶段（**仍在一切 Update 之前**）。静态核对结论：Awake→Start 之间无读者依赖 rig 位置（`BattleStageDirector` 只在自身 `Update` 路径经 `BattlePlayerRegistry.CurrentTransform` 读玩家位置）。**Play 侧未回归验证。**
> 📌 **原第 10 步 `rig.SyncRuntimeBridge()` 已删除（2026-09-19）**：装配器里那 1 处调用与宿主 4 处一并移除——桥接层退役后玩家目标由 rig 在 `Awake` 自注册，装配链不再需要"同步桥接"这一步（原 11–14 步依次上移为 10–13）。
> **[2026-09-18 迁出]** 技能 HUD 不再由 rig 绑定：文本渲染与数据绑改由 `Battle_MainPanel` 在宿主 `OpenBattleUiWhenSceneReady` 打开面板时自行 `BindHud()`（`BindableProperty` 反向绑定）——故本装配链中已无对应步骤。

### `BattleSetupSystem.ResolveBattleDatas()` —— 最有信息量的一段（数据从哪来）

> **2026-09-19 第 2 步**：整条数据管道（`ResolveTeamCatalog` / `DescribeCatalogState` / `DescribeSessionIds` / `BuildDefaultFromCatalog` / `#if UNITY_EDITOR` 直读回落，**日志文案逐字未变**）已从宿主搬进 `Core/BattleSetupSystem.cs`（247 行）。该方法返回 **`BattleTeamResolution`**（嵌套 struct：`BattleRoleRuntimeData[] Datas` + `bool RetreatToEntryScene`）——解析失败时**只置标志**，清面板表与退回场景由装配器执行（System 不驱动 View/流程）。`BattleSetupSystem` **无状态**：关卡目录 / 出战编目由宿主按次作为参数传入，架构级 System 不缓存场景级引用。

```
① BattleTeamSession.HasSelection ?
     ├─ 是 → ResolveTeamCatalog() 拿编目
     │        ├─ null      → 警告「会话存在但编目未解析到」→ Clear() 会话
     │        └─ 非 null   → BattleTeamRuntimeBuilder.Build(Members, catalog)
     │                        ├─ 有结果 → return（正常路径）
     │                        └─ 空     → 警告「id 未命中」→ Clear() 会话
     └─ 否 → failure = "出战会话为空（未走选关→编队→开始出战）"
② #if UNITY_EDITOR
     BuildDefaultFromCatalog(resolvedCatalog)  → 用编目前 4 对角色×武器兜底（直连联调）
     仍为空 → 空队保底运行
   #else
     LogError 详细诊断 → return { Datas = 空, RetreatToEntryScene = true }
     → 装配器收口：UiPanelFlow.CloseAllSafely() + BattleSceneGate.TransitionTo(ReturnSceneName)
   #endif
```

`ResolveTeamCatalog()`（`BattleSetupSystem` 私有静态方法）的优先级：**① `BattleAssetService.ResolveCatalog()`（AB `battle_characters`，真机唯一可用路径）→ ② 场景序列化的 `teamCatalog` 兜底 → ③ `#if UNITY_EDITOR` 直读 `Assets/Data/BattleTeamCatalog.asset`**。

> ⚠️ ③ 就是 09 关键坑 28/30 那个「**编辑器专属回落**」——编辑器能跑通、真机/热更 DLL 返回 null。

---

## 5. QFramework 架构：1 个 Model + **10 个 System**

`BattleArchitecture.Init()`（`BattleArchitecture.cs`，47 行）**先 Model 后 System** 注册（下表即注册顺序）：

| # | 注册项 | 类型 | 访问方式 | 一句话职责 |
|---|---|---|---|---|
| 0 | `BattleStateModel` | `AbstractModel` | `Architecture.GetModel<>()` | 全体 System 的共享黑板（§6） |
| 1 | `BattleSetupSystem` | System | `GetSystem<>()` | **入场数据解析（2026-09-19 第 2 步新增，排第一）**：关卡解析 + 出战编队解析（原宿主的数据管道与诊断）。**无状态**：关卡目录 / 出战编目按次传参 |
| 2 | `BattleRoleSystem` | System | 同上 | 角色初始化/切人/阵亡自动切人/接管控制 + **成员级血量、受击结算、治疗、全灭判定**（`BattleHealthSystem` 已于 2026-09-19 并入：`partial` = `Role/BattleRoleSystem.cs` + `Role/BattleRoleSystem.Health.cs`） |
| 3 | `BattleInputSystem` | System | 同上 | 每帧输入采样（键鼠 / 触屏二选一）、ESC 暂停请求 |
| 4 | `BattleMovementSystem` | System | 同上 | 翻滚/冲刺状态机。**只算力、不持 Actor**：`TickUpdate`/`TickFixedUpdate` 返回力，宿主交 `BattleMovementActor` 施加 |
| 5 | `BattleCameraSystem` | System | 同上 | 相机状态 |
| 6 | `BattleAimSystem` | System | 同上 | 锁敌搜索、Tab 切换、准星平滑 |
| 7 | `BattleWeaponSystem` | System | 同上 | 换弹进度 + 开火检测 + 切人补弹 + **弹药读数在变更点发布**（2026-09-19：宿主不再每帧发布，见 §3 注） |
| 8 | `BattleSkillSystem` | System | 同上 | 能量经济 + E/Q 冷却 + 施放管道 |
| 9 | `BattleLevelFlow` | System | 同上 | 关卡状态机 Prepare→Wave→Victory/Defeat→Exit |
| 10 | `BattleUiSystem` | System | 同上 | 结算/暂停面板的 UIKit 开合 + 暂停切换（文件在 `Core/BattleUiSystem.cs`，2026-09-19 自 `UI/` 迁出） |

> 📌 **计数沿革（三处易错）**：① 09 交接原文写「注册 `BattleStateModel` + **8 个 System**」——已于 2026-09-18 在 09 中改正；② 2026-09-19 当天 `BattleHealthSystem` 并入 `BattleRoleSystem` → 9 项；③ 同一天宿主瘦身第 2 步新增 `BattleSetupSystem` → **10 项**（即上表）。`BattleViewSystem` 已于 2026-09-19 第 1 步**删除**（表现改由 4 个 Actor + `Battle_MainPanel` 承担），本表不再列它。
> 📌 **全部 System 已无 View 字段**（2026-09-19 实测 0 处）：View 侧 Actor 由宿主持有并 tick。

`BattleArchitecture` 的类注释里留着一句自评：**「战斗架构未通读，本身独立性较高，我认为现阶段能用就行」**——这句就是本次要解决的问题。

---

## 6. 状态模型：`BattleStateModel` 字段归属表

`BattleStateModel`（+ 两个辅助类 `BattleInputSnapshot` / `BattleMovementState`）是**唯一 Model**。下面按"谁写 / 谁读"分组（**写入方据方法职责判定，未逐行验证**；唯 §6.1 的 `Controller` / `BattleDatas` 两行已于 2026-09-19 实测核对——见 §12）：

### 6.1 装配期写入（只写一次）
| 字段 | 写入方 | 读取方 |
|---|---|---|
| `Controller` | **`BattleSceneRuntimeHost.Awake`（2026-09-19 起为唯一写入者；`OnDestroy` 置 null）** | 几乎全部 System + 宿主只读桥接属性 `Rig` |
| `BattleDatas` | `BattleRoleSystem.Initialize` | Role / Input（`CycleRole`）/ Weapon（`OnRoleSwitched` 补弹）/ Skill / 主面板（HUD） |
| `RoleEnergy` / `SelectedRoleIndex` | `BattleSkillSystem.ResetForBattle` / `RoleSystem` | Skill / View / HUD |

### 6.2 每帧写入
| 字段 | 写入方 | 读取方 |
|---|---|---|
| `Input.*`（`BattleInputSnapshot`） | **`BattleInputSystem.Tick`**（唯一写入方） | Movement / Weapon / Skill / View |
| `Movement.*`（`BattleMovementState`） | `BattleMovementSystem.TickUpdate/TickFixedUpdate` | View（动画）/ Camera |
| `CameraPitch` / `CurrentFov` | `BattleCameraSystem.Tick` | View |
| `ScreenTarget` / `LockSpeedMultiplier` | `BattleAimSystem` | View（准星） |
| `ControlTakenOver` | `BattleRoleSystem.SetControlTakenOver`（由 `battle_runtime` 每帧喂 `rig.ControlTakeOff`） | Movement / Weapon / Skill |

### 6.3 事件/状态机写入
| 字段 | 写入方 | 读取方 |
|---|---|---|
| `LockEnemy`（`IBattleEnemyTarget`） | `BattleAimSystem` | View / Weapon / SkillContext |
| `LevelState` | `BattleLevelFlow.TransitionTo` | `BattleUiSystem` / `battle_runtime`（ESC 守卫） |
| `Paused` | `BattleUiSystem`（响应 PauseToggle/Resume 事件） | `battle_runtime`（短路） |
| `RoleEnergy` | `BattleSkillSystem.AddEnergy` | Skill / HUD |

> ⚠️ **`LockEnemy` 判空必须用 `BattleCombatUtility.IsAlive()`**，不能直接 `!= null`——接口持有 `UnityEngine.Object` 时不触发 Unity 判空重载，敌人 `Destroy` 后会抛 `MissingReferenceException`（09 关键坑 13）。

---

## 7. 事件总线（实测全表）

> ⚠️ **2026-09-19 状态（本节两张表未逐条重测）**：事件类型已统一改名为 **`BattleEvent_*`**（如 `BattleEvent_RoleSwitched`），现共 **19 个**强类型事件结构（`Core/BattleEvents.cs` **13 个** + `Level/BattleLevelEvents.cs` 6 个；较旧文的 18 个多出 `BattleEvent_ControlTakeOverRequested`——步骤 5 控制接管事件化时新增）。**下面两张表的行号与旧名（`BattleXxxEvent`）是 2026-09-18 的快照**，按行号跳转前请先用 `Select-String "SendEvent|RegisterEvent"` 现场核对。

### 7.1 发出点（SendEvent）——共 22 处

| 事件 | 发出方（文件 L行） |
|---|---|
| `BattlePlayerDamagedEvent` | `Enemy/BattleEnemyBullet.cs` L111（敌人子弹命中玩家）、`Enemy/BattleEnemyEntity.cs` L393（范围伤害/爆炸） |
| `BattleRoleHealthChangedEvent` | `Core/BattleHealthSystem.cs` L64、L87 |
| `BattleRoleDownedEvent` | `Core/BattleHealthSystem.cs` L95 |
| `BattlePlayerDiedEvent` | `Core/BattleHealthSystem.cs` L108（**全队阵亡**才发） |
| `BattleRoleSwitchedEvent` | `Core/BattleSystems.cs` L72、L92 |
| `BattleRollTriggeredEvent` | `Core/BattleSystems.cs` L377 |
| `BattleLockEnemyChangedEvent` | `Core/BattleSystems.cs` L909 |
| `BattleReloadCompletedEvent` | `Core/BattleSystems.cs` L1148 |
| `BattleEnemyHitEvent` | `Weapon/BattleBullet.cs` L96（**仅新版敌人实体命中**） |
| `BattleEnemyKilledEvent` | `Enemy/BattleEnemyEntity.cs` L488（**仅玩家造成致死**） |
| `BattleSkillEnergyChangedEvent` | `Skill/BattleSkillSystem.cs` L201 |
| `BattleSkillCooldownChangedEvent` | `Skill/BattleSkillSystem.cs` L336 |
| `BattleLevelStateChangedEvent` | `Level/BattleLevelFlow.cs` L210 |
| `BattleStageWaveStartedEvent` | `Level/BattleStageDirector.cs` L133 |
| `BattleStageClearedEvent` | `Level/BattleStageDirector.cs` L272 |
| `BattleStageTimedOutEvent` | `Level/BattleStageDirector.cs` L288 |
| `BattlePauseToggleRequestedEvent` | `BattleSceneRuntimeHost.cs` L174（ESC，**已重测**）、`Core/BattleSystems.cs` L223（触屏暂停键） |
| `BattlePauseResumeRequestedEvent` | `UI/Panels/Battle_PauseMenuPanel.cs` L55（"继续"按钮） |

### 7.2 订阅点（RegisterEvent）——共 19 处

| 订阅方 | 订阅的事件 |
|---|---|
| `Core/BattleHealthSystem` L28 | `BattlePlayerDamagedEvent` |
| `Core/BattleSystems` L412/418/419/424（View） | `BattleRoleSwitched` / `BattleRollTriggered` / `BattleRoleHealthChanged` / `BattleRoleDowned` |
| `Core/BattleSystems` L1110（Weapon） | `BattleRoleSwitched`（切人补弹） |
| `Level/BattleLevelFlow` L72–75 | `BattleStageWaveStarted` / `StageCleared` / `StageTimedOut` / `BattlePlayerDiedEvent` |
| `Player/Battle_PlayerControl` L122–124 | `BattleRoleSwitched` / `SkillEnergyChanged` / `SkillCooldownChanged`（HUD 文案） |
| `Skill/BattleSkillSystem` L41–43 | `BattleEnemyHit` / `BattleEnemyKilled`（能量）/ `BattleRoleSwitched` |
| `UI/BattleUiSystem` L26–28 | `BattleLevelStateChanged` / `PauseToggleRequested` / `PauseResumeRequested` |

### 7.3 读出两处**悬空/待接**（读代码时容易白找）

- **`BattleLockEnemyChangedEvent`：无任何 `RegisterEvent`** → 目前是"只发不收"（注释说供武器朝向/准星/调试同步，实际世界里的订阅方可能在旧代码或尚缺）。**不是 bug，是未接完。**
- **`BattleReloadCompletedEvent`：无任何 `RegisterEvent`** → 供 T3.3 正式 HUD / 音效消费，**当前只有弹药调试文本在轮询**。

---

## 8. 各功能域调用链（逐条）

### 8.1 输入 → 移动（键鼠/触屏二选一）

```
BattleInputSystem.Tick()
 ├─ SampleVirtual(controller)    // 触屏独占：BattleVirtualInput 虚拟源
 └─ SampleKeyboard(controller)   // 键鼠独占：Input.GetAxis/GetKey
      → 全部写进 State.Input（BattleInputSnapshot）
      → 触屏暂停键 → SendEvent(BattlePauseToggleRequestedEvent)
      → 触屏切人键 → CycleRole(dir)（跳过阵亡成员）

BattleMovementSystem.TickUpdate()   // Update：翻滚/冲刺状态机（CanRoll/PerformRoll）
BattleMovementSystem.TickFixedUpdate() // Fixed：算推力 → 施力到刚体
      → 写 State.Movement.*
      → 翻滚触发时 SendEvent(BattleRollTriggeredEvent)（View 订阅后播动画）
```
**文件**：`Input/BattleVirtualInput.cs`、`Input/Touch*.cs`、`Core/BattleSystems.cs`（`BattleInputSystem` / `BattleMovementSystem`）

### 8.2 锁敌（准星）

```
BattleAimSystem.TickFixedUpdate()          // Fixed
 ├─ FindLockEnemy(controller, out screenTarget)  // 自动吸附
 ├─ Tab → TrySwitchLockedTarget(±1)               // 循环切换/取消
 └─ 变化时 SendEvent(BattleLockEnemyChangedEvent)
BattleAimSystem.TickLateUpdate()           // Late：准星平滑移动
```
候选来源：**`BattleEnemyRegistry.EnumerateTargets()`**（新敌人注册表；旧静态表回退代码已注释）。
**文件**：`Core/BattleSystems.cs`（`BattleAimSystem`）、`Core/BattleCombatTypes.cs`（`BattleEnemyRegistry`）

### 8.3 武器 → 子弹 → 命中（玩家侧）

```
BattleWeaponSystem.Tick()                    // Update
 ├─ 遍历当前角色武器 → TickReload()          // 换弹进度
 └─ State.Input.FireHeld → weaponMode.PressTrigger()
        └─ BattleWeaponModeBase.FireSingleShot(index)
             ├─ ResolvePlayerBulletPrefab()  → BattleElementBulletLibrary（按角色元素取弹，回退武器默认弹）
             ├─ BattleObjectPool.Spawn(prefab, ...)      // T1.3 走池
             ├─ ApplyBulletSpread / ApplyBulletVelocity / ApplyBulletDamage
             └─ 弹道武器另有 BulletLinePrefab（狙击）
  换弹完成 → SendEvent(BattleReloadCompletedEvent)
  切人     → OnRoleSwitched() 补满弹匣

Weapon/BattleBullet.OnTriggerEnter
 ├─ 命中 BattleEnemyEntity → enemy.ApplyElementalDamage(damage, element)
 │     └─ 克制倍率 + 风易伤 → 扣血 → 施加元素状态
 ├─ SendEvent(BattleEnemyHitEvent{Damage, Element})   // 技能能量来源之一
 └─ BattleObjectPool.Despawn / 命中特效
```
**武器 = 4 类 16 子类**（`BattleWeaponModeFactory.Create(WeaponSubType)`）：
`Assault`（5 变体，含 `ExecuteBurstAsync` 连发点射）· `Heavy`（4 导弹变体）· `Scatter`（2 变体，覆写 `OnShootEvent`/`ApplyBulletSpread`）· `Sniper`（4 变体，覆写 `FireSingleShot`）。
**View 适配**：`BattleWeaponViewFactory.Create` → `BattleWeaponViewAdapter`（新组件）或 `LegacyBattleWeaponViewAdapter`（旧组件）。

**文件**：`Weapon/BattleWeaponRuntime.cs`（**614 行**，含视图/工厂/模式基类与 16 子类）、`Weapon/BattleBullet.cs`、`Weapon/BattleElementBulletLibrary.cs`、`Weapon/BattleWeaponObjectComponent.cs`（旧组件，已被适配器桥接）

### 8.4 敌人：实体 + 行为策略 + 头顶 UI

```
BattleEnemyEntity
 ├─ Awake  : 填生命 + EnemyBehaviourFactory.Create(kind) → IEnemyBehaviour
 ├─ Start  : BattleEnemyRegistry.Register(this) + CreateUiIfNeeded() → Battle_EnemyUI
 ├─ Update : 视野检测 → 元素状态着色 → behaviour.TickMove / TickAttack
 ├─ OnDestroy: UnRegister
 ├─ ApplyDamage / ApplyElementalDamage / TakeDirectDamage → ApplyDamageInternal
 │      → ShowDamageText（飘字，走池）+ 元素状态施加（ElementStateEffect.GetOrAdd）
 │      → 致死 → Die() → SendEvent(BattleEnemyKilledEvent{Position, Element})
 ├─ FireBulletAt(yaw)      → 敌人子弹（走池）
 └─ DamagePlayerInRadius() → SendEvent(BattlePlayerDamagedEvent)
```

**4 类敌人 = 4 个纯 C# 策略**（`IEnemyBehaviour`：`TickMove` / `TickAttack` / `OnBeforeDestroy`）：

| `EnemyKind` | 策略类 | 行为 |
|---|---|---|
| `Patrol` | `EnemyBehaviour_Patrol` | 生成点小半径游走 + 单发速射 |
| `GunTurret` | `EnemyBehaviour_GunTurret` | 站桩 + 直线/扇形弹幕参数化（重装变体=高血慢速重弹） |
| `Missile` | `EnemyBehaviour_Missile` | 高速追踪 → 贴近爆炸自毁 |
| `Mine` | `EnemyBehaviour_Mine` | 静置；玩家入感应圈或被击杀 → 大范围爆炸（`OnBeforeDestroy` 幂等引爆） |

**文件**：`Enemy/BattleEnemyEntity.cs`（**500 行**）、`Enemy/EnemyKind.cs`、`Enemy/EnemyBehaviour_*.cs`（4 个）、`Enemy/BattleEnemyBullet.cs`、`Enemy/Battle_EnemyUI.cs`(+Designer)

### 8.5 元素系统

```
攻击方：子弹/技能携带 DamageElement
   ↓
BattleEnemyEntity.ApplyElementalDamage(dmg, attackElement)
   ├─ BattleElementBalance（克制表 1.25×；Electric→Ice→Fire→Wind→Electric）
   ├─ 风易伤倍率
   └─ ElementStateEffect.GetOrAdd(enemy)  → 挂到宿主并施加状态
        ├─ 冰=减速 / 火=DoT / 电=麻痹+溅射 / 风=易伤+聚怪
        └─ NotifyElementVisualChanged() → 刷新身体着色（ApplyVisualMask 优先级）
```
**文件**：`Element/BattleElementBalance.cs`（集中数值）、`Element/ElementStateEffect.cs`、`Enemy/Battle_EnemyUI.cs`（状态小字）

### 8.6 技能（E/Q 三层模型）

```
BattleSkillSystem.Tick()                       // Update
 ├─ EnsureRuntime()                            // 惰性扩容冷却/节流数组
 ├─ TickCooldownBroadcast(roleIndex)           // 整秒变化/就绪翻转才广播（节流）
 └─ State.Input.SkillEPressed / SkillQPressed → TryCast(slot)
        ├─ ComputeReadyState（冷却到 且（极奏）能量达标）
        ├─ 组装 BattleSkillContext{ RoleIndex, Role, Controller, State, Slot, Profile }
        └─ BattleSkillExecutorFactory.Create(RoleElement).CastElemental/CastUltimate(ctx)
                └─ 四元素执行器（Ice/Fire/Wind/Electric）组合公共效果：
                     SkillEffect_AoEBurst（范围爆发）
                     SkillEffect_ElementalBarrage（元素弹幕）
                     SkillEffect_PullEnemies（聚怪）
                     SkillEffect_ClearEnemyBullets（清弹幕）

能量来源（仅玩家造成的）：BattleEnemyHitEvent（少量）→ OnEnemyHit
                          BattleEnemyKilledEvent（大额）→ OnEnemyKilled
                     广播 BattleSkillEnergyChangedEvent / BattleSkillCooldownChangedEvent
```
**文件**：`Skill/BattleSkillSystem.cs`（**446 行**）、`Skill/BattleSkillExecutor.cs`、`Skill/SkillContext.cs`、`Skill/SkillEffect.cs`（公共效果基类）、`Skill/Effects/*`（4 个）、`Skill/Executors/*`（4 个）、`Skill/RoleSkillData.cs`（222 行，`BattleSkillSlot` / `BattleSkillSlotData` / `BattleRoleSkillProfile` / `BattleSkillCatalog`）

### 8.7 关卡流程状态机 + 刷怪

```
BattleLevelFlow（System，状态机）           BattleStageDirector（MonoBehaviour，数据驱动）
  StartLevel() ─────────► Prepare             Setup(level, enemyCatalog)  ← battle_runtime Awake 注入
       ▲ OnWaveStarted                        Start(): BuildSiteMarkers() → ActivateNextSite()
       │   ↑ SendEvent(StageWaveStartedEvent)  Update(): mElapsed++ → 超时 TimeUp()
  Wave                                          → TickCurrentSite():
       │ OnStageCleared                            ├─ 玩家进入 ArriveRadius → SpawnBatch(0) → 首发刷怪
       │   ↑ StageClearedEvent                     │     └─ SendEvent(StageWaveStartedEvent)   ← 只发一次
  Victory                                         └─ PruneAndCountAlive() 达 KillRatio → 下一批
       │ OnStageTimedOut                        CompleteSite() → ActivateNextSite()
  Defeat                                          └─ 全站完成 → SendEvent(StageClearedEvent)
       │ OnPlayerDied（全队阵亡）                TimeUp() → SendEvent(StageTimedOutEvent)
       ▼                                          SpawnEnemies(): EnemyId → ResolveEnemyPrefab(Catalog) → prefab → 位置换算
Exit ◄─ Restart(): BattleSceneGate.ReloadCurrent(UnloadFirst)     状态转移都广播 BattleLevelStateChangedEvent
```

`BattleLevelFlow` 订阅 4 个事件（Wave/Cleared/TimedOut/PlayerDied），每次合法转移（From≠To）发 `BattleLevelStateChangedEvent`；终局调 `TakeOverControl()` 停操作。

**文件**：`Level/BattleLevelFlow.cs`、`Level/BattleLevelEvents.cs`、`Level/BattleStageDirector.cs`（430 行）、`Level/BattleLevelConfig.cs`（流程侧标识字段）。（`BattleSceneRuntimeHost.cs` 已不在 `Level/`，而位于 `03_Battle/` 根目录，见 §9.1）

### 8.8 UI：面板开合与暂停

```
BattleUiSystem（System）
 ├─ OnInit: 订阅 BattleLevelStateChanged / PauseToggleRequested / PauseResumeRequested
 ├─ OnLevelStateChanged(e) → Victory/Defeat 时 UiPanelFlow.TryOpen<Battle_LevelResultPanel>(...)
 │                           其它状态 → CloseResultIfOpen()
 ├─ TogglePause() → PauseGame(): Time.timeScale=0 + State.Paused=true + 打开 Battle_PauseMenuPanel
 └─              → ResumeGame(): timeScale=1 + Paused=false + 关面板
暂停面板「继续」→ SendEvent(BattlePauseResumeRequestedEvent) → 回到 BattleUiSystem
结算面板「退出」→ BattleSceneGate.TransitionTo(BattleTeamSession.ReturnSceneName)
```
面板本身是标准 QFramework `UIPanel`（CodeGen partial + Designer），**经 ResKit AB 加载**（`UiPanelFlow.TryOpen<T>(level, data, bundleName, prefabName)`）。

**文件**：`Core/BattleUiSystem.cs`、`UI/Panels/Battle_{LevelResult,PauseMenu,TouchInput}Panel.cs`(+Designer)、`UI/Panels/Battle_MainPanel.cs`(+`Designer`/`Presentation`)、`UI/BattleTeamHudView.cs`（250 行，队伍血条 HUD）

### 8.9 对象池

```
BattleObjectPool（静态分桶池）
  Spawn(prefab, pos, rot[, parent])   ← 子弹/敌人子弹/飘字/命中特效/爆炸环/弹道线
  Despawn(instance)                    ← 命中/超时回池
  ReturnAfter(instance, delay)         ← 纯表现对象（狙击弹道线）
  DespawnAllActive()                   ← 战斗结束/重开前清场
  WarmUp(prefab, count)                ← 预热
  DumpStats()                          ← F8 打印
细节：ResetRigidbody / CancelAutoReturn / ClearPooledTrail（清拖尾防一帧残留）
      BattlePoolAutoReturn(Update) 负责延迟回收计时
```
**文件**：`Pool/BattleObjectPool.cs`（313 行）、`Pool/BattlePoolAutoReturn.cs`

### 8.10 桥接层（旧表现兼容）—— **已退役（2026-09-19，类与文件已删除）**

> ⚠️ **本节原描述的 `Battle_PlayerControl.SyncRuntimeBridge()` → `BattleRuntimeBridge.UpdateState(...)` 链路已不存在**，保留此处只为给旧笔记一个落点。`BattleRuntimeBridge` 历经两批退役，第 2 批（`56c249bf`）把它的最后一个成员也拔掉后**整类连同 `Data/BattleRuntimeBridge.cs`（末态 61 行）+ `.meta` 一并删除**——`Data/` 现在只剩 `BattleRuntimeData.cs`。

**退役两批**（`git log --oneline -- Assets/02_Scripts/03_Battle/Data/BattleRuntimeBridge.cs` 可复现）：

| 批次 | 拔掉的成员 | 去处 |
|---|---|---|
| 第 1 批（`3eca3fd1`） | `Camera` / `EnvironmentEffectController` / `ActiveElement` / `LockEnemyTransform` / `LockSpeed` | `BattleStateModel` 的派生只读属性（`BattleCamera` / `EnvironmentEffects` / `CurrentDamageElement`）；`LockEnemyTransform`+`LockSpeed` 随旧武器组件自驱 `Update` 退役而**失去全部读者**（删） |
| 第 2 批（`56c249bf`） | 最后剩下的 `PlayerTransform`（`3eca3fd1~1` 时该文件 91 行 / 7 参签名，末态 61 行 / 只剩 `UpdateState(owner, playerTransform)`） | **`IBattlePlayerTarget` + `BattlePlayerRegistry`**（`Core/BattleCombatTypes.cs`，与敌人侧对称） |

```
读玩家（取代 BattleRuntimeBridge.PlayerTransform / 宿主每帧喂镜像）
    var player = BattlePlayerRegistry.CurrentTransform;   // 内部先判存活；玩家不存在或已销毁 → null
    if (!player) return;                                  // 读者侧写法与旧桥接基本一致
注册 / 注销（rig 自己，不再由宿主喂）
    Battle_PlayerControl.Awake     → BattlePlayerRegistry.Register(this)
    Battle_PlayerControl.OnDestroy → BattlePlayerRegistry.UnRegister(this)   // ReferenceEquals 身份守卫＝旧 owner 守卫
```

**8 处读者 / 6 个文件**（实测 `Select-String BattlePlayerRegistry`，行号为改后现场）：`Level/BattleStageDirector.cs` L386（`IsPlayerWithin`）、`Enemy/EnemyBehaviour_Patrol.cs` L54、`Enemy/EnemyBehaviour_Missile.cs` L19 + L37、`Enemy/EnemyBehaviour_Mine.cs` L23、`Enemy/EnemyBehaviour_GunTurret.cs` L25、`Enemy/BattleEnemyEntity.cs` L364（`FireBulletAt`）+ L388（`DamagePlayerInRadius`）。

> 📌 **旧文写"7 处读者"是错的**（`BattleRuntimeBridge` 类注释里也是 7）——实测是 **8 处**，因为 `EnemyBehaviour_Missile` 有 2 处。按文件数是 6 个。

**与旧桥接的关键差别**：旧病根是"**由外部每帧喂**"（`SyncRuntimeBridge`）+ 静态类硬引用 rig 具体类型；新注册表把所有权还给 rig 自己，`Core/` 只按接口持有，读到的永远是"活着"的玩家（复用 09 关键坑 13 的 `IsAlive` 封装：接口持有 `UnityEngine.Object` 时**判空必须用 `BattleCombatUtility.IsAlive(this IBattlePlayerTarget)`**，直接 `!= null` 会拿到"伪非空"的已销毁对象）。

**文件**（末态）：`Core/BattleCombatTypes.cs`（**321 行**，含 `IBattlePlayerTarget` / `BattlePlayerRegistry` / `IsAlive(IBattlePlayerTarget)` 重载）、`Actor/Battle_PlayerControl.cs`（**163 行**，`Awake` 自注册 / `OnDestroy` 自注销，`SyncRuntimeBridge()` 方法本身已删）。`Data/BattleRuntimeBridge.cs` **不再存在**。

---

## 9. 文件清单（**这就是"目录"**）

> 行数为实测；「职责」取自类注释与实测调用点。

### 9.1 `BattleArchitecture.cs`（47 行）— 从这开始
架构入口，注册 1 Model + 10 System（顺序即 §5 表）。同目录（`03_Battle/` 根，共 2 个文件 / 357 行）还有 **`BattleSceneRuntimeHost.cs`（310 行）** = 场景入口 `battle_runtime`，见 §2。

### 9.2 `Core/`（6 个文件 / 1931 行）— 状态与事件的心脏
| 文件 | 行 | 职责 |
|---|---|---|
| `BattleSystems.cs` | **733** | **10 个 System 里 5 个的实现**：Input / Movement / Camera / Aim / Weapon（Role 已拆到 `Role/`，View 已删）。**最重的一个文件**；`BattleWeaponSystem` 的 `PublishAmmo` 在变更点发布弹药读数（2026-09-19） |
| `BattleStateModel.cs` | 275 | 共享状态黑板 + `BattleInputSnapshot` + `BattleMovementState`；`BattleHudState` 已删除，HUD 字段平铺于此 |
| `BattleSetupSystem.cs` | **247** | **2026-09-19 新增（宿主瘦身第 2 步）**：入场数据解析——`TryResolveLevel` + `ResolveBattleDatas` → `BattleTeamResolution`。无状态，场景固件按次传参；**解析失败时自置 `selected = null`**（`248df6ca`） |
| `BattleCombatTypes.cs` | 321 | `IBattleEnemyTarget` / `IBattleWeaponView` / `BattleCombatUtility`（含 `IsAlive`）/ `BattleEnemyRegistry` / **`IBattlePlayerTarget` + `BattlePlayerRegistry`**（2026-09-19 桥接层退役时新增，见 §8.10） |
| `BattleEvents.cs` | 199 | 13 个强类型事件结构（`BattleEvent_*`；较 2026-09-18 的 12 个新增 `BattleEvent_ControlTakeOverRequested`，见 §7 状态注） |
| `BattleUiSystem.cs` | 156 | 结算/暂停面板开合 + 暂停切换（System；2026-09-19 自 `UI/` 迁入，`UI/` 现为纯 View 层） |

### 9.3 `Level/`（4 个文件 / 737 行）
| 文件 | 行 | 职责 |
|---|---|---|
| `BattleStageDirector.cs` | 430 | 波次/站点/批次/刷怪 + 4 个关卡事件发送；`IsPlayerWithin` 读 `BattlePlayerRegistry.CurrentTransform` |
| `BattleLevelFlow.cs` | 228 | 关卡状态机 + 事件订阅 + 重开/退出 |
| `BattleLevelEvents.cs` | 56 | 关卡事件 + 暂停事件（6 个事件结构） |
| `BattleLevelConfig.cs` | 23 | 流程侧标识字段（SO） |

> `BattleSceneRuntimeHost.cs` 已移出本目录、位于 `03_Battle/` 根，现 **310 行**（旧文记 418 行、瘦身前实测 559 行，见 §9 末注）——见 §9.1。

### 9.4 `Actor/`（7 个文件 / 669 行）— 原 `Player/`：Actor 与装配器驻地
| 文件 | 行 | 职责 |
|---|---|---|
| `BattleRoleActor.cs` | 202 | 角色/武器实例挂点、显隐、动画、模型朝向（自订阅切人/阵亡/翻滚）。`Tick(animationVelocity, sprinting, deltaTime)` 内聚 `SetMovement(...)` 与跟随视角旋转（`ClampLookRotation(camera.transform.forward)`，无相机回落 `yawAxis.rotation`）；**Model 构造期注入**（`fcb43173`：`Actor/` 内已无 `GetModel<` 运行期查找）；纯 C# 类，靠 `Dispose()` 反注册 |
| `Battle_PlayerControl.cs` | 163 | rig = **纯 View**：Designer 引用 + 工具方法 + 实现 `IBattlePlayerTarget`（`RootTransform`）。**技能 HUD 已于 2026-09-18 迁出**（见 `UI/Panels/Battle_MainPanel.cs`）；`SyncRuntimeBridge()` 已随桥接层删除，改为 `Awake` `BattlePlayerRegistry.Register(this)` / `OnDestroy` `UnRegister(this)`（§8.10）。Designer 上的 `SkillHud*` 字段仍保留，待 rig prefab 上的 HUD 对象删除后由 CodeGen 一并移除 |
| `BattleSceneAssembler.cs` | **139** | **2026-09-19 新增（宿主瘦身第 4 步）：场景组合根**——普通 C# 类（非 System），`Build(level)` 按序装配，只读暴露 4 个 Actor，见 §4 |
| `BattleWeaponActor.cs` | 66 | 武器瞄准表现（**不接管发射/弹药/投射物**）。`Tick()` 内聚三个中间量：锁敌目标（`state.LockEnemy`，经 `IsAlive()`）、自由瞄准点（相机位置 + 前向 ×10，回落 `yawAxis` 再回落 `root`）、吸附速度（`config.LockEnemySpeed × Mathf.Max(1f, state.LockSpeedMultiplier)`），随后 `Aim(...)` |
| `BattleMovementActor.cs` | 51 | 移动表现：`Forward` / `AnimationVelocity` / `IsAvailable` / `ResolveDirection` / `ApplyForce` / `Teleport` |
| `Battle_PlayerControl.Designer.cs` | 28 | CodeGen 生成：相机/枪口/Canvas/子弹 prefab/动画等序列化引用 |
| `BattleCameraActor.cs` | 20 | 相机表现（FOV 混合 `BlendFov`） |

### 9.5 `Weapon/`（4 个文件 / 897 行）
| 文件 | 行 | 职责 |
|---|---|---|
| `BattleWeaponRuntime.cs` | **614** | 武器视图工厂 + 适配器 + 模式工厂 + `BattleWeaponModeBase` + **16 个模式子类** |
| `BattleBullet.cs` | 163 | 玩家子弹（独立文件，供 prefab 序列化挂载）；命中 → `ApplyElementalDamage` + `BattleEvent_EnemyHit` |
| `BattleWeaponObjectComponent.cs` | 69 | **旧武器组件**（过渡期保留，经适配器桥接；即原 `Weapon_ObjectComponent.cs`） |
| `BattleElementBulletLibrary.cs` | 51 | 按角色元素注册/取用 4 种元素子弹 prefab |

### 9.6 `Enemy/`（9 个文件 / 1196 行）
| 文件 | 行 | 职责 |
|---|---|---|
| `BattleEnemyEntity.cs` | 500 | 敌人实体：生命/元素伤害/飘字/死亡/子弹/范围伤害 + 实现 `IBattleEnemyTarget` |
| `Battle_EnemyUI.cs` | 211 | 头顶 UI（QFramework View：血条/Sing 切图/状态小字） |
| `BattleEnemyBullet.cs` | 119 | 敌人子弹（走池）；命中玩家 → `BattlePlayerDamagedEvent` |
| `EnemyBehaviour_Patrol.cs` | 84 | 巡弋者行为策略 |
| `EnemyBehaviour_GunTurret.cs` | 71 | 站桩炮台行为策略 |
| `EnemyBehaviour_Missile.cs` | 67 | 自爆导弹行为策略 |
| `EnemyKind.cs` | 65 | `EnemyKind` 枚举 + `IEnemyBehaviour` + `EnemyBehaviourFactory` |
| `EnemyBehaviour_Mine.cs` | 50 | 自爆地雷行为策略 |
| `Battle_EnemyUI.Designer.cs` | 29 | CodeGen 生成 |

### 9.7 `Skill/`（13 个文件 / 1231 行）
| 文件 | 行 | 职责 |
|---|---|---|
| `BattleSkillSystem.cs` | 446 | 能量 + 冷却 + 施放管道（System） |
| `RoleSkillData.cs` | 222 | `BattleSkillSlot` / `BattleSkillSlotData` / `BattleRoleSkillProfile` / `BattleSkillCatalog` |
| `SkillEffect.cs` | 172 | 公共效果基类（AoE 的共用实现） |
| `SkillContext.cs` | 83 | 施放上下文（只读输入，含 `LockTarget`/`AimForward`/`FirePoint`） |
| `Effects/SkillEffect_PullEnemies.cs` | 61 | 风：聚怪 |
| `Effects/SkillEffect_ElementalBarrage.cs` | 59 | 元素弹幕 |
| `Effects/SkillEffect_AoEBurst.cs` | 44 | 范围爆发 |
| `BattleSkillExecutor.cs` | 38 | `BattleRoleSkillExecutor` 抽象 + 工厂 |
| `Executors/SkillExecutor_Wind.cs` | 24 | 风执行器 |
| `Executors/SkillExecutor_Ice.cs` | 23 | 冰执行器 |
| `Executors/SkillExecutor_Electric.cs` | 22 | 电执行器 |
| `Executors/SkillExecutor_Fire.cs` | 21 | 火执行器 |
| `Effects/SkillEffect_ClearEnemyBullets.cs` | 16 | 清弹幕 |

### 9.8 `Element/`（2 个文件 / 294 行）
`ElementStateEffect.cs`（221，状态组件：冰减速/火 DoT/电麻痹溅射/风易伤聚怪）、`BattleElementBalance.cs`（73，克制/状态数值集中处）

### 9.9 `UI/`（10 个文件 / 1095 行）— 纯 View 层（System 与 HUD 状态已迁出）
| 文件 | 行 | 职责 |
|---|---|---|
| `BattleTeamHudView.cs` | 250 | 队伍血条 HUD（事件驱动；由 `Battle_MainPanel` 在 Presentation 层创建，原 `BattleViewSystem.BuildTeamHud` 已不存在） |
| `Panels/Battle_MainPanel.cs` | 205 | **战斗主 HUD 面板**：技能行渲染 + `BindableProperty` 反向注册；`TryOpenForBattle` / `CloseForBattle` / `TickForBattle`（宿主 LateUpdate 调用） |
| `Panels/Battle_TouchInputPanel.cs` | 134 | 触屏操作层（UIKit UIPanel） |
| `Panels/Battle_MainPanel.Presentation.cs` | 127 | 主面板的 View 侧表现：准星 Lerp、队伍 HUD、血量/弹药刷新与节流 |
| `Panels/Battle_PauseMenuPanel.cs` | 80 | 暂停菜单面板 |
| `Panels/Battle_LevelResultPanel.cs` | 76 | 结算面板 |
| `Panels/Battle_MainPanel.Designer.cs` | 76 | CodeGen：`Cross_LockEnemy` / `Health_Main` / `Health_Value` / `SkillHud*` 等字段 |
| `Panels/Battle_{LevelResult,PauseMenu}.Designer.cs` | 52 / 52 | CodeGen 生成 |
| `Panels/Battle_TouchInputPanel.Designer.cs` | 43 | CodeGen 生成 |

> **面板开合归属**：主 HUD 与触屏层由 **`battle_runtime`**（`OpenBattleUiWhenSceneReady`）打开、`OnDestroy` 关闭；结算/暂停由 **`BattleUiSystem`**（现居 `Core/`）按事件开合。
> 本目录原有 `BattleUiSystem.cs`（迁 `Core/`）与 `BattleHudState.cs`（已删除，字段平铺进 `BattleStateModel`）两项，均已不在 `UI/`。

### 9.10 `Pool/`（2 个文件 / 339 行）
`BattleObjectPool.cs`（313）、`BattlePoolAutoReturn.cs`（26）

### 9.11 `Input/`（5 个文件 / 455 行）
`BattleVirtualInput.cs`（184，虚拟输入源）+ `TouchHoldButton.cs`（93）/ `TouchJoystick.cs`（74）/ `TouchTapButton.cs`（64）/ `TouchDragLookZone.cs`（40）——4 个触屏控件都只写 `BattleVirtualInput`

### 9.12 `Data/`（1 个文件 / 140 行）
`BattleRuntimeData.cs`（140，`BattleRoleRuntimeData` = 角色运行时数据 + 血量/阵亡）。
> `BattleRuntimeBridge.cs`（末态 61 行）已于 2026-09-19 整类删除（`56c249bf`），`Data/` 从 2 文件 / 201 行变为 1 文件 / 140 行；玩家目标入口现为 `Core/BattleCombatTypes.cs` 的 `BattlePlayerRegistry`（§8.10）。

### 9.13 `Effect/`（1 个文件 / 147 行）
`BattleEnvironmentEffectController.cs`（伤害飘字，走池）

### 9.14 `Config/`（1 个文件 / 96 行）
`BattlePlayerConfig.cs`（+ `.asset`；`MainFov`/`SprintFov`/`MoveForwardFollowView`/`LockEnemySpeed` 等手感参数）

### 9.15 `Role/`（3 个文件 / 312 行）— 原 `Team/`：角色系统驻地
`BattleRoleSystem.cs`（156，角色初始化/切人/阵亡自动切人/接管控制）、`BattleRoleSystem.Health.cs`（86，`partial`：成员级血量/受击/治疗/阵亡/全灭）、`BattleTeamRuntimeBuilder.cs`（70，会话 id → `BattleRoleRuntimeData[]`）

> **目录合计（2026-09-19 重测，最后一次在 `56c249bf` 之后；口径同上：`03_Battle/` 不含 `Contracts/` 与 `Editor/`）**：根(357 = `BattleArchitecture.cs` 47 + `BattleSceneRuntimeHost.cs` 310) + Actor(669) + Config(96) + Core(1931) + Data(140) + Effect(147) + Element(294) + Enemy(1196) + Input(455) + Level(737) + Pool(339) + Role(312) + Skill(1231) + UI(1095) + Weapon(897) = **70 个文件 / 9896 行**（旧 `Team/`、`Player/` 已分别改名 `Role/`、`Actor/`；`BattleUiFlowPanelOpener.cs` / `BattleUiFlowNames.cs` 已删除）
> 说明（第三轮差值，`248df6ca` → `56c249bf`，**净 +74 行 / −1 文件**，逐文件实测 `git diff` 后的行数）：**删除 `Data/BattleRuntimeBridge.cs`（61 行，桥接层整类退役）**；`Core/BattleCombatTypes.cs` 239 → 321（**+82，新增 `IBattlePlayerTarget` + `BattlePlayerRegistry`**）；`Core/BattleSystems.cs` 705 → 733（**+28，弹药读数改「变更点发布」**）；`BattleSceneRuntimeHost.cs` 314 → 310（**−4，删掉 4 处 `SyncRuntimeBridge()`**）；`Actor/Battle_PlayerControl.cs` 148 → 163（+15，实现接口 + 自注册/自注销）、`Actor/BattleSceneAssembler.cs` 140 → 139（−1，删同步调用）；`Core/BattleEvents.cs` 181 → 199（+18）、`Level/BattleLevelFlow.cs` 230 → 228（−2）、`Role/BattleRoleSystem.Health.cs` 87 → 86（−1）＝步骤 5「控制接管改事件驱动」。
> 说明（上一轮差值）：与再上次统计（66 / 9673）的差值是两轮改动的叠加——**2026-09-19 宿主瘦身四步及其后续两个提交**（新增 `Core/BattleSetupSystem.cs` 与 `Actor/BattleSceneAssembler.cs`；宿主 **559 → 314** 行；`BattleHealthSystem` 并入 `Role/BattleRoleSystem.Health.cs`；`BattleUiSystem` 迁入 `Core/`；`BattleRoleActor` 构造期注入 Model）与**同期武器/桥接/面板清理**（`BattleWeaponRuntime` 703 → 614、`BattleRuntimeBridge` 91 → 61、旧武器组件改名、主面板拆出 `Presentation` 等）。逐项归属未逐一追溯。
> ⚠️ 本文件 §2 旧文写的宿主「418 行」是更早的口径：瘦身前基线（`1518dfc4`）实测为 **559 行**（`git show 1518dfc4:Assets/02_Scripts/03_Battle/BattleSceneRuntimeHost.cs` 计 559）。

---

## 10. 推荐阅读顺序（每步要回答的问题）

| 步 | 读什么 | 读完要能回答 |
|---|---|---|
| 1 | `BattleArchitecture.cs`（47 行） | 战斗由哪些系统组成？谁先注册？ |
| 2 | `Core/BattleStateModel.cs`（275） | 各系统共享哪些状态？输入快照有哪些字段？ |
| 3 | `Core/BattleEvents.cs` + `Level/BattleLevelEvents.cs`（199 + 56 = 255） | 有哪些事件？语义边界在哪（比如"仅玩家击杀才发 Killed"）？ |
| 4 | `Core/BattleCombatTypes.cs`（321） | 三个关键接口（敌人目标 / 玩家目标 / 武器视图）是什么？为什么必须用 `IsAlive`？ |
| 5 | **`BattleSceneRuntimeHost.cs`（310）** + `Core/BattleSetupSystem.cs`（247）+ `Actor/BattleSceneAssembler.cs`（139） | 一场战斗怎么被装配起来？每帧按什么顺序跑？数据从哪来？（三者依次是相位表 / 数据解析 / 装配链） |
| 6 | `Core/BattleSystems.cs`（**733**，5 个 System） | 输入/移动/视角/相机/锁敌/武器各自怎么实现？弹药读数在哪发布？ |
| 7 | `Level/BattleLevelFlow.cs`（228）+ `Level/BattleStageDirector.cs`（430） | 关卡状态怎么流转？波次怎么刷？ |
| 8 | `Weapon/BattleWeaponRuntime.cs`（614） | 4 类 16 子类怎么组织？开火一发子弹经历什么？ |
| 9 | `Enemy/BattleEnemyEntity.cs`（500）+ `Enemy/EnemyKind.cs` | 敌人怎么组织？4 类行为怎么插拔？ |
| 10 | `Skill/BattleSkillSystem.cs` + `SkillContext.cs` + `Executors/` | E/Q 三层模型怎么组装？能量怎么来？ |
| 11 | `Element/` + `UI/` + `Pool/` | 元素状态、面板开合、对象池各自边界 |
| 12 | `Core/BattleCombatTypes.cs` 的 `BattlePlayerRegistry` | 玩家目标怎么被找到？——**已解决（2026-09-19）**：rig 在 `Awake` 自注册、`OnDestroy` 自注销，8 处读者读 `BattlePlayerRegistry.CurrentTransform`（内部做存活判空）。接口持有的 `UnityEngine.Object` 判空**必须走 `BattleCombatUtility.IsAlive`**。旧的 `Data/BattleRuntimeBridge.cs` **已整体删除**，见 §8.10 |

> **不必通读的**：`Weapon/BattleWeaponObjectComponent.cs`（旧武器组件）、`*.Designer.cs`（CodeGen 产物，看字段即可）。（原表内 `Team/BattleUiFlow*.cs` 死代码与 `Sensor/*.Designer.cs` 均已不存在——2026-09-19 实测全 `Assets` 下无该文件与目录。）

---

## 11. 读的时候一定会困惑的地方（实测，先看这里）

1. **`Battle_PlayerControl` 没有 `Update`/`Start`** —— 不是漏写，是 2026-09-08 上移到 `battle_runtime` 了（关键坑 24）。它现在只有 `Awake`（设 timeScale + 订阅控制接管请求事件 + **自注册玩家目标** `BattlePlayerRegistry.Register`）和 `OnDestroy`（**注销玩家目标** / 清池 / 反注册事件）。
2. **`BattleSystems.cs` 一个文件装 5 个 System** —— 不是笔误，是历史演进结果（逐卡加系统时没有拆文件）；Role 已拆到 `Role/BattleRoleSystem.cs`(+`.Health.cs`)，View 已删除。它对 09 关键坑 10（"prefab 挂载的 MonoBehaviour 必须独立文件"）**不适用**，因为这些是纯 System 类、从不出现在资源上。
3. **`BattleArchitecture` 类注释说"未通读、能用就行"** —— 这句是本次任务存在的原因，别当成设计意图。
4. **`BattleEnemyRegistry.EnumerateTargets()` 里有一段注释掉的旧敌人回退** —— 是"新旧双轨"的残骸，现在新版敌人列表非空就 `yield break`，回退分支永不可达。
5. **`BattleCombatTypes.cs` 里 `LegacyBattleEnemyTarget` 整个类被注释掉** —— 同上，旧敌人适配器已废。
6. **两个事件"只发不收"**（`BattleLockEnemyChangedEvent`、`BattleReloadCompletedEvent`）—— 见 §7.3，是未接完而不是坏了。
7. **`#if UNITY_EDITOR` 段落会改变运行行为**（`ResolveBattleDatas`、`ResolveTeamCatalog`、`BattleWeaponRuntime`）—— **编辑器全绿 ≠ 真机可行**，这正是关键坑 28/30 的成因。
8. **`BattlePauseToggleRequestedEvent` 有两个发出方**（ESC 在 `battle_runtime`、触屏暂停键在 `BattleInputSystem`），但只有一个订阅方（`BattleUiSystem`）——查暂停问题时要同时看这两条入口。
9. **`Time.timeScale` 暂停 vs `State.Paused`** —— 两个都要设/清，只改一个会出现"画面停了但输入还在"或"时间恢复但面板还在"。
10. **`BattleObjectPool` 三个回池重置动作各有原因**（`ResetRigidbody` / `CancelAutoReturn` / `ClearPooledTrail`）——删任何一个都会复现已修过的 bug，别"顺手清理"。
11. **`Battle_EnemyUI` 是 QFramework ViewController**（`EnemyEntity_UI` 重构产物），不是普通 MonoBehaviour。
12. **`BattleStageDirector` 有自己的 `Start/Update`**，与 `battle_runtime` 是**两个独立 MonoBehaviour 各自跑**——波次推进不在 `battle_runtime` 的驱动链里，只是通过事件回传。
13. **技能 HUD 已不在 rig 上（2026-09-18）**——它搬到了 `Battle_MainPanel`。所以：① 看 `Battle_PlayerControl` 找不到 HUD 代码是正常的（本类只剩 Designer 引用与工具方法）；② rig prefab 上那批 `SkillHud*` 文本对象**已成孤儿**（没有代码写它们，会冻结显示旧文本），删掉它们之前上屏会看到两套 HUD；③ rig 的血条/准星/弹药**已不由任何 System 直写**——`BattleViewSystem` 已于 2026-09-19 删除，现在由宿主 `TickActors()` 驱动的 4 个 Actor + `Battle_MainPanel`（`BattleStateModel` 的 `BindableProperty`）承担。
14. **`Battle_MainPanel` 是 UIKit 面板，由 `battle_runtime` 打开，不是由 `BattleUiSystem`**——而结算/暂停面板由 `BattleUiSystem` 打开。查"面板为什么不出现"时先分清归属：主 HUD/触屏看 `battle_runtime`，结算/暂停看 `BattleUiSystem`。
15. **搜不到 `BattleRuntimeBridge` / `SyncRuntimeBridge` 是正常的（2026-09-19）**——桥接层（含 `Data/BattleRuntimeBridge.cs`）**已整类删除**，"找玩家"改走 `BattlePlayerRegistry.CurrentTransform`（rig 自注册、`OnDestroy` 自注销），宿主相位表里也没有"同步桥接"这一步了。详见 §8.10；接口判空必须用 `BattleCombatUtility.IsAlive`（同 §6 的 `LockEnemy` 纪律）。

---

## 12. 本次未做 / 未验证

- **2026-09-19 宿主瘦身已提交 6 个提交，但 Unity 侧验证未做**：`9b4e7e69`（rig 由 Model 唯一持有）/ `a77eb347`（数据管道搬入 `BattleSetupSystem`）/ `69f851ab`（Actor `Tick` 内聚中间量）/ `50e509b5`（抽 `BattleSceneAssembler`）/ `fcb43173`（`BattleRoleActor` 改构造期注入 Model）/ `248df6ca`（**修一处搬运引入的真实缺陷**：`Awake` 忽略 `TryResolveLevel` 的返回值时，错误的会话关卡会被注入 Director——现改为失败即 `level = null`，System 侧也自置 `selected = null`）。宿主 **559 → 314 行**（第 4 步完成时 309，缺陷修复 +5；`56c249bf` 再删 4 处 `SyncRuntimeBridge()` 后为 **310 行**）；**Unity 侧编译与 Play 回归尚未执行**——本轮验证只是「离线整程序集编译校验通过」（Unity 自带 Roslyn + 引用程序集整程序集编译，非猜测）。Play 验收清单见 21 文档 §8.5，逐条证据见 21 文档 §8.2。**本文 §2 / §3 / §4 / §5 / §6.1 / §9 / §10 的行数与归属是 2026-09-19 对源码重测的结果（最后一次在 `56c249bf` 之后），仍未在 Editor 里跑过。**
- **2026-09-19 第四批（`821547ae` 步骤 5 控制接管事件化 / `3fdb3417` 弹药改变更点发布 / `56c249bf` 桥接层整类退役）**：本轮改动**只做了离线整程序集编译（编辑器 / player 各 0 error CS）**——`powershell -File Build\_check\CheckBattle.ps1`（真实 csc + 真实引用程序集，能查出跨文件签名不一致与 `#if` 分支写错）；**Unity 侧导入编译与 Play 回归均未做**（`Library/ScriptAssemblies/TeaGame.Battle.dll` 是否已随这批源码重编译未验）。Play 待验重点：4 类敌人找玩家（游走者开火 / 炮台射程 / 导弹追踪+引爆 / 地雷感应引爆）、站点到达判定（`BattleStageDirector.IsPlayerWithin`）、重开本关无 `MissingReferenceException`、弹药 HUD 是否"同帧可见"（原先慢一帧）。
- **`248df6ca` 的失败路径修复本身未经验证**：只做了静态判读（读 `Awake` 与 `TryResolveLevel` 的失败分支），**没有构造"会话关卡属于别的美术场景 + catalog 为空"的运行时场景去复现**——这一点属「未验证」。
- **未通读的实现体**：`BattleSystems.cs`（**733 行**，现 5 个 System；2026-09-19 弹药发布改动的那段已逐行读）只读了全部方法签名 + 与驱动链相关的段落；`ElementStateEffect`、4 个 `EnemyBehaviour_*`、`BattleWeaponModeBase` 及 16 个子类的**方法体未逐行读**——本文对这些的描述来自类注释 + 调用点。
- **§7 事件总线的行号与事件名未重测**（`BattleEvent_*` 改名 + 行号整体位移），已在 §7 就地标注；§6.2 / §6.3 中的"View"读取方标签也是迁移前的写法（`BattleViewSystem` 已删除）。
- **`BattleStateModel` 的"谁写谁读"表**是按方法职责推断的，**未逐行核对**（§6 已标注）。
- **未验证运行时行为**：本文全部结论来自源码静态分析 + `Editor.log`；**Play 模式主链路回归仍未做**（见 17 文档 §5）。
- **`Assets/03_Module`、`Assets/04_Prefabs/Enemy` 等资源侧**未纳入本文范围。

---

*回填规则：S4/S5 对 `03_Battle` 做任何搬迁或删码后更新 §9；发现新的调用链事实更新 §8。*
