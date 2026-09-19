# 表现层分层：View / Actor 语义层设计

> 产出日期：**2026-09-18**。定位：对「System 直接驱动 View」这一现状的架构分析与改进方案。
> 触发问题（用户提出）：① `BattleViewSystem` 该不该存在？各自的 View 是否应由各自的 ViewController 处理？② 能否在 View 与 System 之间单独建一层，用于**针对 3D 对象的操控**，让 System 与该层沟通完成 3D 空间交互？
> **状态：2026-09-19 用户确认修订方案，代码已落地；编译 / Play / Editor 资产清理待验证。以下“当前实施记录”优先于原始草案。**
> 关系：总体架构见 `../02_系统与玩法设计/01_技术架构设计.md`（已加「表现层（View / Actor）分层」一节指向本文）；域与程序集边界见 `17_代码现状地图.md`；`03_Battle` 调用链与文件目录见 `18_Battle调用链与阅读目录.md`。

---

## 当前实施记录（2026-09-19，用户确认）

### 范围与取舍

- Actor 是场景执行层，覆盖 **物理移动 / 传送 + 3D 表现**。System 保留规则和状态；移动 Actor 可提供位置、速度等只读反馈，不套“只写表现”的旧限制。
- `BattleInputSystem` 原样保留；敌人、投射物、高频池对象、资源加载和实例创建流程不在本次范围。
- `battle_runtime` 继续唯一装配与驱动。4 个 Actor 是普通 C# 对象，由 Host 持有，装配既有 Transform/Camera/Rigidbody，不增加 prefab 上的组件与序列化要求。
- 本次是用户明确批准的架构专项，优先于 16 文档 P1A“只清理、不新增抽象”的旧范围。无需等待引擎升级，不改程序集：运行时代码均属 `TeaGame.Battle`（热更）。
- 一次迁完旧 `BattleViewSystem` 并删除注册、调用、`InitializeView()`，不保留迁移包装入口；原武器 `BattleRuntimeBridge` 有在用消费者，本次不删除它。
- UI 不进入 Actor；用户追加确认 Aim 的位置也迁往 View，通过 `BindableProperty` 驱动。

### 职责与代码入口

| 入口（相对 `Assets/02_Scripts/03_Battle/`） | 责任 |
|---|---|
| `Player/BattleMovementActor.cs` | 世界方向换算、刚体施力、传送、位置/动画速度反馈；MoveForce/冷却/冲刺判断留在 MovementSystem |
| `Player/BattleRoleActor.cs` | 既有角色/武器实例挂点、显隐、出生/翻滚动画、移动动画、模型朝向；选角/阵亡规则留在 RoleSystem |
| `Player/BattleWeaponActor.cs` | 观察点、武器吸附/回正；开火/换弹/弹道不迁入 |
| `Player/BattleCameraActor.cs` | FOV 插值，当前 FOV 为私有缓存；输入视角旋转仍归 CameraSystem |
| `UI/BattleHudState.cs` | 血量、弹药和准星位置/速度的绑定状态；保留已有技能状态 |
| `UI/Panels/Battle_MainPanel.Presentation.cs` | 血条、弹药、队伍列表的生命周期，以及准星坐标转换和 Lerp |
| `Editor/BattleActorMigrationSetup.cs` | 验证主面板字段，再幂等清理 rig 的旧 MainCanvas；不手写 YAML |

### Aim 的数据流与原有吸附规则

`AimSystem → Hud.AimPosition (BindableProperty<Vector2>) → 主面板订阅目标位置 → Host 的 LateUpdate 调用 View → View.Lerp + anchoredPosition`。

- 坐标定义：屏幕中心为原点，屏幕像素乘 `1920 / Screen.width`；沿用旧 rig CanvasScaler 的 1920 宽、MatchWidth=0 口径，使 LockAssistDistance 的单位不随 HUD 层级改变。
- View 将自己的插值位置映射回屏幕，再按实际根 Canvas 的渲染相机转换为准星父 RectTransform 坐标，兼容 Overlay / Camera Canvas 与分辨率变化。
- `Vector2` 已由 QFramework `ComparerAutoRegister` 注册快速比较器，不设置全局 `WithComparer`，不引入自定义绑定 struct。此点修订旧草案“仅 int/float/string”的限制。
- 旧准星距离还控制武器的 1/4/12 倍吸附速度。为避免 UI 未开时改变武器手感，AimSystem 保留**独立逻辑游标**计算这套倍率；它不读取/写入任何 UI，也不负责实际准星插值。View 只读绑定目标与速度，不反写游戏状态。
- 场景装配重置手动锁定、切换冷却、逻辑游标和绑定目标；面板开启注册即读，关闭/销毁幂等反注册。暂停时 Host 不推进 Actor/准星。

