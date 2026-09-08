# Battle Arena 薄场景化重构 · 设计（T3.1C · 2026-09-06）

> 状态：**步骤2-4 已按用户方向修正改版（2026-09-06，D14）**：关卡数据/目录/battle_runtime 场景引导已落地（编译绿）；固定固件（rig/飘字/Director/UIRoot）放回场景由编辑器工具检查放置。**Anchor + 常驻 Runner 按需装配方案撤销**（对应文件已删除）。
> 位置：介于 T3.1B（已验收）与 T3.2（三种关卡模式）之间；先重构并回归，再在干净架构上开 T3.2。

***

## 1. 背景与目标

当前 `BattleGraybox` 场景内嵌了玩法装配（`Battle_PlayerControl` 场景实例、`BattleRoleRepository`、Direct/波次生成逻辑引用等）。问题：

- 场景同时承担「内容（地形/光照）」与「逻辑（玩法装配）」两种职责；

- 关卡/模式新增时若沿用该做法，会不断把玩法拷贝进新场景（T3.2 需 3 套玩法，会放大）；

- 编辑器直连 / 正式 additive 两条路径的装配时序不统一。

**目标**：场景 = **纯美术素材**（地形/几何/光照含烘焙/视觉性特效结合节点），**不含玩法逻辑，也不含锚点/布局数据**；玩家出生点、敌人生成序列、目标点等「布局锚点」全部属于逻辑关卡数据，定义在 `BattleLevelData`(SO)。**同一美术场景可被多个逻辑关卡复用**（换一套布局数据 = 一个新关卡）。玩法/角色/UI/敌人创建/流程全部由常驻运行时以 **纯 C# 系统 + prefab + SO** 形式、经 **0ConfigScene 传参进入**后装配实现。

## 2. 决策记录（2026-09-06 用户多轮确认）

| #  | 议题         | 决策                                                                                        |
| -- | ---------- | ----------------------------------------------------------------------------------------- |
| D1 | 改造粒度       | **先重构（薄场景化）并回归验收，再开 T3.2**                                                                |
| D2 | 场景内脚本（本次修正） | 战斗**固定固件可直接放场景**：角色控制 rig（PlayerControl prefab 实例）、飘字服务（Damage-Texts）、敌人管理（BattleStageDirector 波次驱动）、UIKit UIRoot；另设唯一引导 `battle_runtime`（BattleSceneRuntimeHost）做框架初始化与数据装配。场景不再追求"零脚本/纯美术" |
| D3 | 玩法逻辑宿主     | 战斗玩法/波次规则 = **纯 C# QF Systems + prefab/SO + 场景内固定固件**；不挂 config 场景层级；布局/波次数据一律在 BattleLevelData(SO) |
| D4 | 场景扩展缝      | 场景可保留「特殊功能效果相结合」的节点/占位，供后续扩展实现                                                            |
| D5 | 装配发起方（已被 D14 取代） | 原「常驻 BattleFlowRunner 统一装配」已撤销：按需 DDOL 装配时序不可靠（实战"进战斗未创建 rig"）。装配职责并入**场景内固定引导 battle_runtime** |
| D6 | 迁移粒度       | PlayerRig 连同战斗 HUD（大血条/弹匣文本/技能 HUD）**整包进** **`Battle_PlayerControl.prefab`**（rig 以场景实例固定存在）；HUD 分层属 T3.3 |
| D7 | 直接进入保底（已被 D14 取代） | 原「additive 确保入口常驻已加载 → 交 Runner」撤销；入口 additive 兜底仍由 ConfigSceneGuard 承担，场景装配由 battle_runtime 在场景内自启 |
| D8 | 场景定位（本次修正） | **场景 = 纯美术素材**，锚点/布局数据不进场景；美术场景 ↔ 逻辑关卡为 **1:N**（一场景多关卡复用）                           |
| D9 | 美术坐标系约定 | 美术场景世界原点 (0,0,0) = 该场景所有 `BattleLevelData` 布局坐标的统一基准（+Y 向上）；美术改场景需保持原点约定           |
| D10 | 入口链路 | 2MainScene 测试按钮 → **关卡选择页**（预设 3 关，对应后续三种测试）→ 点选显示介绍 + 进入 → **确认后缓存** → 进入角色选择（Main\_FormationPanel）→ 选队完成 → 进入所选关卡 |
| D11 | 关卡缓存 | 新增静态 `BattleLevelSession`（同 BattleTeamSession 风格）：缓存所选 levelId/BattleLevelData，跨面板/场景读取；重新选择即覆盖 |
| D12 | 直连默认关卡 | battle_runtime **不设** defaultLevelId 字段；无会话时按「引用该场景的首个 BattleLevelData」（目录查找）装配（应用内选关走 D10）  |
| D13 | 波次配置并入（用户指定） | **BattleStageAsset 字段并入 BattleLevelData**：`TimeLimitSeconds`/`Sites` 内嵌于关卡数据（行类型统一定义于 BattleLevelData.cs），改关卡只编辑本表；预设由生成器深拷贝独立副本；`BattleStageAsset`/`StageConfig_Demo` 过渡保留至 battle_runtime 切换（步骤 6）后删除 |
| D14 | 场景装配形态（2026-09-06 用户修正，覆盖 D5/D7） | **场景内置固定固件 + battle_runtime 引导**：rig/飘字/敌人管理(波次 Director)/UIRoot 直接放场景（工具检查放置）；`battle_runtime`（BattleSceneRuntimeHost）Awake 做框架初始化、解析关卡（BattleLevelSession → 目录 D12）、应用出生点、注入 BattleLevelData+EnemyCatalog 给 Director。撤销 Anchor + 常驻 Runner（文件已删，装配时序不可靠） |

