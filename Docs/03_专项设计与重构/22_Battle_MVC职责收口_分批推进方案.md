# Battle MVC 职责收口：分批推进方案

> 状态：方案稿（2026-09-21）  
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
