# Battle MVC 职责收口：分批推进方案

> 状态：方案稿（2026-09-21）；**B0 基线已完成（§12.1）；B1 代码完成、离线编译通过、Play 待验（§12.2）**；B2–B6 未开始  
> 范围：仅 `Assets/02_Scripts/03_Battle` 运行时代码；不改 Menu、Gacha、非 Battle 业务。  
> 依据：`17_代码现状地图.md`、`18_Battle调用链与阅读目录.md`、`19_表现层分层_View与Actor_设计.md`、`21_战斗场景装配与宿主瘦身_设计与落地指导.md`。

## 1. 目标与边界

本专项解决的是 MVC 职责错位，不以“减少文件数”或“全面重写”为目标。最终边界如下：

- **Model**：保存战斗事实、配置和可序列化运行时状态；不创建 GameObject，不访问 UI 坐标，不持有具体 View。
- **System / Controller**：执行业务规则、状态迁移和调度；通过接口、事件或数据结果驱动表现，不直接操作具体 UI/Transform。
- **View**：显示数据并转发输入；不决定伤害、波次、死亡和胜负规则。
- **Actor**：将 System 的结果施加到场景对象，负责 Transform、Animator、特效和对象表现；不拥有战斗规则。
- **Host / Assembler**：只负责生命周期、装配和 Tick 顺序，不承载领域规则。

以下内容本轮明确不做：

- 不改变伤害公式、元素克制、技能效果和武器数值；
- 不重做 QFramework、UIKit、ResKit；
- 不把 `BattleStateModel` 机械拆成多个 Model；
- 不在没有 Editor 的情况下宣称 prefab、场景、AB 或 Play 验收完成。

## 2. 批次总览

| 批次 | 主题 | 主要对象 | 风险 | 依赖 |
|---|---|---|---|---|
| B0 | 基线与保护 | 全 Battle | 低 | 无 |
| B1 | 敌人实体职责收口 | `BattleEnemyEntity`、敌人 UI、Registry | 高 | B0 |
| B2 | 关卡导演职责收口 | `BattleStageDirector`、LevelFlow、Spawner、Marker | 高 | B0，可与 B1 分支开发 |
| B3 | 武器运行时拆分 | `BattleWeaponRuntime`、WeaponSystem、Projectile | 中高 | B0 |
| B4 | 瞄准数据下沉 | `BattleStateModel`、`BattleAimSystem`、`Battle_MainPanel` | 中 | B3 之后更易验证 |
| B5 | Battle UI 入口收口 | `BattleSceneRuntimeHost`、Panel 入口 | 中 | B1、B4 |
| B6 | 文件与依赖清理 | `BattleSystems.cs`、死代码、图谱 | 低 | B1–B5 |

每个批次完成后单独提交，禁止把多个批次混成一个无法回滚的“大重构”。

## 3. B0：基线与保护

### 工作内容

1. 记录 `03_Battle` 运行时代码文件数、行数、程序集编译状态。
2. 备份当前 `code-atlas.generated.json` 和布局文件。
3. 记录以下入口和行为：
   - `BattleSceneRuntimeHost.Start/Update/FixedUpdate/LateUpdate`；
   - 敌人生成、受击、死亡、头顶 UI；
   - 波次开始、清场、超时、胜负；
   - 武器开火、换弹、子弹回收；
   - 瞄准、锁敌、准星显示。

### 验收

- 静态扫描无新增跨程序集引用；
- 形成一份可对照的基线统计；
- 不修改运行时代码。

## 4. B1：敌人实体职责收口（最高优先级）

### 目标

让 `BattleEnemyEntity` 只代表敌人的运行时实体和战斗状态，移除其中的 UI 创建、相机投影和表现控制。

### 建议拆分

```text
Enemy/BattleEnemyEntity.cs
  血量、元素状态、受击、死亡、目标接口、生命周期

Actor/BattleEnemyActor.cs
  Transform、Animator、显隐、受击表现、死亡表现

UI/BattleEnemyViewBinder.cs（或挂在 Battle_EnemyUI 上）
  头顶 UI 创建后的绑定、血条和状态显示、Canvas 投影

Enemy/BattleEnemyRegistry.cs
  注册、注销、查找、锁敌目标
```

### 实施顺序

1. 先把 `BattleEnemyEntity` 中的 UI 字段和 UI 创建逻辑抽到 Binder，保持原 prefab 字段和 GUID 不变。
2. 将屏幕可见性和相机投影迁到 `BattleEnemyViewBinder` 或 Actor。
3. 用事件或只读状态接口让 UI 读取血量、元素和状态；UI 不反向修改 Entity。
4. 再抽取 Animator、特效和显隐逻辑到 `BattleEnemyActor`。
5. 最后检查 `BattleEnemyRegistry` 只保存目标引用，不承担 UI 或表现逻辑。

### 禁止事项

- 不在本批修改伤害和元素计算；
- 不同时引入敌人对象池；
- 不改变敌人 prefab 层级和序列化字段名，除非已完成 GUID/引用核对；
- 不让 Entity 反向获取 `Battle_MainPanel` 或具体 Canvas。

### 验收

- `BattleEnemyEntity.cs` 不再引用 `Battle_EnemyUI`、`Instantiate(uiPrefab)`、`WorldToViewportPoint`；
- Entity 不直接访问 `BattleStateModel.BattleCamera` 或 `EnvironmentEffects`；
- 敌人受击、死亡、锁敌和头顶 UI 在 Editor Play 中各验证一次；
- 静态编译 0 个新增 `error CS`。

### 回滚点

保留旧 Entity 的提交；若 prefab 序列化引用丢失，先回滚表现抽取，不回滚已经验证的纯接口和只读状态改动。

## 5. B2：关卡导演职责收口

### 目标

把关卡状态机、刷怪规则和站点标记表现拆开，避免 `BattleStageDirector` 继续成为关卡上帝类。

### 建议拆分

```text
Level/BattleLevelFlow.cs
  Prepare、Wave、Victory、Defeat、Exit 状态迁移

Level/BattleWaveSpawner.cs
  根据站点/批次/配置生成敌人

Level/BattleStageDirector.cs
  MonoBehaviour 外壳、装配和生命周期转发

Actor/BattleSiteMarkerActor.cs
  站点标记 GameObject、材质、可见性和销毁
```

### 实施顺序

1. 先把站点和批次判定提取为纯 C# 运行时对象，保留现有事件。
2. 把 `Instantiate(enemyPrefab, ...)` 和敌人生成位置计算移到 `BattleWaveSpawner`。
3. 把标记材质、透明队列和标记 GameObject 移到 `BattleSiteMarkerActor`。
4. `BattleStageDirector` 只保留 Unity 生命周期、依赖注入和转发。
5. 与 `BattleLevelFlow` 对照每帧调用顺序，确保事件时序不变。