### 生命周期与顺序

1. Awake 装配移动 Actor → MovementSystem.Teleport 应用出生点（默认保留速度，延续原行为）。
2. Start 初始化角色/血量 → 装配角色、武器、相机 Actor → 角色挂点/显隐/出生动画 → Aim 重置 → 原技能/关卡启动 → 等 UIRoot 打开 HUD。
3. Update：原输入与暂停守卫 → MovementSystem → CameraSystem → 移动动画 / FOV / 模型 / 武器 Actor → 弹药状态镜像 → WeaponSystem → SkillSystem → Bridge。
4. FixedUpdate：移动施力 → 锁定搜索 → Bridge。翻滚仍在原 Update 阶段使用默认 AddForce，未改为 Impulse。
5. LateUpdate：Aim 吸附倍率与速度发布 → 主面板准星插值 → Bridge。
6. 离场：Host 解除持久 System 的 Actor 引用并清 Controller，关闭面板；面板释放绑定和队伍 HUD。旧 rig 自有清池/Bridge 清理保留。

### 迁移状态与一次性验收

- [x] 四类 Actor、旧 ViewSystem 全部职责分派、主面板血条/弹药/队伍 HUD、Aim 绑定与 UI 插值迁移。
- [x] 删除旧 System 注册、InitializeView 和 rig 的 HUD Designer 字段，InputSystem 与 HEAD 比较一致。
- [x] 静态扫描：旧 System/初始化引用为 0，MovementSystem 不再依赖 controller 或直接 AddForce；Aim 无 UI 组件读写。
- [x] `git diff --check` 通过；InputSystem 与 HEAD 原文一致；武器表现在开火之前、准星 View 在 Aim LateUpdate 之后的顺序已核对。1920×1080 / 1280×720 / 2560×1080 / 2400×1080 共 16 个映射点数学等价检查通过（不代表 Unity 画面验收）。
- [ ] Unity 导入 / 本轮 C# 编译（含新 `.meta` 由 Editor 生成）及 Play 行为 **未完成**：本轮日志的最新构建段仍报告旧 `Assets/02_Scripts/UIPanel/**` 残留引用和 `Assets/Data/Editor/Thrud_ReadExcel.cs` 的 `Editor` 命名冲突；前者是仓库当前已删除目录的旧导入缓存，后者是既有 Editor 脚本问题。新菜单最初使用 `TeaGame.Editor` 造成同名空间放大了后者，已改为 `TeaGame.BattleEditor`，需下一次 Refresh 后重新取证。不能据历史 DLL 或本轮失败段声明 Battle 编译成功。
- [ ] 退出 Play 后运行 **`Thrud/Battle/Actor/1. 校验主 HUD 并移除 rig 旧 HUD`**。资产：`Assets/04_Prefabs/03Battle/Battle_PlayerControl.prefab`、`Assets/04_Prefabs/Panel/Battle/Battle_MainPanel.prefab`。检查场景 override，再重新生成 rig CodeGen；发布前重建 AB。
- [ ] `Assets/01_Scenes/BattleGraybox.unity`：出生位置；WASD/升降/翻滚/冲刺；FOV/模型俯仰；1–4 切人；Tab 锁定/目标死亡/取消；未锁定射击与武器回正；血条/弹药/技能/队伍 UI；暂停恢复。
- [ ] 主链路：登录 → 主菜单 → 选关/编队 → 战斗 → 阵亡自动切人/全灭 → 重开 → 返回菜单 → 再进战斗，检查重复订阅、旧 HUD、MissingReference。至少测试 16:9 与宽屏准星位置。
- [ ] HUD 打开失败时移动/武器仍可运行，重新打开 HUD 能恢复显示；Docs/12 的历史伤害疑点仍待独立复现，本次不改伤害规则。

> 代码会关闭尚未清理的 rig MainCanvas，避免重复上屏；这是旧资产隔离，不是旧 ViewSystem 兼容执行路径。资产工具尚未运行，不宣称 prefab 已完成清理。

