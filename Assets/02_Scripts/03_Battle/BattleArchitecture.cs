using QFramework;

namespace TeaGame
{
   /// <summary>
   /// 战斗架构入口。
   /// 战斗开始后的数据是死的，只有结束后会把战利品带出，独立性极高，适合作为单独的架构
   /// 所有战斗相关的 Model、System 都在这里统一注册，
   /// `PlayerControl` 只需要依赖该架构即可完成整个角色控制流程的驱动。
   /// 
   /// *战斗架构未通读，本身独立性较高，我认为现阶段能用就行*
   /// </summary>
   public sealed class BattleArchitecture : Architecture<BattleArchitecture>
   {
      /// <summary>
      /// 初始化战斗架构。
      /// 注册顺序遵循“先 Model，后 System”的原则，
      /// 以确保系统初始化时可以读取到完整的共享状态。
      /// </summary>
      protected override void Init()
      {
         // 状态
         this.RegisterModel(new BattleStateModel());

         // 角色
         this.RegisterSystem(new BattleRoleSystem());
         // 生命
         this.RegisterSystem(new BattleHealthSystem());
         // 输入
         this.RegisterSystem(new BattleInputSystem());
         // 移动
         this.RegisterSystem(new BattleMovementSystem());
         // 相机
         this.RegisterSystem(new BattleCameraSystem());
         // 准星
         this.RegisterSystem(new BattleAimSystem());
         // 武器
         this.RegisterSystem(new BattleWeaponSystem());
         // 技能
         this.RegisterSystem(new BattleSkillSystem());
         // 关卡
         this.RegisterSystem(new BattleLevelFlow());
         // ui
         this.RegisterSystem(new BattleUiSystem());
      }
   }
}