### 验收

- Director 不再直接设置标记材质参数；
- Director 不再同时保存完整波次规则和表现对象；
- 波次比例、限时、站点顺序和胜负事件与基线一致；
- 场景中不增加必须手工挂载的新组件，除非有对应 prefab/场景清理说明。

## 6. B3：武器运行时拆分

### 目标

拆开武器规则、武器表现适配和弹道生成，降低 `BattleWeaponRuntime.cs` 的多职责密度。

### 建议拆分

```text
Weapon/BattleWeaponModeBase.cs
Weapon/BattleWeaponMode_*.cs
Weapon/BattleWeaponModeFactory.cs

Weapon/Presentation/BattleWeaponViewAdapter.cs
Weapon/Presentation/BattleWeaponViewFactory.cs
Weapon/Presentation/BattleWeaponObjectComponent.cs

Weapon/Spawning/BattleProjectileSpawner.cs
```

### 纪律

- 武器 Mode 只能通过窄接口获取开火点、朝向和播放表现；
- 子弹生成统一经过 `BattleProjectileSpawner` 和对象池；
- 删除旧版武器路径前，先确认 prefab、manifest、AB 和场景中没有旧 GUID；
- 本批不调整武器模式的数值和射击行为。

### 验收

- `BattleWeaponRuntime.cs` 不再同时包含 Factory、Adapter、ModeBase 和全部模式实现；
- 开火、换弹、狙击线、枪口特效、元素子弹和对象池回收通过现有 Play 清单；
- prefab 的 `BattleWeaponObjectComponent` 和 `muzzleFireEffect` 引用保持有效。

## 7. B4：瞄准数据下沉到表现层

### 目标

消除 Model 中的 Canvas/UI 空间数据，让 System 输出逻辑瞄准事实，View 自己完成屏幕映射和插值。

### 目标接口

```text
BattleAimSystem -> AimTarget / LockTarget / SnapMultiplier
Battle_MainPanel -> Canvas 坐标、准星位置、UI 插值
```

### 实施顺序

1. 为 `BattleAimSystem` 增加与屏幕尺寸无关的只读结果。
2. 在 `Battle_MainPanel` 中完成 Canvas 投影和 `AimReferenceWidth` 处理。
3. 删除 `BattleStateModel.AimPosition` 等 UI 空间字段。
4. 删除 System 与 Panel 之间的重复屏宽常量。
5. 用锁敌、自由瞄准、触屏拖拽三条路径分别回归。

### 验收

- Model 不再出现 Canvas、屏幕宽度、UI 参考空间字段；
- System 不引用 `Battle_MainPanel`；
- 电脑输入和触屏输入的准星表现不回归。

## 8. B5：Battle UI 入口收口

### 目标

让 Host 只调用一个战斗 UI 入口，不直接知道具体 Panel 名称和 UIKit 注册表细节。

### 建议形状

```text
BattleUiEntry.OpenForBattle()
BattleUiEntry.CloseForBattle()
BattleUiEntry.SanitizeAfterSceneUnload()
```

`BattleSceneRuntimeHost` 继续保留场景生命周期和调用时序，但不再直接调用多个具体 Panel 的静态入口。

### 验收

- Host 不直接枚举主 HUD、触屏 Panel、结算 Panel；
- 跨场景进入、退出、重开后 UI 注册表不残留；
- 具体 Panel 仍只负责显示和输入转发。

## 9. B6：文件整理与最终检查

### 工作内容

1. 将 `BattleSystems.cs` 按 System 拆文件，只做文件移动和命名整理。
2. 检查 `BattleUiFlowPanelOpener.cs` 等已知死代码，删除前完成 GUID 和资产引用核对。
3. 重新生成架构图，确认：
   - System 不引用具体 View；
   - Model 不创建 GameObject；
   - View 不写入战斗规则；
   - Actor 不决定胜负和伤害；
   - Host 不包含领域规则。
4. 更新 `18_Battle调用链与阅读目录.md`、本文件的实施记录和 `99_下一步行动/README.md`。

## 10. 每批统一验证模板

每个批次提交前必须记录：

- 修改文件和未修改文件；
- 静态扫描结果；
- Editor 编译结果（若无 Editor，标记“未验证”）；
- 需要用户在 Play 中验证的场景、Prefab 和操作步骤；
- prefab/scene/AB 影响；
- 回滚提交号。

未完成 Editor 或 Play 验证时，不得把批次标为“完成”，只能标为“代码完成，运行时待验”。

## 11. 推荐执行顺序

建议按 `B0 → B1 → B2 → B3 → B4 → B5 → B6` 推进。B1 和 B2 可以分支并行，但合并前必须分别完成编译和 Play 回归。若开发期间出现 prefab 引用丢失，优先回滚表现层抽取，保留已经确认的接口和文档变更。

## 12. 实施记录

> 每批完成后追加一节；「验证状态」按 §10 模板填写。**未完成 Editor/Play 验证的批次只能标「代码完成，运行时待验」。**

### 12.1 B0：基线与保护（2026-09-21）

**验收状态：代码 0 改动；静态基线已建立；Editor 编译与 Play 均未验证（无可用 Editor）。**

#### 12.1.1 环境与测量方法（复现须知）

| 项 | 事实 |
|---|---|
| Editor 可达性 | `unity_status` → `success:false` / `STATUS_NO_INSTANCES`（无实例装载 `com.unity.pipeline`）。按纪律只调用一次、不重试 |
| 编译验证 | **不可用**。本批全部 C# 结论均为静态阅读，未经编译器确认 |
| 行数测量 | 本机 shell 是 **Windows PowerShell 5.1**，其 `Get-Content` 读本仓库 UTF-8/LF 文件会**少数行**：`BattleSceneRuntimeHost.cs` 实测 `Get-Content` 266 行 / `ReadAllLines` 310 行 / `find /c /v ""` 310 行。**行数一律以 `[System.IO.File]::ReadAllLines()` 为准**，`find.exe` 可复核；`Measure-Object -Line` 或 `Get-Content` 得到的数字（如「8183 行」）偏小，不得引用 |

#### 12.1.2 代码规模基线

`Assets/02_Scripts/03_Battle` 运行时（排除 `Editor/`）：**79 文件 / 10630 行（非空 9115）**；Editor-only（`TeaGame.Battle.Editor`）：**6 文件 / 1185 行**。