---

## 原始草案存档（2026-09-18，以下待确认项已被上方用户决策替代）

## 0. 结论摘要

1. **方向正确**：在 System 与 prefab 具体组件之间加一层**语义化 actor 句柄**，是成熟做法（Unity DOTS 混合模式、Unreal Actor/AnimInstance 分离、MVP Presenter 同源）。
2. **但成败只取决于一个判据**：这层边界上过的是**语义动词**还是**字段透传**。属性透传只是把耦合换个名字。
3. **它只解决三类问题中的一类**。必须把三条通道分开，不能合并（§3）。
4. **它只适用于"少而复杂"的 actor**：本项目实测 = 玩家 rig（1）+ 敌人（4 类 / 22 prefab）；**不适用于**池化投射物（10 prefab、每秒几十个）——那里做语义层是净亏损。
5. **本项目的真根因不是 `BattleViewSystem`**，而是 `Battle_PlayerControl` 当了「场景引用总仓库」——它才是全部 `controller.` 引用的共同根因（§2）。
6. **建议**：先补规范（本节已回填 01 文档）→ 只做玩家 rig 一个对象验证 → 敌人用"同对象抽 View 组件"的轻量版 → 子弹不做（§6）。

---

## 1. 现状与实测证据

### 1.1 `BattleViewSystem` 实际在做什么

`Core/BattleSystems.cs` 的 `BattleViewSystem`：**L390–L791，400 行**，其中 **34 处语句直接操作具体 View**。

它同时扮演三种不同职责：

| 职责 | 代表方法 | 该归谁 |
|---|---|---|
| **算**（表现的目标值） | 目标 FOV、目标旋转、血条比例 | Model / System（应保留） |
| **找 + 写**（操作具体组件） | `controller.Health_Main.sizeDelta`、`controller.Health_Value.text`、`animator.SetFloat/SetBool/SetTrigger/Play`、`controller.ViewRotateAxis.rotation`、`controller.WepLookatTarget.position`、`camera.fieldOfView` | **View 自己** |
| **建 + 销毁** | `BuildTeamHud` / `DisposeTeamHud`（`BattleTeamHudView.Create`） | 谁创建谁负责 |

它还订阅 4 个事件（`BattleRoleSwitched` / `BattleRollTriggered` / `BattleRoleHealthChanged` / `BattleRoleDowned`），并拥有 `InitializeView()`——由 `battle_runtime` 在装配阶段调用。

> **已落地的第一步（2026-09-18）**：技能 HUD 那部分已从 `Battle_PlayerControl` 迁到 `Battle_MainPanel`，改由 `BattleHudState` 的 `BindableProperty` 反向绑定驱动。见 §3.1 与 §8.1。

### 1.2 「直触 View」必须分类，不能一概而论

**判据修正**：逻辑层驱动**世界实体**的 Transform（敌人位移、刚体施力、技能位移）是 **simulation，不是 View**。对 3D 游戏把世界 Transform 当 View 剥离，成本极高、收益极低。真正该判违规的只有 **UI / HUD / 动画参数 / 相机表现**。

按此口径逐条分类（实测）：

| System | 行数 | 命中 | 实际性质 |
|---|---|---|---|
| `BattleViewSystem` | 400 | **34** | **全部是真违规**（HUD 文本/血条/Canvas/Animator/表现轴/相机 FOV） |
| `BattleAimSystem` | 261 | 14 | 约 **6 处真违规**（`Cross_LockEnemy` 准星 RectTransform + `MainCanvas` 投影换算）；其余为 `Control_Camera` 读取与 `PlayerConfig` |
| `BattleMovementSystem` | 100 | 10 | **0 处 UI 违规**：`SelfRigidbody`、`ControlXAxis.forward`、`PlayerConfig` |
| `BattleCameraSystem` | 52 | 4 | **0 处 UI 违规**：两条轴线 Transform + `PlayerConfig` |
| `BattleRoleSystem` / `BattleWeaponSystem` / `BattleInputSystem` | 106 / 84 / 171 | 4 / 2 / 1 | 均为 `PlayerConfig` 与开关读取 |

**结论**：`BattleViewSystem` 是唯一真正的"UI 驱动式 System"，`BattleAimSystem` 的准星是第二处。问题真实但**孤立**——这一点比"架构洁癖"重要得多，也决定了改造的规模可控。