## 3. 目标架构

```
0ConfigScene（编排壳，常驻 · 不挂玩法组件）
 ├─ SceneFlow（加载遮罩/切换） / UIKit 全局根（按需 DDOL）/ ConfigSceneGuard（编辑器直连 additive 兜底）
 └─ 流程侧仅"登记关卡选择"（BattleLevelSession，D10/D11）

美术场景 BattleGraybox（固定固件 + 引导；仍可被 1:N 逻辑关卡复用，D8）
 ├─ 地形 / 几何 / 光照（含烘焙）/ 视觉特效结合节点（扩展缝，D4）
 ├─ 固定固件：PlayerControl rig 实例（Battle_PlayerControl.prefab，D6）、
 │           Damage-Texts（飘字服务）、BattleStageDirector（敌人/波次驱动）、UIRoot（框架根）
 ├─ battle_runtime（唯一引导：BattleSceneRuntimeHost —— 框架初始化 + 从关卡数据装配，D14）
 └─ 布局坐标不进场景——由 BattleLevelData 以“美术场景原点基准”数据化提供（出生点/站点/刷怪）

Battle 运行时/规则
 ├─ QF Systems（纯 C#：流程/伤害/技能/切人/输入…，已有架构即此形态）
 ├─ BattleLevelData(SO)：ArtSceneName + 出生点 + 波次表(站点/批次/刷怪) + 限时 + 模式（原 BattleStageAsset 并入）
 ├─ EnemyCatalog(SO)：敌人 id → prefab
 └─ BattleLevelCatalog(SO)：3 预设关列表 + EnemyCatalog + PlayerRig prefab（battle_runtime 数据源，编辑器生成器维护）
```

### 美术场景 ↔ 逻辑关卡（1:N）

- 一个美术场景（一套地形/光照）被多个 `BattleLevelData` 复用：布局差异只体现在**数据表坐标**上；

- D9 坐标约定保证复用对齐：美术场景根在世界原点 (0,0,0)（+Y 上），`BattleLevelData` 内全部位置/旋转按该坐标系手写或在编辑器中摆放导出；

- 因此「新关卡」大多 = 新增一个 `BattleLevelData`，不一定需要新场景/新美术。

### 装配时序（两条路径汇成一条，D14）