| 目录 | 文件 | 行数 | 目录 | 文件 | 行数 |
|---|---|---|---|---|---|
| `(根)` | 2 | 357 | `Level` | 4 | 737 |
| `Actor` | 7 | 669 | `Pool` | 2 | 339 |
| `Config` | 1 | 96 | `Role` | 3 | 312 |
| `Contracts\Level` | 5 | 295 | `Skill` | 5 | 961 |
| `Contracts\Team` | 4 | 453 | `Skill\Effects` | 4 | 180 |
| `Core` | 6 | 1931 | `Skill\Executors` | 4 | 90 |
| `Data` | 1 | 140 | `UI` | 1 | 250 |
| `Effect` | 1 | 147 | `UI\Panels` | 9 | 845 |
| `Element` | 2 | 294 | `Weapon` | 4 | 883 |
| `Enemy` | 9 | 1196 | `Input` | 5 | 455 |

行数 Top 15（后续批次的主要对象）：

| 行数 | 文件 | 行数 | 文件 |
|---|---|---|---|
| 733 | `Core\BattleSystems.cs` | 275 | `Core\BattleStateModel.cs` |
| 614 | `Weapon\BattleWeaponRuntime.cs` | 250 | `UI\BattleTeamHudView.cs` |
| 500 | `Enemy\BattleEnemyEntity.cs` | 247 | `Core\BattleSetupSystem.cs` |
| 446 | `Skill\BattleSkillSystem.cs` | 228 | `Level\BattleLevelFlow.cs` |
| 430 | `Level\BattleStageDirector.cs` | 222 | `Skill\RoleSkillData.cs` |
| 321 | `Core\BattleCombatTypes.cs` | 221 | `Element\ElementStateEffect.cs` |
| 313 | `Pool\BattleObjectPool.cs` | 211 | `Enemy\Battle_EnemyUI.cs` |
| 310 | `BattleSceneRuntimeHost.cs` | | |

#### 12.1.3 程序集与依赖基线

`TeaGame.Battle.asmdef` 共 12 条引用，GUID 全部解析完毕：`TeaGame.App`、`TeaGame.Data`、`TeaGame.Tools`、`TeaGame.Battle.Contracts`、`QFramework`、`QFramework.CoreKit`、`QFramework.Data`、`UIKit`、`ResKit`、`AudioKit`、`Unity.TextMeshPro`、`DOTWeen.Modules`。

- **对 `TeaGame.Menu` / `TeaGame.Gacha` 的引用为 0**（`using TeaGame.*` 扫描仅命中 `TeaGame.App` 4 处：`BattleSceneRuntimeHost.cs:2`、`Contracts\Team\BattleAssetService.cs:2`、`Contracts\Team\BattleSceneGate.cs:2`、`Actor\BattleSceneAssembler.cs:2`）。
- `TeaGame.Battle.Contracts.asmdef` 引用：`TeaGame.App`、`TeaGame.Data`、`QFramework`、`QFramework.CoreKit`、`ResKit`。
- **本批未新增任何跨程序集引用**（无代码改动，恒成立）。后续批次以此为对照基线。

#### 12.1.4 静态扫描基线（后续批次对照用）

| 扫描项 | 基数 | 位置 |
|---|---|---|
| `new GameObject(` | 8 | `Level\BattleStageDirector.cs:298,306`（站点容器/标记）、`Pool\BattleObjectPool.cs:307`、`UI\BattleTeamHudView.cs:36,132,171,194`、`UI\Panels\Battle_TouchInputPanel.cs:92` |
| `Instantiate(` | 6 | `Data\BattleRuntimeData.cs:27,37`、`Enemy\BattleEnemyEntity.cs:462`、`Level\BattleStageDirector.cs:223`、`Pool\BattleObjectPool.cs:256`、`Weapon\BattleWeaponRuntime.cs:366` |
| `WorldToScreenPoint` / `WorldToViewportPoint` | 2 | `Core\BattleSystems.cs:535`（Aim，B4）、`Enemy\BattleEnemyEntity.cs:352`（B1） |
| `UnityEditor` | 1 | `Core\BattleSetupSystem.cs:219`，位于 `#if UNITY_EDITOR`（`:218-222`）内 — **合规，本批无违规** |