### 1.3 项目规范缺位（这才是它能长到 400 行的原因）

实测 `../02_系统与玩法设计/01_技术架构设计.md` 原文**没有 View 层规范**：其"四层"是 游戏模块层 / 框架层 / 基础设施层 / 数据层，ViewController 只出现在 UIKit 那一行的"面板生命周期"说明里。

→ **"该不该有 ViewSystem"在项目里从未被规定过。** 所以本文的第一项产出是**补规范**（已在 01 文档新增一节，§3 的表即为规范内容）。

---

## 2. 更深一层的根因：`Battle_PlayerControl` 是「场景引用总仓库」

`Battle_PlayerControl`（rig，`ViewController`）身上挂着：相机、刚体、X/Y 轴线、展示模型轴、武器挂点、Canvas、血条、HUD 文本、Animator、子弹 prefab ×4、PlayerConfig。

于是**每个 System 都要穿过 `controller.` 去取自己需要的那一点点**：

| System | 它真正需要的 |
|---|---|
| `BattleMovementSystem` | `SelfRigidbody` + `PlayerConfig` |
| `BattleCameraSystem` | `ControlXAxis` / `ControlYAxis` + `PlayerConfig` |
| `BattleAimSystem` | `Control_Camera` / `MainCanvas` / `Cross_LockEnemy` + `PlayerConfig` |

这是 Service Locator 式的「View 门面」，或叫**过度集中的 View 载体**。它是 `controller.` 全部命中的共同根因。

**修法不是删 ViewSystem，而是给每个 System 注入它真正需要的最小引用**（构造/`Initialize` 时注入 Rigidbody、Config、轴线），System 就不再需要认识 `Battle_PlayerControl` 这个类型。这项改动**收益比搬 HUD 更根本，但风险更大**（动了每个 System 的装配签名），排在 §6 路线之后。

---

## 3. 核心：三类通道必须分开

**把三个问题塞进同一层，就是它变成第二个 God object 的开始**——这正是 `Battle_PlayerControl` 的教训。

| 问题 | 本项目实例 | 正确解法 | 为什么不用 Actor 层 |
|---|---|---|---|
| **离散状态 → UI 显示** | 能量、冷却、血条比例、角色名、弹药数 | **`BindableProperty` 反向绑定** | UI 关心的是"值变了"，不需要 3D 对象句柄；UI 生命周期由面板管，与场景对象无关 |
| **仿真对场景的依赖** | `SelfRigidbody`、`PlayerConfig`、`ControlXAxis` | **装配期最小引用注入** | 这是"读配置/读物理"，不是"操控表现"；套语义层只增加间接 |
| **3D 对象的表现操控** | 朝向、动画参数、身体着色、飘字、挂点、爆炸环 | **Actor 语义层（本文主题）** | —— |

### 3.1 通道一：`BindableProperty`（离散状态）

- 数据源：`BattleHudState`（`03_Battle/UI/BattleHudState.cs`），挂在 `BattleStateModel.Hud` 上。
- View 侧：`property.RegisterWithInitValue(_ => Refresh()).UnRegisterWhenGameObjectDestroyed(gameObject)`——**注册即渲染一次**，无需首帧手动刷新；面板销毁时自动反注册。
- **实测框架坑（必须记住）**：
  1. `BindableProperty<T>.Comparer` 是**按 T 共享的 static 字段**（不是每实例），`WithComparer` 会改全局；
  2. 默认比较器 `(a, b) => a.Equals(b)` 对未约束泛型 T 会退化为 `object.Equals` → **值类型装箱**；`ComparerAutoRegister` 只为 int/float/string/Vector2~4/Color 等注册了快速比较器；
  3. → **契约：只用 `int` / `float` / `string`，不要自定义 struct。**
- **纪律**：`BattleHudState` 只放"UI 直接消费"的值，**不要扩成第二个 StateModel**。

### 3.2 通道二：最小引用注入（仿真依赖）

把"装配期解析出的场景引用"收进一个只读结构（如 `BattleRigRefs`），由 `battle_runtime` 注入给需要的 System。解决"总仓库"问题的一半，**不解决**"System 知道 UI 细节"（那要通道三）。

### 3.3 通道三：Actor 语义层（3D 表现）

**判据（本文最重要的一条）**：

