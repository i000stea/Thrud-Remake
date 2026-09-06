# M1\_TeamSelect 预编队面板（Main\_BattleTeamSelectPanel）重构设计

> 版本：**v1.0（已冻结，2026-09-05 用户评审定稿）**
> 状态：设计定稿 → 进入实现（A8：Editor 方式搭建 prefab，先移除过时 Editor 方法）。
> 对应：07 指南 T3.1A · 面板 UI 重构；承接 `10_开发日志_2026-09-05_收工总结.md`。

***

## 1. 背景与目标

现 `Main_BattleTeamSelectPanel`（CodeGenKit UIPanel 骨架，`Assets/02_Scripts/UIPanel/`，prefab 在 `Assets/04_Prefabs/Panel/`）为空壳。本次重构为 **「4 槽卡位式」预编队 UI**，交互逻辑用 **QFramework CodeGenKit Bind + 轻量 MVC**。

**目标交互**：

1. 横排 **4 个大元素（槽）**；每槽**上=角色、下=武器**两半；每半中心区域可点击；显示「空」或对应元素（灰盒=文本占位）。
2. 点击半区 → 弹出**列表新框**；**单击=看简介，双击=直接执行替换并关闭**（后续该区域将扩展“对选择项进行调整”，如角色/武器强化等）。

## 2. 决策记录（最终）

| #   | 议题        | 决策                                                                                                                                                        |
| --- | --------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D1  | 槽位与人数     | 4 槽常显、可为空；**空槽不参战**（统计“角色+武器都填”的槽，1–4 人，槽序=切人序）                                                                                                           |
| D2  | 去重        | **全局唯一**：同一角色/武器最多占一槽                                                                                                                                     |
| D3  | 弹窗交互      | **单击=显示简介；双击=直接执行**并关闭弹窗（双击窗口 **300ms**）；模态（遮罩不关闭）；「取消」；**「空」恒为列表第 1 项**                                                                                  |
| D4  | 底部控制条     | **清空全队 + 开始战斗 + 返回（→0ConfigScene）**                                                                                                                       |
| D5  | 唯一性冲突     | 已被占用的项**置灰不可点、排列表末尾**，标注「已占用·槽 N」                                                                                                                         |
| D6  | 弹窗结构      | **单一弹窗容器复用**（列表 + 简介区 + 取消）；条目运行时动态生成，兼容扩容                                                                                                                |
| D7  | 列表项内容     | 角色项=名称+元素标签；武器项=名称+子类标签；灰盒文本占位                                                                                                                            |
| D8  | 主路径       | 主路径=`Main_BattleTeamSelectPanel`（04\_Prefabs/Panel，AB=`main_battleteamselectpanel_prefab`）；旧 `Resources/Panels/BattleTeamSelectPanel` 验收后删除（Resources 清理） |
| D9  | MVC       | 独立 Model + 面板控制器，留便捷扩展点；暂不建全局 Architecture                                                                                                                |
| D10 | 流程/初始     | **M1\_Menu 废弃**；灰盒直连 Play `M1_TeamSelect`                                                                                                                 |
| D11 | prefab 搭建 | **A8：Editor 脚本程序化生成 prefab（YAML 资产）**；创建前先移除过时 Editor 方法                                                                                                  |
| D12 | 局外绑定(A2)  | **方案 A：`IBattlePlayerDataService`** **接口 + 内存实现**（全量编目 + 内存出战配置）；局外系统完成后换真实实现，UI/Model 不动                                                                 |
| D13 | 返回目标(A3)  | 灰盒「返回」/战斗「退出」→ **0ConfigScene**；引入来源场景机制（`BattleTeamSession.SourceSceneName`），由未来入口/启动类写入                                                                 |
| D14 | 简介区/双击    | 简介区在**弹窗内右侧 DetailPane**（后续强化操作区）；双击窗口 **300ms**（确认）                                                                                                      |

## 3. UI 布局与交互规格

### 3.1 静态布局（prefab 结构基线，Editor 生成）

```
Main_BattleTeamSelectPanel（根：RectTransform+Image+UIPanel）
├── Header → TitleText  「出战编队」
├── SlotRow（横排 4 槽）
│   └── Slot_1~4（卡片）：
│       ├── RoleArea（上半）→ RoleButton + RoleText  （空/角色名+元素）
│       ├── Divider
│       └── WeaponArea（下半）→ WeaponButton + WeaponText（空/武器名+类型）
├── BottomBar → ClearAllButton「清空全队」 StartButton「开始战斗」 BackButton「返回」
└── PopupWindow（默认隐藏）
    ├── PopupMask（半透明全屏，仅拦截点击=不关闭）
    ├── PopupPanel
    │   ├── PopupTitleText（选择角色/选择武器）
    │   ├── ItemList（左，可滚动：VerticalLayoutGroup+ScrollRect）→ ItemTemplate（Button+Text，运行时克隆）
    │   ├── DetailPane（右侧简介区：detailTitleText/detailBodyText；**后续强化等操作区**）
    │   └── PopupCancelButton「取消」
```

