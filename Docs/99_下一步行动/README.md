# 下一步行动清单（Docs 整理日汇总）

> **2026-09-19 Actor 专项（用户已确认，代码落地，编译/Play 待验）**：移动含传送→MovementActor；模型显隐/动画→RoleActor；武器表现→WeaponActor；FOV→CameraActor。InputSystem 原样保留。BattleViewSystem / InitializeView / 注册已删除，UI 职责归 Battle_MainPanel；Aim → BindableProperty<Vector2> → View 映射/Lerp，吸附倍率独立于 UI。Host 统一装配和 Tick；敌人/子弹/资源加载不扩范围。详细验收与 Editor 清理菜单见 `../03_专项设计与重构/19_表现层分层_View与Actor_设计.md` 当前实施记录。未修改伤害规则，未运行 prefab 工具/重建 AB，不宣称资产或真机完成。

当前 Actor 待办优先于下文旧“方案待确认/步骤迁移”条目：
- [x] 用户确认范围及 Aim UI 迁移；代码职责拆分完成。
- [ ] Unity Refresh，核验本轮 Editor.log 编译段及生成的新脚本 meta。
- [ ] 执行 Thrud/Battle/Actor 资产清理菜单、检查场景覆盖、rig CodeGen。
- [ ] 按 19 当前实施记录一次性 Play 回归；发布前重建 AB。

代码阅读工具专项（来源：`../03_专项设计与重构/20_代码知识图谱可视化工具_设计与实施计划.md`；2026-09-19 落地）：
- [x] K0 冻结基准：`web/ProgressChaser/scripts/baseline-stats.js`，备份见 `data/backups/`（9 程序集 / 143 文件 / 221 类型 / 1525 关系；Battle 151 节点 / 559 聚合边）。
- [x] K1 npm 锁文件 + `.cmd` 一键依赖检查启动链（`scripts/ensure-deps.js`、`GET /api/health`、`/vendor/*` 白名单）。
- [x] K2 Cytoscape.js Canvas 替换 SVG 整图重建渲染（两层视图、筛选、拖拽、右侧证据、同向聚合单边）。
- [x] K3 ELK.js Worker 落地 layered + orthogonal 布局：requestId 丢弃过期结果、>3 秒取消、失败回退。
- [x] K4 LOD、一跳/两跳邻居、搜索定位、`cy.batch()`、合成图性能取证（`web/ProgressChaser/PERFORMANCE.md`）。
- [x] K5 JSON 校验与错误恢复：逐字段定位、layout schema 1→2 迁移、orphan 保留、409 冲突、扫描 `.bak` 回滚；`npm test` 10 项通过。
- [ ] K6 文档已同步（README/PERFORMANCE/本文/20），**待用户按 §10.3 做浏览器视觉验收**（FPS、内存、线团改善）。
- [ ] 未实现（文档 §6.4 可选建议）：外部类型按程序集聚合节点；本轮外部节点逐个显示并保留折叠开关。


> 汇总日期：**2026-09-18（P1A S2/S3 落地：代码域拆分完成、产出 17 代码现状地图，来源 `../03_专项设计与重构/17_代码现状地图.md` + `../03_专项设计与重构/16…` §5A + `../05_开发日志/09_开发日志_会话交接.md`）**；上一轮汇总 2026-09-15（回填 `M1_TeamSelect` 文档清账）。目的：在文档按类型重组后，**保留并集中可追溯的「下一步要做」内容**。
> 本文只做**摘编**，不改写事实；每项标注来源，**原文/细节一律以来源文档为准**。新会话仍按老规矩：先读 `../05_开发日志/09_开发日志_会话交接.md`，再按 `../01_里程碑与执行/07_M1战斗灰盒_开发追踪指南.md` 逐卡执行。

---

## 1. 当前进度快照（2026-09-08 晚，已核对）

- **里程碑**：M1 开发中；M0 仅 ConfigScene 架构 C1–C3 代码落地（其余 C4/C6/C7 + 热更链路待做）；M2/M3 未开始。
- **M1 已完成并验收**：T0.x–T1.x（Phase 0/1）、T2.1–T2.4（Phase 2）、T3.1、T3.1A、T3.1B、**T3.1C**（2026-09-05/06/08 用户验收）。
- **T3.1C（Battle_Arena 薄场景化重构）**：主体已提交（`22f09dfd`/`500d10e4`）；**2026-09-08 收尾完成**——步骤 4 工具复跑幂等收敛（rig/Damage-Texts/Director 就绪、battle_runtime 引用已序列化、无残留对象）、步骤 6 Play 回归 6 项全绿（用户验证）、步骤 8 归档（07 插卡、09/11/99 同步）。Docs 重组已于同日提交 `2be69fc1`。
- **Demo 线（主线 A）进展**：**T0** 已完成 Android 平台切换（IL2CPP+ARM64）与 ResKit AB 全量构建（真机最小链路待验）；**T1 触屏输入映射已完成并 Play 验证通过**（UIKit 触摸面板 + 虚拟输入源 + 二选一输入；用户反馈「基本正常」）。
- **下一步**：T3 剿灭关卡/数值调整（与真机验收并行）→ T2 一键构建与外放。
- 来源：`../05_开发日志/09_开发日志_会话交接.md`、`../03_专项设计与重构/11_Battle_Arena_薄场景化重构_设计.md`、`../01_里程碑与执行/13_手游试玩Demo_规划.md`、`../01_里程碑与执行/07_M1战斗灰盒_开发追踪指南.md`

## 2. 主线决策（2026-09-08 用户确认）

- [x] **A. Android Demo 外放线**（`../01_里程碑与执行/13_手游试玩Demo_规划.md`，§6 排期约 1 周）——已确认：T0 Android 最小包冒烟先行，T1/T3 并行，T2 收口。
- [ ] ~~**B. M1 战斗主线**（T3.2）~~ → 顺延：Demo 线 T0–T2 之后回到 T3.2（09 交接「当前进度」段已同步此顺序）。
- [x] 决策已回填：09 交接（头部日期/进度快照/Phase 3 计划/提交记录）、07（T3.1C 插卡）、11（§7 全绿勾选 + 状态头）、13 文档已随 Docs 重组入库。

> 两线执行顺序（用户确认）：先 13 的 T0（最快打穿“出包 + UI 能加载”，阻断风险最高）→ T1 输入映射 ∥ T3 数值调整 → T2 外放 → 回到 T3.2。

---

## 3. 待办清单（按来源整理）

### 3.1 Demo 线（Android 外放，来源：`../01_里程碑与执行/13_手游试玩Demo_规划.md`）

**T0 · Android 最小包冒烟（0.5~1 天，先行 · 2026-09-08 起执行）**
- [x] Android 环境：SDK 就绪、包名 `com.i000stea.thrudremark`、MinSdk 28、横屏、**IL2CPP + ARM64**（切平台编译通过，用户确认「打包成功」）
- [x] ResKit AB 全量构建（Android 目标，用户确认「ResKit 打包成功」）
- [ ] 最小链路真机：登录 → 主菜单 → 选关 → 编队 → 进关出 rig（**面板不空屏**）
  - ⚠️ 2026-09-15 核对：**P0 修复从未进包**——最近 APK `alpha.0.47`（09-10 20:29，189.2MB）早于 `battle_characters` AB（09-12 构建）。本项必须**重新一键出包**后再验；验收项见 `15` 文档 §6 A/B/D
