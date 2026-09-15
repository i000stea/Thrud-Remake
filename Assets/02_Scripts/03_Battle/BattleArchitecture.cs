using QFramework;

namespace TeaGame
{
   /// <summary>
   /// 战斗架构入口。
   /// 战斗开始后的数据是死的，只有结束后会把战利品带出，独立性极高，适合作为单独的架构
   /// 所有战斗相关的 Model、System 都在这里统一注册，
   /// `PlayerControl` 只需要依赖该架构即可完成整个角色控制流程的驱动。
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
         this.RegisterModel(new BattleStateModel());

         this.RegisterSystem(new BattleRoleSystem());
         this.RegisterSystem(new BattleHealthSystem());
         this.RegisterSystem(new BattleInputSystem());
         this.RegisterSystem(new BattleMovementSystem());
         this.RegisterSystem(new BattleViewSystem());
         this.RegisterSystem(new BattleCameraSystem());
         this.RegisterSystem(new BattleAimSystem());
         this.RegisterSystem(new BattleWeaponSystem());
         this.RegisterSystem(new BattleSkillSystem());
         this.RegisterSystem(new BattleLevelFlow());
         this.RegisterSystem(new BattleUiSystem());
      }
   }
}