### 3.2 弹窗条目交互

| 事件               | 行为                                            |
| ---------------- | --------------------------------------------- |
| 单击某条目            | 高亮该条目；`DetailPane` 显示简介（不关闭、不生效）；再次单击其他项切换简介  |
| 双击（300ms 窗口内第二击） | **立即应用**：替换当前槽半区 → 关闭弹窗 → 刷新 4 槽；双击「空」= 清空该半区 |
| 已占用条目            | 置灰、单击/双击无效，**排列表末尾**，标注「已占用·槽 N」              |
| 「取消」             | 关闭弹窗，放弃浏览                                     |
| 点遮罩              | 无动作（模态）                                       |

- 简介（灰盒）：角色=`itemName/enName/元素/profile`；武器=`itemName/类型·子类/damage/gunshot/reload/magazineSize/skillDescribe`（截断）。

- DetailPane 为后续“强化/微调”扩展点（本条目不实现强化）。

### 3.3 槽位与底部 / 校验

| 操作      | 行为                                                                       |
| ------- | ------------------------------------------------------------------------ |
| 点槽 i 上半 | 打开角色弹窗：`[空]` + 未占用角色（编目序）… + 占用项置灰末尾                                     |
| 点槽 i 下半 | 打开武器弹窗：同上                                                                |
| 清空全队    | 4 槽置空 → 刷新                                                               |
| 开始战斗    | 校验：全空→提示；半满槽→提示并定位；通过→写 `BattleTeamSession` → `LoadScene(BattleGraybox)` |
| 返回      | `LoadScene(BattleTeamSession.ReturnSceneName ?? "0ConfigScene")`（见 D13）  |

## 4. 数据分层（A2 定稿：方案 A）

| 层       | 内容             | 生命周期    | 归属                                   |
| ------- | -------------- | ------- | ------------------------------------ |
| 局外·账号数据 | 持有/等级/成长/强化    | 持久化     | 未完成（M2），**本卡不触**                     |
| 局外·出战配置 | 4 槽 角色×武器 绑定方案 | 持久化（可选） | **`IBattlePlayerDataService`（本卡定义）** |
| 局内·出战会话 | 本局 1–4 成员草稿    | 内存      | `BattleTeamSession`（已有）              |

- `IBattlePlayerDataService`：`GetAvailableRoles() / GetAvailableWeapons()`、`LoadFormation() / SaveFormation(4 槽)`。

- 灰盒实现 `MemoryBattlePlayerDataService`：编目全量开放；出战配置**内存保存**（每次启动重置，绝不落盘）。局外系统完成后替换为账号实现，View/Controller/Model 不动。

- 语义注意：绑定关系是**槽位级配装方案**（自由搭配），非“角色私有装备”。

## 5. MVC 结构

```
Assets/02_Scripts/03_Battle/Team/MainTeamSelect/
├── Main_TeamSlotDraft.cs           槽草稿（Role/Weapon 可空 + IsComplete）
├── Main_BattleTeamSelectModel.cs   4 槽 + 编目 + 唯一性查询/变更 + FormationChanged 事件
├── BattlePlayerDataService.cs      接口 + Memory 实现（灰盒）
Assets/02_Scripts/UIPanel/
├── Main_BattleTeamSelectPanel.cs           控制器（交互/校验/弹窗/写服务与 Session）
├── Main_BattleTeamSelectPanel.Popup.cs     弹窗逻辑 partial（可选拆分）
└── Main_BattleTeamSelectPanel.Designer.cs  CodeGen 产物（Bind 字段契约 §6）
```

扩展点：列表条目“应用前回调”（后续强化入口）；`IBattlePlayerDataService` 为账号/存档接入缝；槽位数=`BattleTeamSession.MaxTeamSize`。

## 6. CodeGen Bind 字段契约（Editor 生成时同名挂 Bind）

| 字段名                                           | 类型                          | 说明               |
| --------------------------------------------- | --------------------------- | ---------------- |
| titleText                                     | Text                        | 标题               |
| slotRoleButtons\[4] / slotRoleTexts\[4]       | Button\[] / Text\[]         | 4 槽角色区           |
| slotWeaponButtons\[4] / slotWeaponTexts\[4]   | Button\[] / Text\[]         | 4 槽武器区           |
| clearAllButton / startButton / backButton     | Button                      | 底部三键             |
| popupMask                                     | Button/Image                | 遮罩               |
| popupTitleText                                | Text                        | 弹窗标题             |
| itemListRoot                                  | RectTransform               | 条目容器（ScrollRect） |
| itemTemplate                                  | Button                      | 条目模板（隐藏，运行时克隆）   |
| detailPane / detailTitleText / detailBodyText | RectTransform / Text / Text | 简介区              |
| popupCancelButton                             | Button                      | 取消               |