- [x] 产出：一键构建脚本雏形 + 真机验收清单 v0（2026-09-15）——脚本 `AndroidDemoBuilder.cs` 补齐门禁/AB 校验/体积与报告/版本号；说明书 + 清单落 **`../01_里程碑与执行/15_Demo打包与外放_构建说明与真机验收清单.md`**
- 备注：切 Android 时暴露并已修复 `TeaGame` 全平台程序集内 Editor 代码（CS0234/CS0012）→ 三个 Editor-only asmdef 隔离（见 09 交接「关键坑」）。

**T1 · 触屏输入映射（2026-09-08 完成并 Play 验证通过——实现记录见 `../01_里程碑与执行/13` §3.1）**
- [x] 输入改造：在 `BattleInputSystem` 采集层插入虚拟输入源（`BattleVirtualInput`），战斗逻辑零改动；编辑器下**二选一**（触屏层启用时仅触屏输入，关闭时仅键鼠）
- [x] 布局（首版）：左下移动摇杆 / 右下 开火大键 + 冲刺 + E/Q + 切人（◀▶ 循环）+ 暂停 / 屏中约 40% 宽拖拽转镜头
- [x] 实现方式：**UIKit UIPanel + Editor 生成 prefab + ResKit AB**（`BattleTouchInputPanel` / `battletouchinputpanel_prefab`），非运行时代码构建
- [x] 跨场景修复：`battle_runtime` 等 UIRoot 归属稳定后再打开触摸层（2MainScene/BattleGraybox 各自带 UIRoot 场景实例，非 DDOL）
- [ ] 真机微调项：拖拽区与按钮热区重叠判定、多指互斥、安全区（刘海/全面屏）、触感反馈、升降键（Q/E）是否需要
- [ ] **T1 触摸层真机回归（2026-09-09 已修 AB 预检过早回收）**：随 T0 真机链路一起验触摸层可开/可操作（来源：09 关键坑 25）
- [ ] `BattleUiSystem.TryEnsurePanelAvailable`（结算/暂停面板 AB 预检）**同源「过早 Recycle2Cache」隐患待修**（来源：09 关键坑 25）
- [ ] `battle_runtime` 挂 `teamCatalog`（= `Assets/Data/BattleTeamCatalog.asset`）：**方案 B 真机前置**，编辑器直连可回落 Assets/Data 不挂（来源：09 关键坑 26）
- [ ] 可选清理：`BattleLegacyRuntimeDataAdapter`（旧版 EntityBattleData 适配器，无调用者）

**T3 · 剿灭关卡/敌人/角色调整（1~2 天，与 T1 并行）**
- [ ] 复用 level_01（ArtSceneName=BattleGraybox），站点数 = 3（教学→压力→收尾，约 90s）
- [ ] 数值按 P7：玩家 MaxHp≈400、普通弹幕单发≈40（约扛 8~10 发）、E≈CD 8s、Q≈CD 25s+能量
- [ ] 角色 SO 把工作区 HP 残留（60/40 压测值）改回正式值并**一并提交**
- [ ] 失败/全灭 → 可立即“再来一次”；胜利 → 结算 → 重开/返回主菜单

**T2 · 打包收口与外放（0.5~1 天 · 2026-09-15 脚本补门禁，待执行出包）**
- [x] Android 一键构建工具：`Thrud/Demo/Android/`（**0 前置检查** / 切平台 / 打 AB / Build APK Release·Development / Build All One-Click / **6 版本号 +1**）——`AndroidDemoBuilder.cs`
- [x] 构建说明（2026-09-15）：`../01_里程碑与执行/15_Demo打包与外放_构建说明与真机验收清单.md`——环境前置、菜单逐条说明、门禁 G1–G8、日志关键字、失败速查
- [x] 真机回归清单 v0（2026-09-15）：`15` §6 A–H、S（安装启动 / 最小链路 / 触屏 / 面板不空屏 / 战斗数值 / 性能发热 / 中断恢复 / 包体分发），每项含通过标准与失败指向
- [ ] **编辑器执行一键构建出包**（菜单 0 → 1 → 5；需切 Android + IL2CPP 首次编译，约 3–18 分钟）；出包后填 `15` §8 交付记录
- [ ] 真机按 `15` §6 逐项验收（重点 A3 两条启动日志 + B5 角色出现 + D2 重开后再进关触摸层仍在）
- [ ] 分发：debug 签名 apk 直接外放（正式签名后补）
- [ ] 包体余量不足 11MB（当前 189.2MB / 目标 200MB；AB 合计 131.1MB）：**先跑菜单 7 清掉孤儿产物 `battleteamcatalog_asset`（42.7MiB，已不在 AB 清单里）**，可省回约 43MiB；仍逼近上限再按 `../03_专项设计与重构/14…` P3 做 AB 划分

**开放待验证小项（不阻塞启动，`../01_里程碑与执行/13` §5）**
- [ ] 切人键用「←/→ 循环」即可，还是四槽直接选？
- [ ] 中央拖拽区屏幕占比与边缘热区
- [ ] 冲刺(Sprint)独立按键，或并入摇杆外圈快推
- [ ] 是否需要“自动开火”开关（隐藏项降门槛）

### 3.2 M1 主线（来源：`../01_里程碑与执行/07_M1战斗灰盒_开发追踪指南.md`、09 交接、11 文档）

**T3.1C 收尾（2026-09-08 已全部完成——来源 `../03_专项设计与重构/11…` §6/§7/§8）**
- [x] 步骤 4 Editor 工具复跑：`Thrud/Battle/T3.1C/1. 检查并放置战斗固定固件 (BattleGraybox)`（幂等收敛；结果：rig/Damage-Texts/Director 就绪、battle_runtime 引用已序列化、场景无残留对象；工具文案「跳过(见警告)」已修正为「完成(含本就一致)/未接线(见警告)」）
- [x] 步骤 6 Play 回归：2026-09-08 用户验证全部通过
- [x] 步骤 8 归档：07 插 T3.1C 卡、09/11/99 同步（含本清单）
- [x] 回归基线 6 项（11 §7 已全绿勾选）：① 编队→进战斗按所选出战/切人 ② 入口链路（测试按钮→3 预设选关→写 Session→进关；重选覆盖缓存）③ 暂停/结算退出经 SceneFlow 回来源 + 重开先卸后载无残留 ④ 编辑器直连 BattleGraybox 时 battle_runtime 自启装配（rig 必出现）⑤ T3.1B 血量/阵亡/自动切人/HUD 不回退 ⑥ 对象池清场纪律保持

**Demo 线完成后再进入（来源：`../01_里程碑与执行/07` Phase 3/4 + 09 “当前进度” → 顺序已改为 Demo 先行）**
- [ ] T3.2 三种关卡模式与灰盒关卡（清剿/护送/防守 + Boss 关，3-5 关，`BattleLevelData` 数据优先）
- [ ] T3.3 正式战斗 HUD（收编队伍血条列表/技能 HUD；灰盒 UGUI 基础组件）
- [ ] T3.4 结算系统（S/A/B/C 评价、掉落表、battle:enter/settle 埋点先 Debug.Log 占位）
- [ ] Phase 4：T4.1 手感指标实测（反馈≤100ms/吸附≤200ms/切人≤1帧/20 敌 60fps，结果回填 07“验收记录”表）→ T4.2 埋点与 GC 检查 → T4.3 垂直切片冻结（tag `m1-slice-v1.0`）
- [ ] Phase 5–7（远期）：T5.1 暂停菜单与调试功能 UI / T6.1 Android 屏幕输入适配 / T7.1 Android 热更发布测试版

