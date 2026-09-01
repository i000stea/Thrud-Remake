# Gacha 模块设计

## 模块定位

`Gacha` 是第二里程碑加入的抽卡模块，负责池子规则、抽取结果、演出展示与结果入库申请。它必须和 `Economy`、`Bag`、`Profile` 分层，不把“消耗”“入库”“持有状态”全写在自己内部。

## 职责边界

负责：

- 角色池
- 武器池
- 保底 / 定轨规则
- 抽卡演出
- 抽卡历史记录

不负责：

- 货币定义
- 物品真实入库
- 持有状态解释

## 首版功能要点

- 常驻角色池与武器池
- 单抽 / 十连
- 保底规则与定轨占位
- 抽卡演出与可跳过逻辑
- 历史记录查询

## 核心分层

- `GachaRuleService`：概率与保底裁决
- `GachaDrawService`：抽取执行
- `GachaPresentation`：演出与展示
- `GachaHistoryRepository`：记录保存

## 依赖关系

- 依赖：`Config`、`Economy`、`Bag`、`Profile`、`TelemetryLog`
- 被依赖：`MainCity`

## 与当前仓库的关系

- 现有 `Assets/02_Scripts/Before/02Gacha` 与 `Assets/01_Scenes/2GachaScene.unity` 可作为参考
- 抽卡场景美术与动画资源可复用，但代码按新架构重建

## 首版验收

- 角色池与武器池可分别运作
- 保底规则可配置
- 结果演出与实际结果一致
- 重复获得可正确转化并交由 `Bag` / `Economy` 处理

## 后续扩展

- 限定池
- UP 角色/武器定轨
- 更复杂的保底继承规则