> Editor 生成器为这些节点挂 Bind（按命名），CodeGen 后 Designer 产生字段；若实际产物有出入以 CodeGen 为准。

## 7. A3 流程（定稿）

- M1\_Menu 废弃；灰盒：Play `M1_TeamSelect` 直达。

- `BattleTeamSession` 增 `SourceSceneName`（来源场景）；入口/启动类写入；「返回」与暂停/结算「退出」统一回 `SourceSceneName`，为空时回 **0ConfigScene**。

- 未来：ConfigScene battle 启动类填 `SourceSceneName + 关卡数据` 直接进流程；面板与退出逻辑不写死菜单场景名。

## 8. A8 实施说明（定稿）

- prefab 采用 **Editor 生成脚本**（程序化建层级 + 挂 Bind + `SaveAsPrefabAsset` 产出 .prefab/YAML），非手写 YAML。

- **创建新生成器前，先移除过时 Editor 方法**：

  - `Assets/02_Scripts/03_Battle/Editor/BattleTeamFlowSetup.cs`（旧 Setup All：M1\_MainHub 主场景 + Resources/Panels 旧格式面板 + 场景重建）→ 移除；

  - 其关联的运行时旧引导/旧警告文本随主路径切换不再使用（`BattleUiFlowPanelOpener` 的“运行 Setup All”提示更新或移除）；

  - 旧 `Resources/Panels/BattleTeamSelectPanel` 备胎在验收后删除（D8）。

## 9. 实施步骤（顺序）

1. 移除过时 Editor 方法（§8）并提交；
2. 编写新 Editor 生成器 `Main_BattleTeamSelectPrefabBuilder`（依 §3/§6）→ 用户编辑器内运行 → 产出 prefab + AB 标记；
3. 用户对 prefab 执行 CodeGen（生成 Designer 字段）后回传；
4. 代理实现 Model/Service/面板逻辑（§5）→ 离线两级编译自检；
5. `M1_TeamSelect` 场景接线（catalog + 打开新面板；返回 0ConfigScene）→ 用户打 AB → Play 验收（§10）；
6. 验收后清理旧 Resources 面板（D8）。

## 10. 验收清单（T3.1A-5 范围）

- [ ] Play `M1_TeamSelect` 直达；4 槽全空；

- [ ] 半区点击弹列表：`[空]` 第 1 项；未占用按编目序；占用项置灰末尾并标注槽 N；

- [ ] 单击→简介区显示不生效；双击→应用并关闭；取消/遮罩正确；

- [ ] 1–4 人自由搭配（唯一约束）；半满/空校验；写 Session 进战斗，切人序=槽序；

- [ ] 结算/暂停「退出」回 0ConfigScene（或 SourceScene）；直连 BattleGraybox 默认编队兜底回归。

## 11. 架构修正与 MVC 细则拆分（v1.2 · 已确认）

### 11.1 层归属修正（用户反馈）

问题：上一版把编队数据/逻辑放在 `03_Battle/Team/MainTeamSelect/`（战斗模块）——归属错误。

对齐 `Docs/Modules/00_模块索引.md / 04_Formation_模块设计.md / 06_Grow_模块设计.md`：
- **`02_PlayerMeta` = 局外养成工作台（Out-of-Battle / Player Meta）**：覆盖 持有与档案(Profile/Bag) → 养成线(Grow) → 编成(Formation) → 详情与展示，是菜单侧对玩家开放的“培养 + 组队”总区；**Formation 子域仍沿用该名**（上阵位编辑/唯一性/预设/入场快照，不属于 Battle）；
- **Battle 只接收编队结果快照**（`BattleTeamSession`），不反向修改编队状态；
- 模型表现层采用**方案①（轻契约+容器+上下文适配器）**，当前灰盒不实现、仅留接入缝（见 §11.4）。
- 本卡（T3.1A 灰盒）PlayerMeta 只落 Formation 的“战前出战准备”深度；Grow/Profile/Bag 依赖留到 M2。

### 11.2 目录（定稿）