```csharp
// ❌ 属性透传：只是把耦合换个名字，System 照样知道背后是 Image / TMP / Animator
interface IBadPuppet { RectTransform HealthBar { get; } Animator Anim { get; } }

// ✅ 语义动词：System 不知道背后是 Image、TMP、SkinnedMesh 还是 Canvas
interface IBattleActorView
{
   Vector3   Position { get; }
   bool      IsAlive  { get; }
   void Teleport(Vector3 pos, Quaternion rot);    // 装配 / 复活
   void SetMoveSpeed(float forward, float side);  // 动画参数（不是 SetFloat）
   void SetSprint(bool on);
   void SetHealthRatio(float ratio);              // 血条（不是 sizeDelta）
   void PlayRoll(Vector2 dir);                    // 一次性演出（不是 SetTrigger）
   void AimAt(Transform target, float speed);
}
```

> **用"语义动词"命名的接口才是这一层；用"属性透传"的只是多一次间接调用。** 这条判据可直接用于 review。

**额外收益（最被低估的一点）**：这层**不破坏同帧顺序确定性**——`battle_runtime` 仍可按固定顺序对 puppet 发语义调用，**保住了集中排序，但不再集中知识**。这正好回答了「拆散后顺序谁来管」的反对意见。

---

## 4. 适用范围：只做"少而复杂"的 actor

实测本项目战斗中的 3D 对象分布：

| 对象 | 数量 | 复杂度 | 现状 | 是否上语义层 |
|---|---|---|---|---|
| **玩家 rig** | 1 | 高 | `BattleViewSystem` 34 处直写其字段 | ✅ **值得** |
| **敌人** | 4 类行为 / 22 个 prefab | 中 | `BattleEnemyEntity`（499 行）内 **28 处**表现语句（`mBodyRenderer.material.color` ×2、飘字、爆炸环、头顶 UI 创建、子弹 LookAt/Rotate） | ✅ **值得，但用轻量版**（§6 步骤 2） |
| **子弹 / 特效** | 10 个 prefab、池化、每秒数十个 | 低 | 直接 Spawn/Despawn + 直写 Transform | ❌ **不做** |

**硬界线：语义层适用于"少而复杂"的 actor，不适用于"多而简单"的投射物。** 每帧对几百个子弹多做一次虚调用与中间对象是净亏损；那类对象应保持 DOD 式直接操作。这条界线能防止该层被无限扩权。

---

## 5. 失败模式与设防（这类层最常见的四种腐烂）

| # | 失败模式 | 设防 |
|---|---|---|
| 1 | **生命周期没主**：谁创建/销毁 puppet？ | puppet 的创建归 `battle_runtime`（场景装配）；**池化对象的 reset 必须挂进 `BattleObjectPool` 的回池流程**（否则悬垂引用） |
| 2 | **变成双向依赖**：puppet 开始读游戏状态 | **硬纪律：sim → puppet 单向只写**。需要读的一律由 sim 在调用时作为**参数**传入 |
| 3 | **接口按类型爆炸**：敌人 4 类 × 武器 16 子类各来一个接口 | **接口要少而通用**（一个 `IBattleActorView` 足够），特化能力用重载/参数表达 |
| 4 | **为"以后多端"提前抽象** | 违反 P1A 红旗第 3 条。**没有具体失败用例就不做** |

---

## 6. 建议路线（分步、每步可停、每步可验收）

> 前置纪律（`16_热更新方案选型与落地计划.md` §5A.2）：**按 View 逐个迁、逐个提交**，禁止一次性重写 `BattleViewSystem`。

