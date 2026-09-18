# 02_Gacha · 抽卡域（TeaGame.Gacha）

> 建立：**2026-09-18**（P1A S2/S3）。本 README 是该目录的**唯一说明入口**，读完即可判断"这里的东西能不能用"。

## 目录内容

| 路径 | 是什么 | 参与运行时？ |
|---|---|---|
| `TeaGame.Gacha.asmdef` | 域程序集。引用 `TeaGame.App` / `TeaGame.Data` / `TeaGame.Tools` + 框架层 | — |
| `GachaModule.cs` | **占位框架**：让程序集非空（消除 Unity 的「no scripts」提示），并标记 M2 的落点 | 是（只有一个常量，无行为） |
| `Legacy/` | **M2 参考实现（存档，不迁移）**：`Gacha_CoreOperation`（抽卡概率/保底）、`Gacha_MainControl`（流程编排/UI 驱动）、`Gacha_UIOperation`（结果演出） | **否** |

## `Legacy/` 的性质与边界（重要）

- **来源**：从 git 历史 `5bc3890c` 的 `Assets/02_Scripts/Before/02Gacha/` 原样还原（`.cs` 与 `.cs.meta` **成对还原，GUID 与原文件一致**）。
- **性质**：**只读参考实现**。它是旧框架（旧 LoadingControl 链路 + 旧数据流）下的产物，**未与当前架构对齐**，只作为 M2 全量重写时的"当时怎么做的"参照。
- **"不参与运行时"的准确含义**：这些脚本**会被编译进 `TeaGame.Gacha`**（因此也随热更 DLL 进包，比空程序集大约多 23KB），但**没有任何调用入口、没有被任何活场景/预制体挂载**，运行时不执行。
  若 P1C 阶段希望它们**彻底不进包**，最省事的两个做法：① 直接删除本目录（它只是参考，git 历史里永远取得到）；② 给 `Legacy/` 加一个嵌套 asmdef 并设 `defineConstraints` 为未定义符号（Unity 会跳过编译）。**不要**为它写封装层。
- **纪律**：
  - **不要"封装"它**、不要给它加接口/基类/适配层——对沉默的代码做封装等于长期维护负担；
  - **不要把它接到主流程上**；
  - **不要以它为准**理解当前抽卡设计（当前无抽卡实现）。

## 已知的刻意未修复项

| 位置 | 情况 | 处理 |
|---|---|---|
| `Legacy/Gacha_MainControl.cs` `Button_Back()` | 原调用 `LoadingControl.OnLoad(1)`；`LoadingControl` 已随 `Before/` 整体删除 | **已就地切断**（改为一行 `Debug.LogWarning` + 注释说明现代对应物 `SceneFlow`/`SceneRequest`），**不迁移行为** |
| `Legacy/` 全部 | 依赖 `TeaGame.Data` 的旧数据结构（`GachaPool_Data`/`RoleItem_ListData`/`WeaponItem_ListData` 等），仅保证**编译通过** | 不保证运行正确（引用资产/场景可能已不存在） |

## 副作用（已知且有益）

`Gacha_MainControl` 的 `guid` 与原文件相同（`e294907c1db688542af62f44f4bc6fbf`），而 `Assets/01_Scenes/3GachaScene.unity` 正好引用该 guid
→ **该场景上的 `Missing (Mono Script)` 随之消失**（脚本引用复活）。该场景 `enabled=0`，不在包内，无出包影响。
（另两个脚本 `Gacha_CoreOperation` / `Gacha_UIOperation` 本就无任何资产引用。）

## M2 开工时怎么做

1. 先读 `Docs/04_模块设计/` 的 Gacha 模块文档与 16 文档 §5A.1b 的决策记录；
2. 决定 `Legacy/` 的去留（删除 / 留在原地 / 移入 `Assets/Legacy/`）——**"决定不改"也是合格产出**；
3. 新实现直接写在 `02_Gacha/` 根下（不要写在 `Legacy/` 里），并更新本 README 与 `GachaModule.IsImplemented`。