**07 文档中的待确认项（不阻塞开发，按默认值先行）**
- [ ] 锁定手感（D06）：默认半自动（吸附+Tab），待最终确认
- [ ] 垂直切片角色：默认法尼娅（雷）+ 突击武器
- [ ] 整场限时用途（T2.2）：默认超时判负（Defeat 占位日志）
- [ ] 导弹被击落（T2.1）：默认直接销毁不爆炸；地雷被击杀必爆炸（大范围）——参数随灰盒调参

### 3.3 ConfigScene / M0 剩余（来源：`../03_专项设计与重构/08_常驻管理场景_ConfigScene_设计.md`）

- [ ] **待用户执行**：Unity 编辑器跑 `Thrud/Setup ConfigScene` 生成场景文件并置 Build Settings[0]，Play 验证启动链路（ConfigScene → additive 加载 Login）
- [ ] C4：迁移全局对象（StoreBattleRoleData / BattleRoleRepository / LoadingGroup / UIRoot）入 ConfigScene，去 DontDestroyOnLoad
- [ ] C6：Login/Main/Gacha/Battle 场景切换改造为 additive 请求模式
- [ ] C7：架构文档同步（更新 08 与 01 架构文档到实际实现）
- [ ] M0 其余（`../02_系统与玩法设计/02_热更新方案.md` + `../01_里程碑与执行/06_里程碑规划与任务依赖.md`）：HybridCLR + YooAsset 集成、热更链路联调、PC+Android 构建矩阵——**2026-09-15 已立项，阶段编号重排为 P1A → P1B → P1C → P2 → P3，见 `../03_专项设计与重构/16_热更新方案选型与落地计划.md`**；原 P0（前置与环境验证）已撤销（内容去向见该文档 §5.0，其中"中国版 2022.3 兼容性实测"因 P1B 换引擎而作废）

**P1A · 通读代码 + 清理冗余代码（2026-09-15 立项，当前执行；来源：`../03_专项设计与重构/16_热更新方案选型与落地计划.md` §5A）**

> 定位：Demo 已外发测试，热更线顺延。在下一个大阶段开发前，插入缓慢调整阶段，目标是"开发者本人能读懂并掌控代码结构"。**纪律：只做结构，不加功能/不改数值/不修 bug（发现的 bug 记入本清单）/不动第三方目录**。

- [ ] **S1 · 先建立验证回路**（**2026-09-18 半达成**）：`unity` CLI 仍不在 PATH、工程无 `com.unity.pipeline`（`unity_status` 失败一次即止）。**但已找到更省事的路子**：用户开着 Editor 时，**编译结果可直接读 `%LOCALAPPDATA%\Unity\Editor\Editor.log`**（取新段、按行号排旧段；模板见 17 文档 §5）→ 建议把"**用户开着 Editor + 我读 Editor.log**"固化为默认验证回路，`unity pipeline install`（原方案 B）降为可选；**仍未解决的是**：无法主动触发编译、无法读 Console 面板、无法操作场景
- [ ] **S2 · 现状地图（核心交付物）**（**2026-09-18 大部分完成**）：**已产出两份**——`Docs/03_专项设计与重构/17_代码现状地图.md`（域边界 / 实测跨程序集依赖边 / 契约层口径 / 搬迁回退映射表 / 遗留问题）+ `Docs/03_专项设计与重构/18_Battle调用链与阅读目录.md`（**`03_Battle` 规模已实测补齐**：63 文件 / 9366 行；含入口生命周期、**每帧驱动顺序表**、启动装配链、1 Model + 11 System 职责表、状态模型字段归属、**21 个事件发出/订阅全表**、9 条功能域调用链、**63 文件清单（可当目录）**、12 条阅读顺序、12 条"会困惑的地方"）
  - [ ] 仍缺（**其余三项已由 18 文档覆盖，只剩这一项**）：**24 个未挂载旧脚本的死代码判定表**——需逐个标注"确认删除 / 保留待迁移 / 失效路径（`PlayerControl.I`、`Bullet_Base`、`EnemyControl.enemys`）"。注意 `Before/` 目录已整体删除，**旧脚本文件本身已不存在**，该表退化为"确认已删干净 + 复核 `2MainScene` 的 Missing Script"
- [x] **S3 · 定边界（唯一架构决策点）——完成（2026-09-18）**：`02_Scripts` 按**业务域**拆为 9 个程序集（`TeaGame.App`/`Menu`/`Gacha`/`Battle` + 共享层 `Data`/`Tools` + 契约层 `Battle.Contracts`），**已编译通过**；`menu ↔ battle` 收口为 0 处互相引用
  - [x] **AOT / 热更归属已定稿（用户 2026-09-18，两轮）**：**AOT 只有 `TeaGame.App`，其余全部热更**（`Data`/`Tools`/`Battle.Contracts`/`Battle`/`Menu`/`Gacha`）。第一版决策曾使 `TeaGame.Tools` 因 `App → Tools` 依赖被迫进 AOT；用户把 `TeaBaseButton`（含 `.meta`、GUID 不变、6 处资产引用未断）搬进 `00_App/Tools/` 后依赖消失 → **Tools 回到热更**。复核：App asmdef 无任何项目内引用、App 源码 0 处热更类型 → 满足「AOT 不得引用热更」。见 17 文档 §7 / §7.1
  - [x] **热更层「禁止 `UnityEditor`」已实测核验通过**：全热更层仅 1 处命中（`BattleSceneRuntimeHost.cs:318`），且有 `#if UNITY_EDITOR` 保护 → 满足 HybridCLR 前提，**P1C 无需为此改动**；副作用（编辑器专属回落）要在迁 YooAsset 时一起看。见 17 文档 §7.2
  - [ ] 仍待办：① 现有 9 程序集 ↔ `Tea.Framework`/`Tea.Game`/`Tea.Data` 三层命名的**改名/归并**（P1C 开工时执行，**不需要重新划边界**）；② `Assets/Game`、`Assets/Engine` 顶层目录是否落地（本阶段刻意未动）
  - [ ] `03_Module` 名实对齐：实测 **0 个 `.cs`**（纯资源目录）；`AGENTS.md` 已修正描述，01 文档待同步