具体 Panel / View 类型被 `UI\` 之外的文件引用：

| 类型 | 处数 | 位置 |
|---|---|---|
| `Battle_MainPanel` | 9 | `BattleSceneRuntimeHost.cs:222,269,287,305`、`Actor\BattleSceneAssembler.cs:130`、`Actor\Battle_PlayerControl.cs:12`、`Core\BattleSystems.cs:541,544`（重复常量注释）、`Skill\BattleSkillSystem.cs:344` |
| `Battle_TouchInputPanel` | 6 | `BattleSceneRuntimeHost.cs:270,287,289,296,306`、`Input\BattleVirtualInput.cs:8` |
| `Battle_PauseMenuPanel` | 3 | 全部在 `Core\BattleUiSystem.cs:120,124,153` |
| `Battle_LevelResultPanel` | 3 | 全部在 `Core\BattleUiSystem.cs:70,74,84` |
| `Battle_EnemyUI` | 4 外部 | `Enemy\BattleEnemyEntity.cs:86,87,454,467` |
| `BattleTeamHudView` | 0 | — |

#### 12.1.5 六条入口行为基线

**① Host 生命周期** — `BattleSceneRuntimeHost.cs`（310 行）

| 相位 | 行 | 顺序 |
|---|---|---|
| `Awake` | `:84` | `ResKitBootstrap.EnsureInitialized` → 建 `BattleArchitecture`(`:89`) → `StateModel.Controller = playerControl`(`:93`) → `BattleSetupSystem.TryResolveLevel`(`:99`) → `ApplyLevelToScene`(`:104`) → `stageDirector.Setup(selected, catalog.EnemyCatalog)`(`:120`) |
| `Start` | `:138` | `new BattleSceneAssembler(...).Build(level)`(`:142-144`) → `battleReady` → `StartCoroutine(OpenBattleUiWhenSceneReady)`(`:147`) |
| `Update` | `:156` | 早退(`:161` `!battleReady \|\| Rig==null`) → `RoleSystem.SetControlTakenOver`(`:164`) → `InputSystem.Tick`(`:167`) → ESC 暂停请求(`:170-175`) → **暂停短路**(`:178-181`) → `MovementSystem.TickUpdate` + `ApplyMovementForce`(`:184`) → `CameraSystem.Tick`(`:187`) → `TickActors`(`:190`) → `WeaponSystem.Tick`(`:194`) → `SkillSystem.Tick`(`:197`) |
| `FixedUpdate` | `:200` | 早退 → `MovementActor.ResolveDirection`(`:208`) → `MovementSystem.TickFixedUpdate`(`:212`) → `AimSystem.TickFixedUpdate`(`:213`) |
| `LateUpdate` | `:216` | 早退 → `AimSystem.TickLateUpdate`(`:221`) → `Battle_MainPanel.TickForBattle`(`:222`) |
| `OnDestroy` | `:258` | `RoleActor.Dispose`(`:262`) → `StateModel.Controller = null`(`:265`) → `Battle_MainPanel.CloseForBattle` + `Battle_TouchInputPanel.CloseForBattle`(`:269-270`) → `UiPanelFlow.SanitizeRegistry`(`:279`) |
| 协程 | `:291` | 至多 1200 帧等 `Battle_TouchInputPanel.IsUIRootReadyForScene`(`:296`) → `Battle_MainPanel.TryOpenForBattle`(`:305`) + `Battle_TouchInputPanel.TryOpenForBattle`(`:306`) |

**② 敌人生成 / 受击 / 死亡 / 头顶 UI** — `Enemy\BattleEnemyEntity.cs`（500 行）

- 生成：`BattleStageDirector.SpawnEnemies:210` → `Instantiate`:223 → `GetComponent<BattleEnemyEntity>`:224 → `entity.Element = element`:234。
- 注册：`Start:204-208` → `BattleEnemyRegistry.Register(this)`:206 **与** `CreateUiIfNeeded()`:207 同一入口；注销 `OnDestroy:215`。
- 受击入口仅两处：`Weapon\BattleBullet.cs:78`、`Skill\Effects\SkillEffect_AoEBurst.cs:40`。`IBattleEnemyTarget.ApplyDamage`（接口 `Core\BattleCombatTypes.cs:38` / 实现 `:274`）**全工程无调用者 → 死路径**。
- 结算顺序：`ApplyElementalDamage:287`（倍率 `:289-293`）→ `ApplyDamageInternal:303` → **之后**才 `ElementStateEffect.GetOrAdd(this).Apply`:308 → `return finalDamage`:311。
- `ApplyDamageInternal:322` 一个方法内混 5 类职责：扣血 `:324-325`、飘字 `:327`、UI 血条 `:331`、死亡判定 `:334-340`。
- 死亡：`Die():476`（`mDead` 幂等 `:478`）调用者 = `:339`（玩家击杀）、`EnemyBehaviour_Missile.cs:50,64`、`EnemyBehaviour_Mine.cs:31`。`mKilledByPlayer` 只在 `:338` 置位、`:487` 消费、`:494` 复位；`BattleEvent_EnemyKilled` 发 `:489-493`，唯一订阅者 `Skill\BattleSkillSystem.cs:42`（**载荷被 `_ =>` 丢弃**）。
- 头顶 UI：字段 `uiPrefab:21` / `uiYOffset:22`；`CreateUiIfNeeded:458` → `Instantiate(uiPrefab, transform)`:462 → `localPosition = up*uiYOffset`:463 → `TryGetComponent<Battle_EnemyUI>`:465。UI 侧 `Battle_EnemyUI.cs:64-67` `GetComponentInParent<BattleEnemyEntity>()` 自绑宿主。**宿主→UI 下行只有 `RefreshHealth:331` 一处**；UI→宿主每帧读 `Element`（`Battle_EnemyUI.cs:132`）与 `ActiveStateMask`（`:177`）→ **双向耦合**。
- 可见性：`Update:224` 每帧写 `mOnCamera` → `IsVisibleInBattleCamera:347-356`（`BattleCamera:349`、`WorldToViewportPoint:352`）；唯一读者 `CanBeLocked:110` = 组件启用 + GameObject 激活 + **上一帧**在视口内（一帧延迟）。
- 飘字：`ShowDamageText:411-416`（`:415` 用根 transform 坐标），唯一触发者 `ApplyDamageInternal:327`；公开重载 `:403` 无调用者。

**③ 波次 / 清场 / 超时 / 胜负**

- 注入：`BattleSceneRuntimeHost.cs:120` → `BattleStageDirector.Setup:33`。
- 每帧：`BattleStageDirector.Update:100` → `mFinished` 短路 `:102` → `mElapsed` 累计 `:104` → 超时 `:105-109` → `TickCurrentSite:116`。
- 站点到达：`TickCurrentSite:121-137`，判定 `IsPlayerWithin(Marker.position, ArriveRadius)`:123 → `SpawnBatch(0)`:127 → 首站发 `BattleEvent_Stage_WaveStarted`:133（`mWaveStarted`:72 保证一次）。
- 批次推进公式（`:164-169`）：`killed = mBatchSpawned - aliveNow`；`ratio = killed / (float)mBatchSpawned`；`ratio >= batch.KillRatio` → `SpawnBatch(mBatchIndex+1)`。
- 刷怪：`SpawnBatch:176-208` → `ResolveEnemyPrefab:359-363`（`EnemyCatalog.ResolvePrefab`）→ `ResolveSpawnPosition:368-373`（`Quaternion.Euler(0,AngleDeg,0)*forward*Distance`）→ `SpawnEnemies:210-242`（`Instantiate:223`）。
- 清场：`PruneAndCountAlive:394-410` → `CompleteSite:246-256`（隐藏标记 `:251`）→ `ActivateNextSite:258-280` → 索引越界 `mFinished=true:268` → 发 `BattleEvent_Stage_Cleared`:272。
- 超时：`TimeUp:282-289` → `mFinished=true` → 发 `BattleEvent_StageTimedOut`:288。
- 状态机（`Level\BattleLevelFlow.cs`，228 行）：枚举 `:12-24`；唯一写点 `State.LevelState = next` **`:205`**；迁移：`StartLevel:104`→Prepare、`OnWaveStarted:119`→Wave（守 `:114`）、`OnStageCleared:133`→Victory（守 `:128`）、`OnStageTimedOut:147`→Defeat（守 `:142`）、`OnPlayerDied:163`→Defeat（守 `:156-158`）、`Restart:177`→Exit；中枢 `TransitionTo:197-216`，末尾发 `BattleEvent_LevelStateChanged`:215。
- 结算 UI：`Core\BattleUiSystem.cs:52-66` `OnLevelStateChanged` → `OpenResult:68-80` / `CloseResultIfOpen:82-85`。

**④ 武器开火 / 换弹 / 子弹回收**

- 驱动：`BattleSceneRuntimeHost.Update:194` → `BattleWeaponSystem.Tick`（`Core\BattleSystems.cs:653`）：`ControlTakenOver` 早退 `:655` → 同步 `ReloadDuration:663` → 写 `FreeAimRotation:671-673` / `ActiveElement:677` → `TickReload:680`（完成发 `BattleEvent_ReloadCompleted`:682）→ 手动换弹 `:688-692` → 开火 `PressTrigger:697` → 装填期间按帧 `PublishAmmo:703-706`。
- 弹药发布纪律：`PublishAmmo:644`，**只在变更点**（开火 / 开始换弹 / 换弹完成 / 切人）发布，唯一连续量「装填进度」在装填期间按帧发布。
- 开火：`Weapon\BattleWeaponRuntime.cs` `PressTrigger:186`（守卫 `:188-190`）→ 空仓 `StartReload:192-196` → 射速间隔 `60/firingRate`:198 → `CanFireTime`:199 → 弹匣--:201 → `PlayMuzzleFire`:202 → `OnShootEvent`:203。
- 模式分发：`BattleWeaponModeFactory.Create:72-93`（15 种 `WeaponSubType`）；`PreciseFrame` 连发 `await Task.Delay`:445-455；`Scatter` 多发 `:517-524`；`ScatterPrecisionShot` 扇面 `:538-546`；`Sniper` 射线 + 线迹 `:555-600`。
- 弹道：`FireSingleShot:280` → `ResolvePlayerBulletPrefab:305`（元素子弹优先）→ 基准旋转 `FreeAimRotation ?? FirePoint.rotation`:287 → `SpawnBulletPrefab:335`（可池化 → `BattleObjectPool.Spawn:347` + `ResetBullet:353`；旧路径 → `Instantiate:366` + 延迟 `Destroy:368`）→ 散布 `:376` → 速度 `:397`。
- 回收：`Weapon\BattleBullet.cs:58` 超时 → `Despawn:64`；命中 `OnTriggerEnter:68` → `OnHit:106` → 命中特效池 `:114/117` → `Despawn:121`。池 API `Pool\BattleObjectPool.cs:63/71/109/147/171/189/208`。敌人子弹 `BattleEnemyBullet.cs:61/96/116`。

**⑤ 瞄准 / 锁敌 / 准星**（Model 中 UI 空间字段的精确清单）

| 符号 | 声明 | 写入 | 读取 |
|---|---|---|---|
| `AimPosition` | `BattleStateModel.cs:137`（说明 `:24`） | `BattleSystems.cs:417,424,490,557` | **仅 1 处** `Battle_MainPanel.Presentation.cs:31`（`RegisterWithInitValue`）；另 `Role\BattleRoleSystem.cs:66` 复位写 |
| `AimLerpSpeed` | `BattleStateModel.cs:140` | `BattleSystems.cs:559,569` | **仅 1 处** `Battle_MainPanel.Presentation.cs:32` |
| `LockSpeedMultiplier` | `BattleStateModel.cs:79` | `BattleSystems.cs:558,568,569` | `Actor\BattleWeaponActor.cs:53`、`Role\BattleRoleSystem.cs:67` |
| `LockEnemy` | `BattleStateModel.cs:76` | `BattleSystems.cs`（16 处） | `Actor\BattleWeaponActor.cs:44,45`、`Role\BattleRoleSystem.cs:65`、`Skill\SkillContext.cs:43`、`BattleSystems.cs:670` |
| `BattleCamera` | `BattleStateModel.cs:180`（派生只读） | — | `Effect\BattleEnvironmentEffectController.cs:55,59,60,66`、`Enemy\Battle_EnemyUI.cs:87,88,90`、`Enemy\BattleEnemyEntity.cs:345,349` |

- 固定帧：`BattleSceneRuntimeHost.FixedUpdate:213` → `BattleAimSystem.TickFixedUpdate:383` → 失效清空 `:389-393` → Tab / 虚拟键切换 `:397-403` → `TrySwitchLockedTarget:441` → 手动锁定分支 `:407-419`（写 `AimPosition:417`）/ 自动吸附分支 `:421-425`（`FindLockEnemy:577` → 写 `LockEnemy:423` + `AimPosition:424`）→ 变化发 `BattleEvent_LockEnemyChanged:429`。
- 投影：`ProjectToAimSpace:533-538` = `WorldToScreenPoint` → 减屏幕中心 → `× (AimReferenceWidth / Screen.width)`。
- **重复常量**：`BattleSystems.cs:546` 与 `UI\Panels\Battle_MainPanel.cs:43` 各有一份 `AimReferenceWidth = 1920f`，且前者注释（`:540-545`）已自认「必须一致 / 待投影下沉后删除」。
- 后期帧：`BattleSceneRuntimeHost.LateUpdate:221` → `TickLateUpdate:562-571`（由 `AimPosition` 距离算 `LockSpeedMultiplier:568` → `AimLerpSpeed:569` → 自身游标插值 `:570`）；`LateUpdate:222` → `Battle_MainPanel.TickForBattle`。
- 复位：`ResetForBattle:552-560`。

#### 12.1.6 与方案稿的偏差（B1–B6 范围修正）

| # | 方案稿的假设 | 实测事实 | 对批次的影响 |
|---|---|---|---|
| 1 | B2 需新建 `Level/BattleLevelFlow.cs` | **已存在**（228 行，`AbstractSystem`），状态机与事件收口已完成 | B2 范围收窄：只剩「生成 → Spawner」「标记 → MarkerActor」「Director 外壳」三件事；`BattleStageDirector` 不再需要为状态机搬家 |
| 2 | B5 需新建 `BattleUiEntry` 统一 UI 入口 | `Core/BattleUiSystem.cs`（156 行）**已是**暂停/结算面板的唯一入口（唯一非 UI 引用者） | B5 范围收窄为「主 HUD + 触屏层」两处；只需扩展现有 System 或在其上加薄门面 |
| 3 | B1 要让 Registry「只保存目标引用」 | `BattleCombatTypes.cs:226-284` **已是**纯注册/枚举表（register/unregister/clear/enumerate），无 UI 与表现 | 该验收项**已满足**；B1 的 Registry 工作仅为「搬文件位置 + 补 `Clear()` 调用」（`:256` 现无调用者） |
| 4 | B1 拆出 `BattleEnemyRegistry.cs` | Registry 实体在 `Core\BattleCombatTypes.cs`，非独立文件 | B1 需决定是否迁文件（涉及 GUID/引用核对），或仅保留原位 |
| 5 | B6 清理死代码 `BattleUiFlowPanelOpener.cs` | 全工程**已无此文件** | B6 需重新生成死代码清单 |
| 6 | B4 只涉及 Model / System / `Battle_MainPanel` | `LockSpeedMultiplier` 还被 `Actor\BattleWeaponActor.cs:53` 读；`LockEnemy` 还被 `Actor\BattleWeaponActor.cs:44,45`、`Role\BattleRoleSystem.cs:65`、`Skill\SkillContext.cs:43` 读 | B4 的「删除 Model 瞄准字段」必须区分**UI 空间量**（`AimPosition`/`AimLerpSpeed` → 下沉）与**逻辑量**（`LockEnemy`/`LockSpeedMultiplier` → 保留）；后者有 Actor/Skill 读者，不能一并删 |
| 7 | B4 目标是「System 不引用 `Battle_MainPanel`」 | 该条成立；但 `BattleAimSystem` 的私有方法签名依赖 **View 类型 `Battle_PlayerControl`**（`:496,517,526,577`） | 「System 不引用具体 View」在当前代码里已有一处违规，B4 需一并决定其归属 |
| 8 | 未提及 | `Data\BattleRuntimeData.cs:27,37`：`BattleRoleRuntimeData` 构造函数直接 `Object.Instantiate` 角色与武器 prefab | 与最终验收「Model 不创建 GameObject」冲突，属 B3/B6 范围外的**新增项**，需单独登记 |
| 9 | B3 的 `BattleWeaponObjectComponent.cs` 待建 | 该文件**已存在**（`Weapon\BattleWeaponObjectComponent.cs`，69 行） | B3 只剩「Mode/Factory/Presentation/Spawner 拆文件」 |
| 10 | B3「子弹生成统一经过 `BattleProjectileSpawner`」 | 生成点分散 5 处：`BattleWeaponRuntime.cs:347,366,582`、`Enemy\BattleEnemyEntity.cs:367,445`、`Skill\SkillEffect.cs:92`、`Effect\BattleEnvironmentEffectController.cs:86`、`Weapon\BattleBullet.cs:114` | B3 需先界定 Spawner 的**收养范围**（仅玩家子弹？含敌人子弹与技能？） |
| 11 | §7 目标接口写作 `AimTarget / LockTarget / SnapMultiplier` | **`SnapMultiplier` 在 `Assets/02_Scripts` 全工程 0 命中**；实际概念是 `State.LockSpeedMultiplier`（`BattleStateModel.cs:79`），阈值 50 / 15 的单位是 **1920 参考空间像素**（`BattleSystems.cs:568`） | B4 的接口命名需按实际标识符重写；若 `LockSpeedMultiplier` 一并下沉，须先决定阈值语义归属（它同时被 `Actor\BattleWeaponActor.cs:53` 与 AimSystem 消费） |
| 12 | 未提及 | `BattlePlayerConfig.asset:26` 序列化 `LockEnemySpeed: 15`，**覆盖**代码默认 10（`Config\BattlePlayerConfig.cs:91`）；而 `LockAssistDistance` / `LockSwitchCooldown` **未序列化** → 取代码默认 200f / 0.3f | B4 回归时的实际参数以 15 / 200f / 0.3f 为准，不要按代码默认值预期手感 |
| 13 | 未提及 | `Assets/04_Prefabs/Panel/Battle/Battle_TouchInputPanel.prefab` 的 `TouchTapButton.kind` 取值为 {1,2,3,4,5}（行 `:226,:316,:406,:630,:1881`），**缺 `Reload`(6) 与 `LockSwitch`(7)**（枚举 `Input\TouchTapButton.cs:12-23`：None0 / SkillE1 / SkillQ2 / RolePrev3 / RoleNext4 / Pause5 / Reload6 / LockSwitch7） | B4「触屏拖拽回归」这条路径在移动端**无法切换/取消锁敌**；是否为有意省略未确认，需用户确认后再决定是否补按钮 |

#### 12.1.7 资产基线（prefab / 场景）

- `Assets/04_Prefabs/Enemy/` 下 `Enemy_*.prefab` 共 **21** 个：**20** 个挂 `BattleEnemyEntity`（`Enemy_BlastRange.prefab` 无该组件），**20** 个的 `lockPoint` 均为 `{fileID: 0}` → `LockPoint` 全部退化为根 `transform`（`:107`）。
- **20** 个敌人 prefab 的 `uiPrefab` 均指向 `EnemyEntity_UI.prefab`（guid `01f69bce0396c984c91c7c9d68ee9095`），`uiYOffset` 均为 `1`。
- 序列化字段名清单（B1 禁止改名）：`element`、`lockPoint`、`uiPrefab`、`uiYOffset`、`baseHealth`、`kind`。
- **`Assets/01_Scenes/BattleGraybox.unity` 内存在两个激活的 `BattleStageDirector`**：`m_Name` 见 `:619` 与 `:1444`，`markerColor` 见 `:637` 与 `:1477`；宿主的 `stageDirector: {fileID: 673914850}`（`:1416`）只指向第一个，第二个无数据 → 运行时按 `BattleStageDirector.cs:78-82` 打一条警告后空转。全工程仅此场景含该脚本（脚本 GUID 出现 2 次已核验）。

#### 12.1.8 保护点与回滚锚点

| 项 | 值 |
|---|---|
| B0 起始 `HEAD` | `e731fd62`（工作区干净，含本次 code-atlas 刷新） |
| `code-atlas.generated.json` | sha256 `DCFF83544422A2E2239B986EEBE020F4E357E696E56CA6C30A2C30C6902C4268`（1022115 B） |
| `code-atlas.layout.json` | sha256 `A5B72F2790CA702C401112EBB909D593591616361DFAC75A8A918A6D6B84806D`（35079 B） |
| `code-atlas.generated.json.bak` | sha256 `3AB2B6EEF7E1D6AD12703BA98D58BC31B9D8555BAED23544B01F638F98AD809C`（1011721 B） |

§3 要求「备份 code-atlas」：三个文件均已被 git 跟踪并于 `e731fd62` 入库，**该提交即备份**，故不再额外复制文件（避免仓库内出现未跟踪副本）；上表哈希用于在 B6 重新生成后判定「是否只发生了预期变化」。

#### 12.1.9 B0 遗留（静态推断，未验证 — 不得当作结论）

以下均为**读代码推断**，需 Editor Play 验证；B1/B2 实施前不得据其改行为：

> **用户决策（2026-09-21）**：第 1、2 项（站点卡死、Prepare 期超时事件被丢弃）**判定为「战斗数据表现现状」，本专项 B0–B6 一律不修改**——它们是战斗数值/元素与关卡数据耦合的既有表现，留待**后续真正涉及技能与元素的专项**时一并调整。B1–B6 实施时**不得顺手"修"这两处**，以免把行为变更混进职责重构提交。第 3–13 项仍按各自批次的边界处理。

1. 站点卡死：某批 `Spawns` 全部解析不到 prefab 时 `mBatchSpawned` 恒为 0 → `TickCurrentSite:153-161` 永不推进（仅限时可脱困）。
2. Prepare 期超时：`Update:104` 在 Prepare 也累计 `mElapsed`，超时会 `mFinished=true` 并发事件，但 `BattleLevelFlow.cs:142` 的 `!= Wave` 守卫将其丢弃 → 关卡永久停在 Prepare。
3. `SpawnEnemies:225-229`：prefab 根节点缺 `BattleEnemyEntity` 时已 `Instantiate` 的对象既不销毁、也不计入统计。
4. `BattleEvent_LockEnemyChanged`（`Core\BattleEvents.cs:38` 定义 / `BattleSystems.cs:429` 发送）**无订阅者**。
5. `BattleEnemyRegistry.Clear()`（`BattleCombatTypes.cs:256`）无调用点；跨场景残留未实测。
6. `BattleEvent_PlayerDied.WaveIndex`（`BattleEvents.cs:92`）无写入方。
7. 材质 `mMarkerMaterial`（`BattleStageDirector.cs:74`）与站点 GameObject 无释放点。
8. `BattleBullet.OnTriggerEnter` 对非敌人碰撞体也走 `OnHit`（`:100`）→ 命中任意碰撞体即回池。
9. 场景中第二个（无数据的）`BattleStageDirector` 的实际运行时影响未实测。
10. `FindLockEnemy:597-604` 把**世界距离**写入 `selectedDistance` 后与**屏幕参考距离** `canvasDistance` 比较 → 量纲混用，疑似 bug（B4 前需用户确认设计意图）。
11. `BattleAimSystem.trackingPosition`（`BattleSystems.cs:549`）用同一 `AimLerpSpeed` 做插值（`:570`）但**从不发布**，仅用于派生 `LockSpeedMultiplier` → 与 View 侧插值构成双份缓存（`:549/:556/:567/:570` 共 4 处，无其他读者）。
12. `BattleAimSystem.TrySwitchLockedTarget`（`:441`）为 public 但仅本类 `:402` 调用，无外部调用者。
13. `BattleAimSystem.ResetForBattle` 的调用者是 `Actor\BattleSceneAssembler.cs:111`（装配期），不在宿主相位表内。

#### 12.1.10 B0 验收结论

| §3 验收项 | 结果 |
|---|---|
| 静态扫描无新增跨程序集引用 | **通过**（无代码改动；基线见 §12.1.3 / §12.1.4） |
| 形成可对照的基线统计 | **通过**（§12.1.2–§12.1.7） |
| 不修改运行时代码 | **通过**（本批 0 个 `.cs` 改动） |
| Editor 编译 / Play | **未验证**（无可用 Editor） |

**结论：B0「代码完成，运行时待验」**；可进入 B1。

### 12.2 B1：敌人实体职责收口（2026-09-21）

**验收状态：代码完成；离线整程序集编译通过（0 error / 0 warning）；Editor 导入编译与 Play 未验证。**

#### 12.2.1 做法（不动 prefab 的最小抽取）

`BattleEnemyEntity` 的序列化字段（`uiPrefab` / `uiYOffset` / `lockPoint` / `blastRangeRing` / `blastRingPrefab` / `element` / `baseHealth` / `kind` …）**全部原地保留**——20 个敌人 prefab 的字段名与引用不变。抽取出的两个类都是**纯 C# 类**（非 MonoBehaviour）：

| 新增/修改 | 文件 | 行数变化 |
|---|---|---|
| 新增 | `Actor/BattleEnemyActor.cs` | 新文件（约 180 行） |
| 新增 | `UI/BattleEnemyViewBinder.cs` | 新文件（约 100 行） |
| 修改 | `Enemy/BattleEnemyEntity.cs` | 415 → 332 非空行（−83） |
| 修改 | `Enemy/Battle_EnemyUI.cs` | +2 字段 / +2 方法（自读血量） |

**为什么用纯 C# 类而不是 MonoBehaviour**：若把头顶 UI 创建改成挂在敌人 prefab 上的新组件，就必须编辑 20 个 prefab（新增组件、重新序列化），违反本批「不改变敌人 prefab 层级和序列化字段名」的禁止事项。纯 C# 类由实体在 `Awake` 组装并持有引用，既完成职责分离，又做到 prefab 零改动。

#### 12.2.2 职责迁移明细

| 原位置（`BattleEnemyEntity`） | 去向 |
|---|---|
| 字段 `mBodyRenderer` / `mBaseColor` / `mElementBodyColor` / `mLastVisualMask` + `ApplyVisualMask()` / `RefreshBaseBodyColor()` | `BattleEnemyActor`：`CacheBodyRenderer()` / `SetElement()` / `RefreshVisual()` / `RefreshBaseBodyColor()` |
| `ShowDamageText(int,Color,bool)` 实现体（读 `EnvironmentEffects`） | `BattleEnemyActor.ShowDamageText()` |
| `RangeRingPlaneSize` 常量 / `SyncBlastRangeRing()` / `ShowBlastRing()` 实现体 | `BattleEnemyActor.SyncBlastRangeRing()` / `ShowBlastRing()` |
| `mUiInstance` 字段 + `CreateUiIfNeeded()` | `BattleEnemyViewBinder.CreateHeadUi()`（保留同样的缺组件告警） |
| `mOnCamera` 字段 + `IsVisibleInBattleCamera()`（读 `BattleCamera` + `WorldToViewportPoint`） | `BattleEnemyViewBinder.TickVisibility()` → `IsOnCamera` |
| `mUiInstance.RefreshHealth(HealthRatio)` 反向推送 | 删除；改由 `Battle_EnemyUI` 在 `LateUpdate` **自读** `mHost.HealthRatio` |

**保留为对外入口**（外部调用者不变）：`ShowBlastRing`（`EnemyBehaviour_Mine.cs:46` / `EnemyBehaviour_Missile.cs:62` 调用）、`ShowDamageText` 两个重载、`NotifyElementVisualChanged` / `AttachElementState` / `TakeDirectDamage`（`ElementStateEffect` 调用）、`ApplyDamage` / `ApplyElementalDamage`。

#### 12.2.3 行为等价性核对（逐条）

1. `Element` setter：原为「算元素色 → 通知」，现为「交 Actor 算元素色 → 通知」，顺序与结果一致。
2. `Update`：`mOnCamera` 更新点从「方法内联」改为「Binder 每帧写属性」，位置仍在 `Update` 首行。
3. 着色掩码缓存：`mLastVisualMask` 初值 `-1` 语义原样搬到 Actor（`RefreshVisual` 内部做「掩码未变则跳过」），优先级 电>火>冰>风 未改。
4. 飘字坐标：`transform.position` → `mOwner.position`（同一 transform）。
5. 爆炸环：`y=0.02` 贴地、`RangeRingPlaneSize=10`、`ReturnAfter(0.55s)` 均未改。
6. `CanBeLocked`：仍是「组件启用 + GameObject 激活 + 在视口内」，仅可见性来源改为 Binder。
7. 血条展开时机：**见下条，已修正**。

**本次实施中自查发现并修正的一处自造 bug**：改为自读血量后，若变更检测初值取 `-1`，则首帧读到满血 `1.0` 即判定"变化"→ 调 `RefreshHealth(1)` → `!mOpened` 分支**让血条在未受伤时立刻展开**，破坏原有"首次受伤才展开"的手感。修正：初值置 `1f`（敌人生成必为满血，故首次变化必然是受伤），血条展开时机与迁移前一致。该 bug 无法被编译器发现，属需要 Play 或代码审查才能拦下的类型。

#### 12.2.4 静态扫描结果

`Enemy/BattleEnemyEntity.cs` 中（B1 §4 验收项）：

| 验收项 | 结果 |
|---|---|
| 不再引用 `Battle_EnemyUI` | **通过**（0 处代码引用；仅 1 处注释提及，见 §12.2.7） |
| 不再 `Instantiate(uiPrefab)` | **通过**（0 处代码引用；仅 `Die()` 既有注释里出现 "Instantiate" 一词） |
| 不再 `WorldToViewportPoint` | **通过**（0 处） |
| 不再访问 `BattleStateModel.BattleCamera` | **通过**（0 处） |
| 不再访问 `EnvironmentEffects` | **通过**（0 处） |
| 对外 API 未破坏 | **通过**（HEAD 与当前各 28 个 public/protected 成员，逐一比对仅 `CanBeLocked` 换行位置不同） |

顺手确认：`BattleEnemyRegistry`（`Core/BattleCombatTypes.cs:226-284`）本就是纯注册表，**本批未改动**，该项验收在 B0 已判定预先满足。

#### 12.2.5 离线整程序集编译（本次新增的验证回路）

**重要**：本机虽无可用 Editor（`unity_status` 无实例），但**离线整程序集编译可用**，因此 B1 不是"完全未验证"：

- 编译器：`D:\Program Files\Unity\Editor\2022.3.60f1c1\Editor\Data\DotNetSdkRoslyn\csc.dll`（经 `dotnet` 运行；本机无 .NET SDK，但运行 dll 只需运行时）。
- 输入：由 Unity 生成的 `TeaGame.Battle.csproj` 提供 **226 条 Reference** 的 HintPath + **70 个 Compile 项**（= 03_Battle 运行时代码 79 文件 − Contracts 9 文件），另加 11 条 `ProjectReference` 对应的 `Library/ScriptAssemblies/*.dll`，合计 237 条引用。
- 语言/宏：`-langversion:9.0` 与 csproj 的 `DefineConstants` 原样带入（含 `UNITY_EDITOR` / `UNITY_ANDROID`）。
- 结果：**基线（改动前）0 error CS / 0 warning；B1 改动后（72 源文件）0 error CS / 0 warning**。
- 局限：这是**编译验证**，不是 Unity 导入验证——它不检查 `.meta`、不做 AssetDatabase 导入、不覆盖 Play 行为。**仍需 Unity 刷新一次**（见 §12.2.6）。

#### 12.2.6 需要用户执行的动作

1. **回 Unity 刷新一次**（切到 Editor 窗口即可触发导入），随后**提交两个新生成的 `.cs.meta`**：
   `Assets/02_Scripts/03_Battle/Actor/BattleEnemyActor.cs.meta`、
   `Assets/02_Scripts/03_Battle/UI/BattleEnemyViewBinder.cs.meta`（本次未手写 `.meta`，按纪律由 Unity 生成）。
2. 读一眼 Console：期望 **0 error CS**（与离线编译一致）。
3. Play 验收清单（场景 `Assets/01_Scenes/BattleGraybox.unity`，Prefab 目录 `Assets/04_Prefabs/Enemy/`）：

| # | 操作 | 期望 |
|---|---|---|
| 1 | 进入战斗、走到首个站点触发刷怪 | 敌人正常生成，**头顶 UI 出现在敌人上方**且不歪斜（billboard 朝相机） |
| 2 | 首次命中敌人 | 血量条**此时才展开**（未受伤时应是收起的），且随伤害缩短 |
| 3 | 用不同元素武器命中 | 身体按元素/状态着色，优先级 电>火>冰>风；头顶「元素标识 + 状态小字」正确 |
| 4 | 打死敌人 | 敌人销毁、无残留 UI、无 DOTween "missing target" 报错刷屏 |
| 5 | 锁敌（Tab / 桌面键位） | 只有**在相机视口内**的敌人可被锁定；锁敌切换行为与改动前一致 |
| 6 | 让敌人移动到屏幕外/边界 | 该敌人不再可被锁定（可见性判定生效） |
| 7 | 打 `Enemy_Mine_*`（地雷）与 `Enemy_Missile_*`（导弹） | 常驻感应圈大小正常；自爆时**范围提示环**正常出现并自动回收 |
| 8 | 受到敌人伤害 | 玩家侧伤害飘字正常；敌人被打的**伤害飘字**位置/颜色/克制放大与改动前一致 |

#### 12.2.7 prefab / scene / AB 影响

- **prefab：0 处改动**。20 个敌人 prefab 的 `uiPrefab`(→`EnemyEntity_UI.prefab`, guid `01f69bce0396c984c91c7c9d68ee9095`)、`uiYOffset`、`lockPoint`、`element`、`baseHealth`、`kind` 全部未动。
- **scene：0 处改动**（`BattleGraybox.unity` 的两个 `BattleStageDirector` 重复对象仍按 §12.1.7 记录，**留待 B2 处理**）。
- **AB / manifest：0 处改动**（未新增/删除任何资产，`Battle_EnemyUI` 的脚本 GUID 未变，`EnemyEntity_UI.prefab` 无需重建）。
- **`.meta`**：两个新 `.cs` 的 `.meta` 由 Unity 生成后需提交（见 §12.2.6 第 1 条）。

#### 12.2.8 回滚点

- B0 起始锚点：`e731fd62`（B1 起的所有改动均未提交，`git checkout` 即可整体回滚）。
- 若 prefab 序列化引用出现问题：按 §4「回滚点」——**先回滚表现抽取（本批）**，不回滚 B0 的文档与接口结论。

#### 12.2.9 本批排除项（不做的）

- 未改伤害/元素/克制/技能数值（`ApplyElementalDamage` 原样保留，仅位置未动）。
- 未引入敌人对象池（`Die()` 仍为 `Destroy`）。
- 未修改 §12.1.9 记录的「战斗数据表现现状」两处（站点卡死、Prepare 期超时），按用户 2026-09-21 决策留给技能/元素专项。
- 未拆分 `BattleEnemyRegistry` 的物理位置（仍在 `Core/BattleCombatTypes.cs`，见 §12.1.6 第 4 条）。