- **正式路径（0Config 侧）**：流程（当前=编队确认后 `BattleSceneGate` 请求；后续步骤7 增加选关）→ 选关缓存写入 `BattleLevelSession` → SceneFlow 带遮罩加载**美术场景**（additive 于 config 之上）→ 场景内 `battle_runtime`（BattleSceneRuntimeHost）Awake：确保架构 → 解析关卡（会话优先/目录 D12）→ 应用出生点到场景 rig → 注入 `BattleLevelData`+EnemyCatalog 给场景 Director → `Battle_PlayerControl.Start` 走既有复位链。

- **编辑器直连路径（调试/美术迭代）**：直接 Play 美术场景 → `ConfigSceneGuard` additive 兜底加载入口常驻场景 → 场景内 `battle_runtime` 自启，目录 D12 解析「首个引用本场景的 `BattleLevelData`」走同一条装配；**不依赖任何外部触发**。

- **重开（UnloadFirst）/退出（遮罩回来源）**：保持现语义，仍由 `BattleSceneGate` 经 SceneFlow 执行；rig 随场景卸载走 T3.1B 既有收口（OnDestroy → `BattleObjectPool.DespawnAllActive` + UIKit 面板复位），battle_runtime 随新场景实例重新装配（数据驱动、幂等）。

### 入口链路（D10/D11）

```
2MainScene（测试按钮）
 → LevelSelectPanel（覆盖层：3 预设关 = 清剿/护送/防守 的三种测试占位）
    点选 → 显示关卡介绍 + 「进入」；「进入」→ BattleLevelSession.Selected = 该关（缓存）
 → Main_FormationPanel（角色选择覆盖层，现有 MVC 不变）
    开始出战 → Gate 加载 BattleSceneName；battle_runtime 读 BattleLevelSession.Selected（无则目录 D12）装配
```

- 三预设关由 `BattleLevelData` 驱动（本次先建 01/02/03 骨架：可先共用同一美术场景/布局，T3.2 补三种模式规则后即为其正式入口）；
- `BattleLevelSession`：静态缓存 { levelId, BattleLevelData }，供编队页「开始出战」与 `battle_runtime` 读取；回主场景重选即覆盖。

## 4. 场景契约（固定固件留场景 · 数据/规则出场景，D14 修正）

**留在美术场景（固定固件 + 内容层）**

- 环境几何、材质、光照设置、烘焙数据、Light Probe/反射探针；

- 视觉性「特殊功能/效果结合节点」（空壳占位 + 约定命名，规则数据/实现后续挂到引导/系统侧，场景不写规则）；

- **固定固件**（战斗每次必用，直接场景内摆放，由编辑器工具检查/放置）：
  - `PlayerControl`：`Battle_PlayerControl.prefab` 场景实例（角色控制 + 战斗 HUD 引用，D6 整包）；
  - `Damage-Texts`：飘字服务（`BattleEnvironmentEffectController`）；
  - `BattleStageDirector`：敌人/波次驱动（数据由引导注入，不再消费场景旧 config 资产）；
  - `UIRoot`：UIKit 框架根（保留，去留排步骤6）；
  - `battle_runtime`：唯一引导（`BattleSceneRuntimeHost`，见 5.1）。

**移出美术场景（仅数据/一次性逻辑 → SO/系统）**

- **布局锚点**：玩家出生点、目标点序列、敌人生成序列/位置/朝向、刷怪时序 → `BattleLevelData`(SO) 内数据字段（坐标系基准=D9 美术场景原点），**场景内不再摆放任何玩法锚点**；

- 波次/生成/胜负规则数据 → `BattleStageDirector`（场景固件，只做执行）+ `BattleLevelData`(SO)；**原 BattleStageAsset 字段已并入 BattleLevelData**（内嵌波次表，改关卡只编辑本表），`StageConfig_Demo`/`BattleStageAsset` 过渡保留至步骤 6 后删除；

- 悬空/一次性对象：`BattleRoleRepository`（实例引用已被置 null、无人消费）从场景删除；数据装配不再依赖旧的按需 Runner/Anchor（已撤销删除）。