- [ ] **S4 · 机械搬迁与去纠缠（一次一个模块、一次一个提交）**：叶子优先（`Data`/`Tools` → UI 面板 → `03_Battle`）；必须清掉 3 处新旧纠缠（`Weapon_ObjectComponent` 的 `PlayerControl.I.config`、`BattleCombatTypes` 的 `EnemyControl.enemys`、`BattleWeaponRuntime`/`SkillEffect` 的 `Bullet_Base` 运行时禁用）；顺手修 3 条失效硬编码路径。**注意**：`03_Battle` 内部结构本次**一个文件都没挪**，是 S4 的主体
- [ ] **S5 · 删死代码与文档对齐**：删前必跑 GUID 挂载复核（§5A.5 命令模板）；同步 01/02/04 与 `AGENTS.md` 项目事实段
  - [ ] **`2MainScene.unity` 的 `MainCityControl` Missing Script（2026-09-18 实测，需 Editor 操作）**：脚本已随 `Before/` 删除，guid `7976042f492d57b4fba4d6772c9baaf6` 仍被**活场景** `Assets/01_Scenes/2MainScene.unity` 引用 → 需在 Editor 里手动摘掉该组件（其余被删旧脚本只剩已禁用场景引用，无活引用）。来源：17 文档 §6.1
  - [ ] **死代码（已定位未删）**：`03_Battle/Team/BattleUiFlowPanelOpener.cs`（自述无调用者，实测全工程无调用点）、`03_Battle/Team/BattleUiFlowNames.cs`（常量指向已删除的 `BattleMainHubPanel`/`BattleTeamSelectPanel`）。来源：17 文档 §6.5
- [ ] **场景与组件清理（2026-09-15 用户决策，详见 §5A.1b）**：**保留** `4BattleScene`/`5BattleScene-Test0/1`（及 `3GachaScene`）作为美术资产，**场景内旧脚本组件全部摘除，只留美术侧内容**。**铁律：先摘组件、后删脚本**，否则留下 `Missing (Mono Script)`。
  - A 类（只活在禁用资产里，摘组件后删脚本即可）：`EnemyControl`/`EnvEffectControl`/`StoreBattleRoleData`（三个禁用战斗场景）、`EnemyUIData`（`Battle EnemyUIData.asset`，**数据资产随脚本一起删**，需先解除三个场景引用）；`EnemyEntity_Main` 的**唯一挂载点随 `Enemy_0Base.prefab` 一起消失**
  - **`Enemy_0Base.prefab` 已由用户删除（2026-09-15）**，实测安全：`EnemyCatalog` 引用的 21 个 guid = 20 个 typed 敌人 prefab + `EnemyCatalog.cs` 自身，**不含它** → 活敌人生成链路未受影响
  - [ ] **遗留两个 Variant 孤儿**：`Enemy_Test-Ele.prefab` / `Enemy_Test-Fire.prefab` 是已删基座的 Prefab Variant，基座没了它们仍在（Unity 中表现为断开继承）；仅被禁用场景 `5BattleScene-Test1` 引用 → **建议一并删除**
  - B 类（**挂在活资产上，必须进 Editor 手动摘组件**）：`MainCityControl`（`2MainScene.unity`，enabled=1）、`DevMessageDisplay`（`LoadingGroup.prefab`）
  - 已核实：活战斗场景 `BattleGraybox.unity` 挂的全是新脚本、**零旧脚本**；20 个 typed 敌人 prefab **不是** `Enemy_0Base` 的 Variant；四个禁用场景的场景名在代码与 AB 清单中**零引用**
  - [ ] 场景移位（**用户已批准"可以移动"**）：四个禁用场景（`3GachaScene`/`4BattleScene`/`5BattleScene-Test0`/`5BattleScene-Test1`）可移入 `Assets/01_Scenes/Legacy/`；**只移位不改名**（D25 场景名即 key）
  - **gacha 代码**：`Gacha_MainControl`（仅挂 `3GachaScene`）/`Gacha_CoreOperation`/`Gacha_UIOperation`（无任何挂载、无代码引用）→ 保留为参考，移入隔离目录 + README 标注「M2 参考实现，未迁移，不参与运行时」；**不做"封装"**
- [ ] 节奏与纪律（**2026-09-15 更新：不设硬时间盒**——当前无打包/发布计划，时间完全放松）：**闸门从"时间"改为"交付物 + 红旗清单"**；每完成一个 S 步骤汇报一次；任何时刻仓库须"编译通过 + 主链路可玩"；Demo 反馈的阻塞 bug 可打断 P1A（单独提交，不混进重构提交）
  - **"优化地狱"红旗（出现任意一条即停手复盘）**：① 同一文件第三次"顺手改一下" ② 给没有失败用例的代码重做设计 ③ 写"以后可能用到"的通用化机制/接口/基类 ④ 为"更干净"去重命名或移动被引用的资产 ⑤ **开始重写 `03_Battle` 的某个系统**（本项目最大陷阱） ⑥ 连续两个会话只读不改且没产出地图文档 ⑦ 期间开始装 HybridCLR/YooAsset/配 AB ⑧ 一次提交混进两个及以上不相关范围
  - 同等重要的纪律：**"决定不改"也是合格产出**——读完发现某处虽丑但不影响依赖可控性 → 记进地图文档并标注"本次不动"

**P1B · 更新 Unity 版本（2026-09-15 决策：排在 P1A 之后且早于 P1C；来源同上文文档 §5B）**

> 决策：**现在不切引擎**，P1A 先行。目标版本 **Unity 6.3 LTS（`6000.3.x`）**，**不是 6.0**（安全支持 2026-10-16 就到期），也不是 6.5/6.6。
> 依据：`2022.3 LTS 安全支持已于 2025-05-07 结束`（endoflife.date，2026-09-11 数据）；Android targetSdk 合规与 HybridCLR 兼容性都会随引擎过旧而变紧。
> **P1B 必须在 P1C（拆程序集 + HybridCLR + YooAsset）之前完成**——HybridCLR 的 il2cpp 补丁与 Unity 版本强绑定，先装再升等于把这份工作做两遍。

- [ ] **Unity 6 发行版选择（新增首项，影响后续全部工具链）**：Unity 6 **国际版** vs **Unity 6 中国版**。本工程现为中国版 `2022.3.60f1c1`；调研中发现的两个国区问题（CLI 被国区 CDN 重定向成老版本 `0.1.x`、中国版与第三方工具缺少兼容声明）都指向"国际版可减少一类不确定性"，但需确认账号/授权与下载渠道
- [ ] **安装 6.3 LTS 时勾选模块**：Android Build Support（含 SDK/NDK）+ **Windows Build Support (IL2CPP)**（PC 验证链路必需；当前 Standalone 后端为 Mono）
- [ ] **P1B 前置确认清单（开工前须全部有结论）**：① RealToon — 已确认最新版支持 Unity 6，需落实**具体版本号 + 授权可升 + 需重验的材质/Shader 清单**（工程内现为 `v5.0.8p7 / 2022-12`）；② Amplify Shader Editor（独立 asmdef）Unity 6 版本；③ DOTween；④ QFramework / ResKit / UIKit / SupportOldQF 在 Unity 6 下编译；⑤ 中文字体（`Assets/08_Fonts`，TMP 并入 ugui 2.0 后需实测）；⑥ Android Gradle/AGP/NDK 版本要求对 MinSdk 28 / IL2CPP / ARM64 的影响
- [ ] **P1B 迁移面实测结论（好消息）**：Cinemachine **0 引用**（代码与场景皆无）→ Cinemachine 3.x 破坏性改名零成本；`ai.navigation` **0 引用**；全工程唯一挂在 Renderer 上的 RendererFeature 是 URP **内置** `ScreenSpaceAmbientOcclusion` → **URP 17 Render Graph 改造几乎零成本**
- [ ] **P1B 迁移面实测结论（坏消息）**：`Assets/RealToon/.../DeNorSob Outline/` 下 `DepthNormalsFeature.cs`、`DeNorSobOutline.cs` 使用 URP 12 前已废弃的 `RenderTargetHandle` / `renderer.cameraColorTarget`，且 RealToon **无 asmdef → 编入 `Assembly-CSharp`，编不过即全工程编不过**；升级 RealToon 版本后需专项重验
- [ ] **P1B 步骤**：隔离分支 → 升级前快照提交 → 第三方插件逐个升级（RealToon/ASE/DOTween/TMP 字体，各单独提交）→ Hub 装 6.3 LTS 打开工程触发重序列化（**churn 单独一次提交、不含业务改动**）→ 修编译错误（重点 `Assembly-CSharp`）→ 渲染回归（角色着色/描边/后处理/粒子/UI）→ Android 重出包并按 `15` 文档重验 → 同步 `AGENTS.md` 版本事实与 01/02 文档
- [ ] 退出条件：主链路 Play 全绿；Android 真机包可装可玩无渲染异常；`AGENTS.md` 版本段已更新；升级提交与业务提交分离且可整体回退