| 步 | 内容 | 验收 | 备注 |
|---|---|---|---|
| **0** | **补规范**：01 架构文档新增「表现层（View / Actor）分层」一节（§3 的三通道表 + §3.3 判据） | 规范可 grep 执行 | **已完成**（2026-09-18 回填 01 文档） |
| **1** | **主 HUD 一次做完**：技能行（已完成）→ 血条 `RefreshPlayerHealth`、准星 `Cross_LockEnemy`、弹药文本，全部迁入 `Battle_MainPanel`，走 `BindableProperty` | rig 上不再有任何 HUD 对象；`BattleViewSystem` 不再出现 `Health_Main`/`Health_Value`/`MainCanvas` | 与用户上一项需求合流 |
| **2** | **队伍血条**：`BattleTeamHudView` 已是独立 View → 把 `Build/Refresh/DisposeTeamHud` 从 ViewSystem 挪到它自己 | ViewSystem 不再 `new`/持有 View | 谁创建谁负责 |
| **3** | **玩家 rig 的 Actor 语义层**：新增 `IBattleActorView` + `PlayerRigActorView`（挂 rig，实现语义方法）；`battle_runtime` 装配时创建并注入 Model；ViewSystem 的 4 个每帧方法改为语义调用 | **把 HUD 从 rig 搬到面板时，System 一行不用改**（这是本步的判定标准） | 只做 1 个对象验证 |
| **4** | **敌人轻量版**：不建接口，在 `BattleEnemyEntity` 同 GameObject 上抽 `EnemyActorView` 组件（语义方法 `SetBodyColor`/`ShowDamageText`/`ShowBlastRing`/`CreateHealthUi`），实体把 28 处表现语句委托给它 | `BattleEnemyEntity` 里表现语句清零 | 同对象组件拆分比跨层接口便宜，收益已得 80% |
| **5** | 迁完 `BattleViewSystem` 只剩"算" → 改名（如 `BattlePresentationModel`）或并入 `BattleHudState`，**`InitializeView()` 消失** | `BattleViewSystem` 名字不再出现 | |
| **6**（可选、收益更大、风险更大） | **最小引用注入**：给每个 System 注入它真正需要的引用，替掉"总仓库" | System 不再出现 `Battle_PlayerControl` 类型 | 动了所有 System 的装配签名 |

### 每帧表现该回哪里（步骤 3 的细节）

`UpdateShowView`（模型跟随视角）、`UpdateWeaponAim`（武器吸附）、`UpdateMovementAnimator`、`UpdateCameraFov` 属于**每帧插值/跟随**：

- **应回到 View 自己的 `Update`/`LateUpdate`**，而不是做成 `BindableProperty`。
- 理由：① 生命周期天然跟随 GameObject（禁用/销毁自动停）；② **插值的中间状态（如"当前 FOV 值"）是 View 私有缓存，塞进 Model 是错的**；③ 每帧触发绑定回调 + 每帧 `float` 比较是纯浪费。
- **顺序确定性**由 `LateUpdate` + Script Execution Order 显式接住（若走步骤 3 的 puppet，则由 `battle_runtime` 按固定顺序 `Tick`）。

**明确不做**：一次性删掉 `BattleViewSystem`；把每帧插值也做成 `BindableProperty`；为"以后可能多端"提前做 View 抽象层。

---

## 7. 待用户确认项

- [ ] **是否采纳本方案**（尤其是 §3 的三通道分流与 §6 的路线顺序）
- [ ] **步骤 3 的形式**：先做 puppet 接口，还是先做通道二（最小引用注入）？后者更轻、解决一半问题
- [ ] **敌人（步骤 4）现在的表现语句是否值得抽**——若近期要接正式美术（T3.2 / M3），抽 `EnemyActorView` 的收益会更大，可提前
- [ ] **`InitializeView()` 的替代时机**：等步骤 3 落地后移除，还是先留一个兼容层
- [ ] **规范是否进 `AGENTS.md`**（作为未来会话的硬约束）——建议**等方案确认后再加**，避免把未确认的规则写成纪律

---

## 8. 与其它文档的关系

| 文档 | 关系 |
|---|---|
| `../02_系统与玩法设计/01_技术架构设计.md` | 已新增「表现层（View / Actor）分层」一节，§3 的三通道表即规范正文；细节与路线以本文为准 |
| `17_代码现状地图.md` | 域与程序集边界；本文不改变任何程序集划分（Actor 层实现类落在 `TeaGame.Battle`，属热更层，无新增约束） |
| `18_Battle调用链与阅读目录.md` | `03_Battle` 的文件清单与调用链；本文步骤 1–5 落地后需同步其 §5 / §8.8 / §9.4 / §9.9 |
| `16_热更新方案选型与落地计划.md` §5A.2 | P1A 的"优化地狱"红旗清单——本文步骤必须按"一个 View 一次提交"执行 |
| `../05_开发日志/09_开发日志_会话交接.md` | 2026-09-18 会话记录（HUD 迁移已落地部分） |

---

*回填规则：方案确认后改状态头；每完成 §6 的一个步骤，更新本表与该步的验收结果，并同步 18 文档对应小节。*