## 5. 关键组件规格

### 5.1 battle_runtime（BattleSceneRuntimeHost，场景内唯一引导）

```
职责：battle 场景的固定引导——框架初始化 + 「从关卡数据完成场景配置资源初始化」。
行为：Awake
  1. 确保战斗架构创建（BattleArchitecture.Interface，与 rig.Awake 幂等）；
  2. 解析本场关卡：BattleLevelSession.Selected（D11）优先；否则目录 D12（首个引用本场景的 BattleLevelData）；
  3. 应用到场景固件：rig 移到 PlayerSpawnPosition/Euler（D9）；BattleStageDirector.Setup(level, EnemyCatalog)。
约束：不承载布局/波次数据；不设 defaultLevelId；只做装配发起，不写玩法规则。
状态：已落地 `Assets/02_Scripts/03_Battle/Level/BattleSceneRuntimeHost.cs`（场景对象名 battle_runtime）。
```

### 5.2 编辑器治理工具（检查并放置，BattleArenaSceneThinner.cs）

```
菜单：Thrud/Battle/T3.1C/1. 检查并放置战斗固定固件 (BattleGraybox)（幂等）
目标态：
  A. 关卡数据资产就绪（3 预设 + BattleLevelCatalog，含 EnemyCatalog/PlayerRig 引用）；
  B. 撤销旧「rig prefab 内建飘字」（若有：移除 DamageTexts 子节点、清 env 基值 → prefab 回净态）；
  C. 场景治理：移除悬空 BattleRoleRepository / 过时 LevelSceneAnchor；
     放置固件：PlayerControl rig 实例、Damage-Texts、BattleStageDirector、battle_runtime；
     接线：rig 实例 env 覆盖 → Damage-Texts；battle_runtime 回填 catalog/rig/director 引用；
  D. 保存场景并输出根对象清单报告。
约束：对象按「根名 + 组件/来源」双条件命中；UIRoot 保留待步骤6。
```

### 5.3 BattleLevelData(SO)

- 结构（已实现 2026-09-06）：`LevelId` / `DisplayName` / `Intro` / `Mode`(T3.2 起) / `ArtSceneName`(美术场景名) / `PlayerSpawnPosition+Euler`（坐标+朝向）/ **`TimeLimitSeconds` + `Sites`(内嵌波次表：站点[位置+到达半径+批次[击杀比例+刷怪条目]]——原 BattleStageAsset 字段已并入本类)**；T3.2 再扩展模式规则与评价权重字段；

- 坐标系基准：引用场景的世界原点 (0,0,0)，+Y 上（D9）；站点位置为水平 (x,z)，刷怪按相对站点方位角/距离/高度换算（沿用现 BattleStageDirector 规则）；

- 来源：波次内容由 Editor 生成器从 StageConfig\_Demo **深拷贝内嵌**进每份预设（各关卡独立副本，改表互不影响）；玩家出生点数据化为 (0,2,0)（现场景实例值）；`StageConfig_Demo.asset` 与 `BattleStageAsset` 类属**过渡保留**（旧场景路径仍引用），battle_runtime 切换后删除。

### 5.4 PlayerRig.prefab

- 内容 = Battle\_PlayerControl 场景对象所承载全部（装配器 + dataRepository + playerConfig + 战斗 HUD/元素子弹引用 + Skill HUD 文本）；以**场景固定实例**存在（BattleGraybox 由编辑器工具放置/校验，出生点由 battle_runtime 按数据应用）。

### 5.5 LevelSelectPanel（关卡选择覆盖层，D10）

- UIKit UIPanel（CodeGen + ResKit AB），3 预设关列表（清剿/护送/防守占位）；
- 列表数据源 = `BattleLevelCatalog`（levelId + 名称 + 介绍 + 引用数据）；
- 交互：点选 → 右侧/下方显示介绍 + 「进入」；「进入」→ 写 `BattleLevelSession` → 关闭本面板 → 打开 `Main_FormationPanel`。