**P1C · 热更新适配（2026-09-15 编号重排；必须排在 P1B 之后；来源同上文文档 §5C）**
- [ ] **P1C · 热更新适配（原 P0 剩余 + P1 + P2 + P3；必须排在 P1B 之后）**
  - [ ] C1 · 拆程序集与目录（`Tea.Framework` AOT / `Tea.Game`+`Tea.Data` 热更，`Assets/Game`|`Engine`）——**当前工程只有一个 `TeaGame.asmdef`**；边界决策由 P1A 的 S3 产出
  - [ ] C2 · HybridCLR 打通（装 `com.code-philosophy.hybridclr#v8.14.1`、确认 git/VS2019 C++ 组件、跑 `HybridCLR/Installer`、Api Level 改 `.NET Framework`、Standalone 切 IL2CPP、`HotfixAssemblyLoader` + 补充元数据、Generate/All → CompileDll 管线）
  - [ ] C3 · YooAsset 接入与资源迁移（**当前 ResKit AB 已在用**，需写 YooAsset 版 `IPanelLoaderPool` 替换 `ResKitPanelLoaderPool`、`BattleAssetService` 改 AssetHandle；`manifest.json` 加 `com.unity.scriptablebuildpipeline`；输出目录用默认 `Bundles/`）
  - 注明：原 P0 的"中国版 `2022.3.60f1c1` + IL2CPP 打包实测"**已作废**（P1B 换引擎后该组合不复存在）
  - 失败退路：HybridCLR 受阻 → ① 换上一稳定版 ② 才考虑 ILRuntime（代价见 16 文档 §2.1）
- [ ] P2 · 更新流程 + 本地模拟服务器（`VersionService`/`PatchDownloadService`/`PatchRecoveryService`，接 `ConfigScene` 启动流程）
- [ ] **P3 · Android 真机热更闭环验收（2026-09-15 大幅缩减）**：**打包工具与文档建设已完成、不再重做**（[x] 平台配置 IL2CPP+ARM64/MinSdk 28、[x] `AndroidDemoBuilder.cs` 一键构建、[x] `15` 文档门禁与验收清单、[x] ResKit AB 全量构建）。**保留两件事**：① **真机热更闭环本身从未做过**（此前只验过"能出包"，没验过"下发热更能生效"）——小包安装 → 局域网增量更新 → 新内容生效 → 改代码一键构建真机生效，这是 M0 的验收条款；② **P1B 换引擎后 Android 出包链路必须重验**（Gradle/AGP/NDK 随 Unity 6.3 变动，"打包已实现"在 2022.3 成立、在 Unity 6 尚不成立，见 16 文档 §5B.5 第 6 步）
- [x] 已确认：**AB 加密不进本次范围**（保留接口与开关）

**P1A 取证时顺带发现、本阶段只记录不修的问题（来源：2026-09-15 实测）**
- [ ] 3 条硬编码资产路径指向不存在的文件，其中 `BattleArenaSceneThinner` 的 `RigPrefabPath` 无回落 → 该 T3.1C 工具当前**已不可用**（`Assets/04_Prefabs/Battle_PlayerControl.prefab` 实际在 `04_Prefabs/03Battle/`；另两条为 `04_Prefabs/Enemy/StageConfig_Demo.asset`、`Resources/New ButtonControlSetting.asset`）→ 归入 S4 处理
- [ ] `PlayerControl.I` 与 `Bullet_Base` 未被任何场景/预制体挂载，但新代码仍在引用/`TryGetComponent` → **疑似失效代码路径**，需 S2 人工判定（涉及武器/子弹结算，勿贸然删）
- [ ] `Assets/03_Module` 下 0 个 `.cs`（只有资源目录），与 `AGENTS.md`「03_Module = gameplay modules」描述不符 → 归入 S3 名实对齐
- [ ] HEAD `5bc3890c` 单次提交混合了目录重构 + 新功能 + 文档 + `web/data/progress.json`，违反 AGENTS.md 提交纪律 → P1A 期间恢复"一次一个范围"

**P1A · 2026-09-19（表现层分层落地）新增待办（来源：`../03_专项设计与重构/19_表现层分层_View与Actor_设计.md`）**

- [ ] **⚠️ 编译未验（最高优先）**：本会话改动分两截——**已编译通过**：源码时间 ≤ **20:36:24**（`BattleSystems.cs` 20:36:01 / `Battle_MainPanel.cs`、`BattleSkillSystem.cs` 20:36:24 …），产出 `TeaGame.Battle.dll` 122,880 bytes @ **20:37:02**，0 `error CS`、0 非 RealToon `warning CS` → **脱 View、模型平铺主体、`UI/` 纯化均在此截内**；**未编译**：20:45–20:47 的 `BattleRoleSystem.cs` / `BattleRoleSystem.Health.cs` / `BattleArchitecture.cs` / `BattleSceneRuntimeHost.cs` / `BattleStateModel.cs`（Health 合并 + `#region` 分区）。判据：`Editor.log` mtime = **20:37:09**，此后无写入 → Editor 空闲、未感知新改动。**动作：切回 Unity 窗口触发刷新即可**（`Battle.dll` mtime ≥ 全部源码 mtime 即通过）。读日志时**锚定"最后一条 `Csc` 行号向后扫"**，不要按"最后 N 行"取窗口（跨会话日志会命中过期段）
- [ ] **Play 回归（随主链路一起做）**：主 HUD 面板能否打开；技能行/血条/队伍血条/弹药显示是否正确（`ReloadProgress` 改为原始 0..1 后，Panel 侧百分比与节流是否正常）；切人与阵亡后的显隐与读数；翻滚/冲刺（力由宿主交给 Actor 施加后手感是否一致）
- [ ] **步骤 6 · 最小引用注入（19 文档 §6；收益更根本、风险更大）**：`BattleRoleSystem.Initialize(Battle_PlayerControl controller, …)` 只要 `InitialRoleIndex`/`ControlTakeOff`/`PlayerConfig` 三个引用却要整台 rig；`BattleRoleSystem.Health` 写 `State.Controller.ControlTakeOff`。目标是让 System 不再依赖"场景引用总仓库"类型（动所有 System 的装配签名）
- [ ] **瞄准量下沉（19 文档 §7）**：`AimPosition`/`AimLerpSpeed` 仍是 `BattleStateModel` 里的**视图空间量**。需把 Canvas 投影搬进 `Battle_MainPanel.TickForBattle`（用 `BattleRuntimeBridge.Camera`），System 侧只留 `LockEnemy`/`LockSpeedMultiplier`。**完成前 `BattleAimSystem` 内有一份重复的 `AimReferenceWidth` 常量**（已在注释标明待删）
- [ ] **`18_Battle调用链与阅读目录.md` 需完整重算**：文件清单/行号/§5 系统表仍是迁移前快照（已在头部加更正表标注）。目录现状：`Player/` → **`Actor/`**（含 4 个 Actor）、新增 **`Role/`**（`BattleRoleSystem.cs` + `.Health.cs` + `BattleTeamRuntimeBuilder.cs`）、`Team/` **删除**、`BattleSceneRuntimeHost.cs` 移到 **`03_Battle/` 根**、`UI/BattleUiSystem.cs` → **`Core/`**、`UI/BattleHudState.cs` **删除**
- [x] **`17_代码现状地图.md` §1.3 目录树** 已同步（2026-09-19：`Player` → `Actor`、新增 `Role/`、`Team/` 删除、host 移到 `03_Battle/` 根、`UI/BattleUiSystem` → `Core/`、`UI/BattleHudState` 删除），并在头部加"增量（二）"banner 修正措辞
- [ ] **本会话新增的关键坑（建议并入 09 关键坑清单，已写在 09 2026-09-19 段）**：① `replace_all` 替换文本别自带 `.Value`；② 删类前查全工程**真实代码**引用（`BattleHudState.AimReferenceWidth` 教训）；③ `Editor.log` 必须锚定最后一条 `Csc` 向后扫

