using QFramework;

namespace TeaGame
{
   /// <summary>
   /// 战斗主 HUD（<see cref="Battle_MainPanel"/>）的可绑定视图状态（反向绑定的数据源）。
   ///
   /// **定位**：`BattleStateModel`（<see cref="BattleStateModel.Hud"/>）持有的、面向 UI 的**少量数值镜像**。
   /// 之所以要这一层：UI 需要"数据变化时被通知"而不是每帧轮询，QFramework 的正统做法是把值放进
   /// <see cref="BindableProperty{T}"/>，由 View 用 `RegisterWithInitValue(...)` 反向注册。
   ///
   /// **为什么不会变成"两处状态各自演进"**：这些属性**只在共享状态的原有写入口同步写入**
   /// （`BattleRoleSystem.SelectRole` / `BattleSkillSystem.AddEnergy` / `BroadcastSlot`），
   /// 没有第二条赋值路径。仿真真值仍在 `BattleStateModel.SelectedRoleIndex` / `RoleEnergy` /
   /// `BattleSkillSystem` 的冷却表里，本类只做"推给 UI 的那一份快照"。
   ///
   /// **类型纪律**：只能用 `int` / `float` / `string` 等基础类型。
   /// 原因（实测 QFramework 源码）：`BindableProperty&lt;T&gt;.Comparer` 是**按 T 共享的 static 字段**，
   /// 默认比较器是 `(a, b) =&gt; a.Equals(b)`；对未约束泛型 T 这会退化为 `object.Equals`（值类型装箱）。
   /// 而 `ComparerAutoRegister` 只为 int/float/string/Vector2~4/Color 等注册了快速比较器，
   /// 所以这里刻意避开自定义 struct。
   ///
   /// **不要扩成第二个 StateModel**：只加"UI 直接消费"的数值；仿真/玩法用的状态留在 `BattleStateModel`。
   /// </summary>
   public sealed class BattleHudState
   {
      /// <summary>当前出战角色槽位（写入点：<c>BattleRoleSystem.SelectRole</c> / <c>HandleRoleDowned</c>）。</summary>
      public readonly BindableProperty<int> SelectedRoleIndex = new(0);

      /// <summary>当前技能能量（写入点：<c>BattleSkillSystem.AddEnergy</c> / <c>ResetForBattle</c>）。</summary>
      public readonly BindableProperty<float> Energy = new(0f);

      /// <summary>E（元素战技）剩余冷却秒数；&gt;0 表示冷却中（写入点：<c>BattleSkillSystem.BroadcastSlot</c>）。</summary>
      public readonly BindableProperty<float> SkillERemain = new(0f);

      /// <summary>Q（极奏）剩余冷却秒数；&gt;0 表示冷却中（写入点：同上）。</summary>
      public readonly BindableProperty<float> SkillQRemain = new(0f);

      /// <summary>Q 的能量缺口（冷却已到但能量不足时 &gt;0，否则 0）（写入点：同上）。</summary>
      public readonly BindableProperty<float> SkillQDeficit = new(0f);
   }
}