### 5.6 BattleLevelSession（静态关卡缓存，D11）

- 同 `BattleTeamSession` 风格：静态 { levelId, BattleLevelData }；
- 写入：LevelSelectPanel「进入」；读取：编队页「开始出战」与 `battle_runtime` 装配；
- 语义：关卡重选即覆盖；领域重载回落默认（无选择时 `battle_runtime` 用目录直连默认=D12）。

## 6. 迁移步骤（先重构，含每步回归）

> **步骤 1 盘点结论（2026-09-06 只读）**：BattleGraybox 已高度 prefab/SO 化——场景无相机/敌人/UI 直摆；角色 rig=`Battle_PlayerControl.prefab` 实例；玩家出生硬编码于场景实例位置 (0,2,0)；目标点/刷怪全部由 `StageConfig_Demo.asset`(SO) 数据驱动、Director 运行时 `CreatePrimitive` 生成，**场景内无任何玩法布局标记**（故「布局一次性导出」退化：只需把 StageConfig_Demo 视为 BattleLevelData 雏形做资产归位）。场景内仍需迁出的玩法对象：`BattleStageDirector`（Environment 下挂 SO 引用的 MonoBehaviour）、`Damage-Texts`（飘字表现服务）、`BattleRoleRepository`（悬空：实例引用被置 null、无人消费，可删）、`PlayerControl` 实例（由 Runner 按 BattleLevelData.playerSpawn 实例化）、`UIRoot` 场景实例（UIKit 框架根，核对双 UIRoot 后决定去留）。美术内容=光照 + Environment(Graybox 地/墙)。

1. **现状盘点清单**（已完，见上）——分类结论：美术留场景；玩法/装配/表现服务迁 prefab/系统；无布局标记需导出。
2. **关卡数据归位（2026-09-06 已实现 §5.3）**：新建 `BattleLevelData` 01/02/03；波次表由生成器从 `StageConfig_Demo` **深拷贝内嵌**（原 BattleStageAsset 字段已并入本类，改关只改本表，各关卡副本独立）；玩家出生点数据化 (0,2,0)；`StageConfig_Demo`/`BattleStageAsset` 过渡保留（旧场景路径仍引用）待步骤 6 删除。
3. **抽 PlayerRig.prefab（D6；场景固定实例）**：Battle_PlayerControl.prefab 已含角色控制 + HUD 整包；BattleGraybox 内以**场景固定实例**存在（出生点数据化后由 battle_runtime 应用，不再依赖场景硬编 (0,2,0)）。
4. **场景引导 + 固件治理（D14 修正版；代码已实现，编译绿；工具 BattleArenaSceneThinner.cs）**：`BattleSceneRuntimeHost.cs`（battle_runtime 场景引导：框架初始化 + 解析关卡[会话/目录 D12] + 应用出生点 + 注入 Director）+ `BattleLevelCatalog.cs`（目录）。**Editor 待做**：跑 `Thrud/Battle/T3.1C/1. 检查并放置战斗固定固件 (BattleGraybox)` —— 幂等收敛：确保 3 预设+目录 → 撤销旧 prefab 内建飘字（若有）→ 移除 BattleRoleRepository/LevelSceneAnchor → 放置 PlayerControl rig 实例/Damage-Texts/BattleStageDirector/battle_runtime 并接线（rig.env 覆盖 → Damage-Texts；host 回填引用）→ 保存并报告根对象清单。
5. **正式路径验收（battle_runtime 自动生效；用户验收通过 2026-09-06）**：编队确认 → Gate 进 BattleGraybox，rig 为场景固定存在（不再依赖"按需创建"），`battle_runtime` Awake 完成装配（会话关卡优先，无选关回落目录 D12 默认关）；回归基线（见 §7）全绿。后续步骤7 选关接入后自然按所选关卡装配。
6. **LevelDirector 切新数据源 + 清理旧类（已执行 2026-09-06）**：BattleStageDirector 改为纯注入（移除旧 `config`/`catalog` 序列化字段，消费 `BattleLevelData`）；`BattleStageAsset` 类与 `StageConfig_Demo.asset` 已删除（引用审计：其 GUID 仅存在于自身 meta 与待删资产，无场景/代码引用）；生成器波次源改为「首个含波次内容的既有预设」模板；运行时编译绿。**待本步 Play 回归**（建议先跑 `Thrud/Battle/T3.1C/1.` 保存场景清除旧序列化残留）。
7. **入口链路接线（D10/D11；已实现并验收——用户反馈无严重问题）**：2MainScene 测试按钮（MainMenu_Runtime.OpenLevelSelect）→ `LevelSelectPanel`（代码：`UIPanel/LevelSelect/LevelSelectPanel.cs`+Designer；目录数据源= prefab 上序列化的 `BattleLevelCatalog`）→ 点选显示介绍 + 「进入」（写 `BattleLevelSession`）→ `Main_FormationPanel`（现有 MVC 不动）→「开始出战」经 Gate 进 battle（battle_runtime 读会话关卡装配）。预置体已由生成器创建（`Thrud/Battle/T3.1C/7. Create LevelSelect Panel (prefab)`，绑定+AB：`levelselectpanel_prefab`+目录回填）。
8. **勾选/归档**：更新 07/09 文档，用户 Play 验收后提交；随后 T3.2 在此架构上实现三种模式（同美术场景可挂多关数据）。