**P1A · 2026-09-18（域拆分会话）新增待办与待确认项（来源：`../03_专项设计与重构/17_代码现状地图.md`）**

- [ ] **待用户执行（Play 回归，S3 落地后的必须项）**：主链路 `0ConfigScene → Login → 2MainScene → 选关 → 编队 → BattleGraybox → 结算 → 返回` 走一遍；并抽查 prefab/场景引用未断（`Assets/04_Prefabs/Panel/Formation/Main_FormationPanel.prefab`、`LevelSelectPanel.prefab`、`BattleGraybox.unity` 上 `battle_runtime` 的 `teamCatalog`）与 Editor 菜单可用性（`Thrud/Battle/T3.1C/…`、`Thrud/Demo/Android/0. 构建前置检查`）。**编译已通过但运行未验**，见 17 文档 §5
- [x] **gacha 还原后的编译 —— 已核验通过（2026-09-18 20:38）**：`Library/ScriptAssemblies/TeaGame.Gacha.dll` 产出（23,040 bytes），该轮 `error CS` = 0、非 RealToon 警告 = 0，`will not be compiled` 提示消失。
  - [ ] 备注（非缺陷）：`Legacy/` 三脚本**会被编译进 `TeaGame.Gacha`**（随热更 DLL 进包约 +23KB），但无调用入口、无资产挂载。若 P1C 要其彻底不进包：直接删除 or 给 `Legacy/` 加嵌套 asmdef + 未定义 `defineConstraints`。17 文档 §6.6
- [x] **`TeaGame.Gacha` 空程序集提示 —— 已决策并落地（2026-09-18）**：保留域 + 新增 `02_Gacha/GachaModule.cs` 占位框架（消除 `will not be compiled, because it has no scripts` 提示）。17 文档 §6.2
- [x] **gacha 参考代码处置 —— 已决策并落地（2026-09-18）**：从 git `5bc3890c` 原样还原 3 个脚本到 `02_Gacha/Legacy/`（GUID 保留）+ 新增 `02_Gacha/README.md` 标注「M2 参考实现，不参与运行时」。唯一必要改动是切断对已删除 `LoadingControl` 的调用（不迁移行为）。17 文档 §6.6
- [x] **`GachaArchitecture` 类名与文件名不符 —— 已由用户修复（2026-09-18）**：类名改为 `GachaArchitecture`（`Architecture<GachaArchitecture>`）、`Init()` 留空，`CS0436` 消失、该轮 `warning CS` 归零。遗留（非阻塞）：该文件 XML 注释仍是 `AppArchitecture` 的复制残留。见 17 文档 §7.3
- [x] **HUD 重构的编译 —— 已核验通过（2026-09-19 00:04）**：技能 HUD 迁至 `Battle_MainPanel`（`BindableProperty` 反向绑定）+ 新增 `UI/BattleHudState.cs`；改动涉及 `BattleSystems.cs`（2 处）/ `BattleSkillSystem.cs`（3 处）/ `BattleSceneRuntimeHost.cs`（4 处）。`Library/ScriptAssemblies/TeaGame.Battle.dll` 重新产出（119,808 bytes，00:04:49），该批 **`error CS` = 0、非 RealToon 警告 = 0**，`will not be compiled` 提示（Gacha 空程序集）也一并消失。静态自检：无残留引用、无重复类、prefab 7 个字段全绑定
  - [ ] **但运行未验**：主 HUD 面板能否正常打开、技能行是否正确显示，需 Play 一次（随下面的 Play 回归一起做）
- [ ] **待用户在 Editor 清理（否则上屏有两个 HUD）**：① 删除 rig prefab（`Assets/04_Prefabs/03Battle/Battle_PlayerControl.prefab`）上的 HUD 对象（`SkillHud*`/`Health_Main`/`Health_Value`/`Cross_LockEnemy`）并重跑 CodeGen；② **重打 AB** 以生成 `QAssetBundle.Battle_mainpanel_prefab`，之后可把 `Battle_MainPanel` 里硬编码的 AB 名改回常量；③ 删除空目录 `Assets/02_Scripts/UIPanel/Battle/` + 孤儿 meta
- [ ] **待决策：两个技能事件是否删除**：`BattleSkillEnergyChangedEvent` / `BattleSkillCooldownChangedEvent` 的唯一订阅方就是本次迁走的代码，现**无订阅方**。建议留到 T3.3 正式 HUD 时再定
- [ ] **表现层分层方案（待确认）**：见 `../03_专项设计与重构/19_表现层分层_View与Actor_设计.md`——① 是否采纳三通道分流与 6 步路线；② 步骤 3（Actor 语义层）与通道二（最小引用注入）谁先做；③ 敌人的 `EnemyActorView` 是否提前做（接正式美术前）；④ 规范是否进 `AGENTS.md`
- [ ] **表现层迁移的具体待办（方案确认后开工）**：步骤 1 主 HUD 剩余部分（`BattleViewSystem` 的血条/准星/弹药 → `Battle_MainPanel`）→ 步骤 2 队伍血条归属 → 步骤 3 玩家 rig Actor 语义层 → 步骤 4 敌人轻量版 → 步骤 5 `BattleViewSystem` 降级改名、`InitializeView()` 消失 → 步骤 6（可选）最小引用注入。**纪律：一个 View 一次提交**
- [ ] **App 层硬编码面板 AB 名单（字符串级耦合，非类型依赖）**：`00_App/ResKitBootstrap.cs` 的 `ExpectedPanels` 列了 6 个 battle/menu 面板的 `QAssetBundle` 常量名——不违反程序集边界，但是**新增面板时最容易漏改的一处**（热更后尤甚）。当前只被已注释掉的 `Validate()` 使用（休眠代码），本次不动。17 文档 §6.3
- [ ] **契约层纪律（后续新增类型时照此判定，避免滚成新的 God assembly）**：只有一句判据——**Menu 是否需要看见它**；需要就下沉，不需要就留在 `TeaGame.Battle`。下沉时**必须连带它的公开面**（字段类型、`[Attribute]` 类型），否则契约层反向依赖 Battle 成环。17 文档 §3.2
- [ ] **场景移位（2026-09-15 已批准，未执行）**：四个禁用场景（`3GachaScene`/`4BattleScene`/`5BattleScene-Test0/1`）可移入 `Assets/01_Scenes/Legacy/`，**只移位不改名**（D25 场景名即 key）
- [ ] **两个 Variant 孤儿（2026-09-15 已识别，未处理）**：`Enemy_Test-Ele.prefab` / `Enemy_Test-Fire.prefab` 是已删基座 `Enemy_0Base` 的 Prefab Variant，建议一并删除（仅被禁用场景 `5BattleScene-Test1` 引用）