```
Assets/02_Scripts/02_PlayerMeta/            # 局外养成工作台（Player Meta，新）
├── Formation/                              # 编成子域（本卡 T3.1A）
│   ├── Main_TeamSlotDraft.cs               # 槽草稿（角色×武器 绑定）
│   ├── Main_BattleTeamSelectModel.cs       # 4 槽 + 唯一性规则（纯 C# Model）
│   ├── BattlePlayerDataService.cs          # IBattlePlayerDataService + Memory + 定位器
│   ├── MainTeamSelectViewContract.cs       # IMainTeamSelectView（V 接口）+ 列表项 VO
│   └── Main_BattleTeamSelectController.cs  # 纯 C# Controller（细则拆分的核心）
├── Grow/  Profile/  Bag/  …                # M2 起按模块补齐（含 VisualDescriptor 表现契约）
Assets/02_Scripts/UIPanel/                  # 沿用 CodeGenKit UI 目录（Prefab 根脚本仍在此）
├── Main_BattleTeamSelectPanel.cs           # 薄壳（UIKit 生命周期 + 组装 Controller/View）
├── Main_BattleTeamSelectPanel.View.cs      # partial：纯 View（字段+渲染，不含业务判定）
└── Main_BattleTeamSelectPanel.Designer.cs  # CodeGen 产物（不变）
```

> 说明：prefab/AB 脚本引用按 **.meta GUID**，移动/新增 partial 不改类名，prefab 无需重建、AB 名不变。

### 11.3 MVC 功能细则（文件级职责）

| 层 | 文件 | 职责 | 禁止 |
|----|------|------|------|
| M(Model) | `Main_TeamSlotDraft` | 槽数据（可空/IsComplete） | 无 UI |
| M | `Main_BattleTeamSelectModel` | 4 槽状态、唯一性规则、变更入口、`FormationChanged` | 不读 UI；不含存取 |
| M·Data | `IBattlePlayerDataService`（接口） | 可用角色/武器池 + 出战配置读写（M2 换账号实现=Profile/Grow/Bag） | 无 UI |
| M·Data | `MemoryBattlePlayerDataService`+Locator | 灰盒内存实现/定位 | 不落盘 |
| C | `Main_BattleTeamSelectController` | 编排交互：槽点击→开列表；单击/双击(300ms)→Model 变更→Save→刷新；清空/开始(校验→写 `BattleTeamSession`)/返回；提示；弹窗状态机 | 不直接碰字段（只经 View 接口） |
| V | `Main_BattleTeamSelectPanel.View`(partial) | 持有 serialized 字段；槽文本刷新；列表渲染；Detail/Toast/弹窗显隐；点击以事件上抛（事件签名不含业务对象） | 不做业务判定/数据写 |
| Shell | `Main_BattleTeamSelectPanel`(partial) | `OnInit/OnOpen/OnClose`：创建 Controller、注入 View、挂/解事件 | 不写玩法逻辑 |

**交互状态机归属 Controller**：`Idle → ListOpen(kind,slot) →（单击 Preview / 双击 Apply→Idle）`；双击窗口、占用置灰、[空] 第 1 项等规则全部内聚 Controller/Model。

### 11.4 模型表现层决策（方案①，本卡只留缝不实现）

- 三件套（后续首个需要模型实机展示的局外面立项时落地）：
  1. `VisualDescriptor`（数据）：prefab/默认姿态/挂点约定/材质变体(元素·稀有度)/展示相机点；
  2. 展示层（轻服务+容器）：`BattleSlot`（走现有 `BattleRoleRuntimeData`/武器适配器，几乎零改）与 `StageView`（局外展示台：相机/灯光/Idle/入场动画，供 详情/编成/Gacha 复用）；
  3. 上下文适配器：战斗由战斗管线驱动；局外页面只声明“展示 X 的姿态/抽卡姿态”，由展示层装配。
- 边界（已确认）：**战斗内展示不并入局外框架，只共享表现描述**。
- 编成页当前为文本占位（A6/D7），将来经 `StageView` 接入同一表现层，不各自写模型逻辑。

## 12. 变更记录

- v0.1 2026-09-05：初稿 + 默认假设；

- v0.2 2026-09-05：整合第二轮决策（单击简介/双击执行、占用置灰末尾、空项第一、M1\_Menu 废弃、A2 分层建议）；

- **v1.0 2026-09-05：定稿**。A2=方案A、A3=返回 0ConfigScene（含来源机制）、A8=Editor 方式且先移除过时 Editor 方法、简介区弹窗右侧 + 双击 300ms。
- **v1.1 2026-09-05：架构修正（待实施）**——编队模块迁出 Battle；按 Shell / Controller / View / Model 拆分。
- **v1.2 2026-09-05：定稿**——`02_PlayerMeta` 局外养成工作台（覆盖持有/养成/编成/展示，M2 补齐 Grow/Profile/Bag）；Formation 子域名沿用；模型表现层采用方案①（轻契约+容器+适配器，灰盒只留缝）；Formation 代码归 `02_PlayerMeta/Formation/`。