## 7. 回归基线（每步必须全绿）

- [ ] 主菜单编队（1-4 人）→ 进战斗：按所选编队出战/切人（切人序=槽序）；
- [ ] 入口链路（D10）：测试按钮 → 关卡选择（3 预设）→ 显示介绍并「进入」（缓存 `BattleLevelSession`）→ 编队 → 进对应关卡；回主场景重新选关会覆盖缓存；

- [ ] 暂停/结算「退出」经 SceneFlow 遮罩回来源场景；「重新开始/再来一次」= 先卸后载（无残留弹幕/状态/HUD 错乱）；

- [ ] 编辑器直连 `BattleGraybox`：`battle_runtime` 自启装配（目录 D12 默认关；rig 为场景固定存在，必出现）；直连时场景可独立做地形/光照迭代；

- [ ] T3.1B 成员级血量/阵亡/自动切人/队伍血条 HUD 行为不回退；

- [ ] `BattleObjectPool` 清场纪律保持（DDOL 池根不滞留跨场景子弹）。

## 8. 硬边界与后续衔接

- **本卡不做**：HUD 正式化分层（T3.3）、三模式玩法（T3.2）、Boss 关、关卡编辑器工具、随机种子、关卡美术。

- **T3.2 衔接**：**少量美术场景 + N 个** **`BattleLevelData`（1:N 复用）**；清剿/护送/防守各一套模式规则系统，同场景换布局数据即新关卡，场景不再复制玩法。

- **M0/M2 衔接**：本架构与 SceneFlow Payload 化、模块索引（Modules/00）一致；PlayerMeta/编队快照经 session/payload 进入战斗。

- **场景扩展缝**：arena 内「特殊功能/效果结合节点」按命名约定预留，规则实现挂到引导/系统侧，场景侧只提供挂点与表现占位。

## 9. 风险与已知坑（沿用本工程纪律）

- additive 同场景并存 → 已由 `UnloadFirst` 重开语义规避；`battle_runtime` 装配在场景自身 Awake 完成（每场景实例一次，天然幂等，不依赖跨场景回调）。

- 跨场景引用残留（接口判空不触发 Unity 判空）→ 一律事件/纯 C# 通信；场景固定固件之间只走序列化引用 + 引导装配。

- MonoBehaviour 需独立文件（prefab/场景序列化 `11500000` 引用）→ 新增引导/固件均独立成文件（BattleSceneRuntimeHost 等）。

- 场景迁移保 GUID（.cs/.meta 成对移动）；PlayerRig 以场景固定实例 + prefab 资产双轨存在，改 prefab 时注意场景实例 override（环境特效等）不回断。