### 3.4 真机资源与战斗装配（2026-09-10 新增；来源：`../03_专项设计与重构/14_真机资源与战斗装配_分阶段修复指南.md`）

> ⚠️ 编号消歧：本节的 **P0–P4 是 14 文档自己的阶段编号**（真机资源修复），与 `16` 文档的全局阶段编号（P1A/P1B/P1C/P2/P3）**不是同一套**，勿混用。

> 纪律：**一次只做一个阶段**，每阶段退出条件不满足就停下回报，不进入下一阶段（详见 14 §0）。

**P0 · 角色资源走显式 AB + 清错误噪音（当天，约 0.5h）**
- [ ] **T0-1（用户/编辑器，1 分钟）**：执行菜单 **`Thrud/Demo/Characters/1. 标记编目为 AB (battle_characters)`**（幂等 + 自动校验；等价替代：Inspector 最底部 AssetBundle 下拉 → New… → `battle_characters`）。只标 `Assets/Data/BattleTeamCatalog.asset` 一个根资产，角色/武器 SO、模型 prefab、贴图作为依赖闭包自动入包（**不要逐个标**；**不要**给 `Assets/Data/BattleLevels/**` 打标；**不要**用 QFramework 自带的 `Assets/@ResKit - AssetBundle Mark`——它按目录名当 bundle 名，会把关卡数据卷进去）
- [ ] **T0-2（用户）**：菜单 `Thrud/Demo/Characters/2. 构建 AB（当前平台）并校验`（或 `Thrud/Demo/Android/2. Build AssetBundles (Android)`）→ 出包
- [x] **T0-3（C#，已落盘未编译）**：新增 `03_Battle/Team/BattleAssetService.cs`（常驻 loader + AB 解析 + 明确日志）；`BattleSceneRuntimeHost.ResolveTeamCatalog()` 与 `Main_FormationPanel.ResolveCatalog()` 改 AB 优先
- [x] **T0-4（已落盘）**：新增项目级 `Assets/link.xml` 保留 `MeshCollider`/`BoxCollider`/`SphereCollider`/`CapsuleCollider`/`Rigidbody`/`MeshFilter`/`MeshRenderer`（见 14 D1；**不关** Strip Engine Code）
- [x] **T0-5（C#，已落盘未编译）**：`M1_TeamSelect` 文案收口（`FormationEntrySceneName = "2MainScene"` + 错误信息指向入口与 AB 打标要求）
- [x] **T0-6（C#，已落盘；首轮实测修复）**：`BattleAssetService` 加载前先做只读清单判定（命中才 `LoadSync`）并给 `LoadSync` 包 try/catch；`UiPanelFlow.TryOpen` 异常时收掉半开面板（QFramework 在清单缺失时会对 null 取 `.Asset` 抛 NRE → 打断面板打开，见 09 关键坑 29）
- [x] **T0-7（Editor 工具，已落盘）**：新增 `02_Scripts/Tools/Editor/BattleCharacterAbSetup.cs`——走 ResKit API 的「标记 / 构建并校验 / 只读校验 / 取消标记」菜单（把 T0-1/T0-2 变成幂等的一条命令，且只标编目一个根资产）
- [x] **T0-8（C#，已落盘；第二轮实测修复）**：`BattleAssetService` 兼容两种 bundle 名（规范名 `battle_characters` + Inspector 默认名 `battleteamcatalog_asset`，避免重打 40MB+ AB）；`ResKitBootstrap.Validate()` 增加角色/武器编目 AB 启动校验；装配失败原因拆成「会话为空 / 编目未解析到 / 编目命中但 id 未命中」三条独立日志（见 09 关键坑 30）
- [ ] ⚠️ **已实测坑**：Inspector 里 AssetBundle 下拉的「New…」默认名是 `<资产名小写>_<扩展名>`；用默认名会与代码常量脱钩，且**只有真机会暴露**（编辑器有 AssetDatabase 回落）。建议统一走 `Thrud/Demo/Characters/1`
- [ ] 提示：**编辑器验证不需要重打 AB**——模拟模式清单来自 AssetDatabase 快照，T0-1 标记后重新进 Play 即可看到 `[BattleAssets] …已从 AB 加载并常驻`；只有真机必须重打 AB（T0-2）
- [ ] 退出条件：Console 有 `[BattleAssets] 角色/武器资源已从 AB 加载并常驻`；无 `未能从 AB 解析出战编目`/`无法装配出战编队`/`MeshCollider`；角色出现且材质不发粉；可移动/开火/切人；退出能回 2MainScene

**P1 · 面板加载健壮性真机回归（0.5 天，不新增功能改动）**
- [ ] 前置：编辑器刷新 0 编译错误（上一轮 `UiPanelFlow`/`ResKitBootstrap` 改动尚未编译验证）；并补跑一次「关掉 SimulationMode」
- [ ] 真机：触摸层/暂停/结算均能出现；**重开本关后再进关触摸层仍出现**（核心用例）；无 `MissingReferenceException`；启动日志有 `[ResKit] 初始化校验通过`

**P2 · AB 加载异步化（1 天）**
- [ ] `UiPanelFlow.TryOpenAsync<T>`（先清理面板表死条目 → `UIKit.OpenPanelAsync` → 判空 + **超时兜底**）
- [ ] 调用点改造：触摸层 → 主菜单/选关/编队 → 战斗内结算/暂停（需 `BattleSceneRuntimeHost` 提供协程入口，`BattleUiSystem` 非 MonoBehaviour）
- [ ] 边界：只覆盖 `LoadFromFileAsync`/`LoadAssetAsync`；`Instantiate` 与字体图集上传仍在主线程（不会归零卡顿）
- [ ] 退出条件：各面板首开/复开打点计时对比 P1 基线明显下降，无白屏/无交互失效

**P3 · AB 划分与体积（1~2 天，与 P2 解耦）**
- [ ] T3-1 清掉 `Main_FormationPanel` prefab 上序列化的 `catalog` 引用（改完全注入）——消除"角色资源靠 UI prefab 意外引用"的最后一条（P0 已把编目搬出面板包）
- [ ] T3-2 抽出 `ui_shared` 共享依赖 bundle（中文字体等；当前 formation 43.8MB / main_menu 20.3MB / loadinggroup 14.0MB / levelselect 7.7MB / touch 5.2MB）
- [ ] T3-3 取消 `LoadingGroup.prefab`、`Main_MenuPanel.prefab` 的 AB 标记（两者都被场景实例引用）；统一 LoadingGroup 取用口径（`SceneFlow` 的 Resources 回落路径真机必然 null）

**P4 · 角色/武器资源的完整热更化**：**不属于 M1**。P0 已把角色/武器资源搬进独立 AB（`battle_characters`）并由 `BattleAssetService` 常驻持有；清单/版本/增量更新/主动卸载等留待 `../02_系统与玩法设计/02_热更新方案.md`（HybridCLR + YooAsset）阶段立项，届时 `BattleAssetService.Release()` 即现成卸载入口

### 3.5 横切提醒（涉及数值/伤害/元素/血条改动前必读）

- [ ] **必读** `../06_问题存档/12_伤害构成_怀疑存档.md`：2026-09-06 用户观察到「伤害构成疑似存在问题」（未给复现细节）。调试前先向用户索要：复现步骤 + 期望 vs 实际 + 飘字/血条/日志三处是否一致。重点怀疑点：新旧两套子弹双重结算、克制倍率/风易伤叠乘、元素状态伤害重复计入、成员切人时 DoT 归属。

### 3.6 工作区与提交（来源：`../01_里程碑与执行/13` §P9、`../05_开发日志/10_开发日志_2026-09-05_收工总结.md` §4）

- [x] `13_手游试玩Demo_规划.md` 已入库（随 2026-09-08 Docs 重组提交 `2be69fc1`）
- [ ] 工作区 3 处保留修改：`Assets/08_Fonts/Before/Default.asset`（字体 churn，勿与业务混提）；`Assets/Data/RoleItemDatas/2001001-Faria.asset`、`2004001-Willa.asset`（HP 60/40 压测残留 → 数值定稿后改回 MaxHp≈400 基准再提交）
- [ ] 提交纪律：按「卡片相关 + 生成 churn」分类提交，`Assets/**` 与 `.meta` 成对

### 3.7 `web/` 进度面板（来源：`../05_开发日志/09_开发日志_会话交接.md` 2026-09-14 会话）

- [x] **v2 收口已完成**：两分页结构（任务汇总 / 全部明细）、`tasks` 主实体、任务新增/删除/行内编辑、`workspace(path)` / `commits(hash)` 主键 PATCH/DELETE、单文件预览页与 README 同步
- [x] **构建与预览验证已通过**：`npm run build`、`npm run preview:verify`
- [x] **本地写接口烟测已通过**：临时启动服务后执行 `POST /api/progress/tasks` 新建测试任务再 `DELETE` 删除，任务数 48 → 49 → 48
- [x] **UI 微调已完成**：第一页任务表列顺序调整为「方向 / 任务 / 类型 / 描述 / 状态 / 优先级 / 操作」，并新增亮/暗色切换按钮；主题使用本地持久化值，不跟随系统主题
- [ ] 若要正式投入使用：在 `web/server/.env` 配置 `ADMIN_TOKEN`，按 `web/README.md` 本地或宝塔启动，并做一次人工页面联调（编辑模式、备份目录、错误提示文案）
- [x] **任务清单已于 2026-09-15 人工重排**（`web/data/progress.json`，77 条）：按 **P1A → P1B → P1C → P2 → P3** 组织，标题带阶段前缀、描述改成口语化拆解、id 改用 `p1a-1` 这类稳定值；`sortOrder` 按阶段分桶（`0` = P1A 验证回路闸门，`2` = P1A 其余，`3` = 真机资源/Demo 线，`4` = P1B，`6` = P1C，`7`/`8`/`9` = 后续与收尾，`5` = 已完成留档）
- [ ] ⚠️ **注意**：`web/sync-doc-tasks.js` 是**整体覆写**式生成器（从本文档重新抽取 + 重发随机 id），**直接跑会丢掉上面这次人工整理**。若要重新同步，先备份 `progress.json`，或先把它改成按 `raw` 合并（保留 id/status/priority/sortOrder）

---

## 4. M2 / M3 备忘（非近期，仅保留方向）

- **M2 外围系统**（`../02_系统与玩法设计/04_外围系统设计.md` + `../04_模块设计/00_模块索引.md`）：存档（多槽/迁移/备份）、配置表工具、2D 主城功能页、三线养成、背包货币、抽卡。
- **M2 依赖线**（04_外围系统 §4/§5）：主城入口「出战/编队」M1 起可用，其余角色/武器/符灵/抽卡/背包等 M2。
- **M3 内容与品质**：正式美术接入、内容扩量、数值平衡、性能达标、埋点复盘（`../01_里程碑与执行/06`）。
- 升级预留（卡片内“升级预估”）：数据层拆分成长实体、传参迁 M0 SceneFlow Payload、DamagePipeline 框架化、倒地状态机、每角色独立能量等——见 07 对应卡，供 M2/M0 参考，不阻塞。

---

## 5. 文档一致性待办（重组后顺手修）

| 待办 | 来源说明 |
|------|----------|
| [ ] 07：T2.1/T2.2 卡 checkbox 未勾选但当日已验收、T2.2 执行记录仍写“（待实现）”——核对补勾回填 | `../01_里程碑与执行/07` Phase 2 |
| [x] 07：未登记 T3.1C（11）与 13 Demo 计划——**已完成**：07 插 T3.1C 卡并注明 Demo 线顺序；13 计划随 Docs 重组入库 | `../01_里程碑与执行/07` 底部重构记录 |
| [x] 09：头部日期/进度/Phase 3 计划/提交记录——**已完成**（2026-09-08 更新，含 T3.1C 与 Demo 线） | `../05_开发日志/09_开发日志_会话交接.md` |
| [x] M1_TeamSelect：v1.3 已声明验收，但 §10 验收清单 checkbox 未回填、§9/§10 旧“M1_Menu/M1_TeamSelect 场景链”步骤已被覆盖层主路径取代——**已完成**：顶部状态、§9/§10 与变更记录均已按 v1.3 主路径回填并标注历史背景 | `../03_专项设计与重构/M1_TeamSelect_预编队面板_重构设计.md` §9/§10/§12 |
| [x] 11：§7 回归基线 6 项 checkbox——**已完成**（2026-09-08 全绿勾选，状态头同步） | `../01_里程碑与执行/11_Battle_Arena_薄场景化重构_设计.md` |
| [x] M1_TeamSelect 旧 `Resources/Panels/BattleTeamSelectPanel`（D8 承诺验收后删除）是否已删——**已核实**：工程内不存在；旧 `M1_MainHub.unity` / `M1_TeamSelect.unity` 也已删除，当前仅保留 `2MainScene` 覆盖层主路径 | `../03_专项设计与重构/M1_TeamSelect_预编队面板_重构设计.md` D8/A8 |
| [ ] 跨引用旧名：00 文档“文档地图”等处写 `Modules/`，已更名为 `04_模块设计/`——如顺手可改 | `../00_项目总览/00_项目总览与决策记录.md` |

---

*更新规则：完成一项即在对应来源文档勾选 `[x]`，再回来更新本清单与 `../README.md` 状态列，并刷新本文件日期。*
