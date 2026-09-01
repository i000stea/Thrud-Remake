using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 玩家健康系统。
   /// 负责玩家血量初始化、受击扣血与死亡状态接管。
   /// 订阅 <see cref="BattlePlayerDamagedEvent"/> 完成扣血，
   /// 血量归零时派发 <see cref="BattlePlayerDiedEvent"/> 并接管控制权。
   /// </summary>
   public sealed class BattleHealthSystem : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      /// <summary>
      /// 受击事件反注册句柄。
      /// </summary>
      private IUnRegister mDamagedUnregister;

      protected override void OnInit()
      {
         mDamagedUnregister = Architecture.RegisterEvent<BattlePlayerDamagedEvent>(OnPlayerDamaged);
      }

      protected override void OnDeinit()
      {
         mDamagedUnregister?.UnRegister();
         mDamagedUnregister = null;
      }

      /// <summary>
      /// 初始化玩家血量。
      /// 最大生命值取自玩家配置（数据优先），当前生命值重置为满值。
      /// </summary>
      public void Initialize()
      {
         var controller = State.Controller;
         var maxHealth = controller ? controller.PlayerConfig.PlayerMaxHealth : 100f;
         State.PlayerMaxHealth = Mathf.Max(1f, maxHealth);
         State.PlayerCurrentHealth = State.PlayerMaxHealth;
      }

      /// <summary>
      /// 处理玩家受击。
      /// 处于控制接管状态时忽略伤害，避免死亡演出期间重复扣血。
      /// </summary>
      private void OnPlayerDamaged(BattlePlayerDamagedEvent e)
      {
         if (State.ControlTakenOver) return;

         State.PlayerCurrentHealth -= e.Damage;
         State.PlayerCurrentHealth = Mathf.Max(0f, State.PlayerCurrentHealth);

         if (State.PlayerCurrentHealth <= 0f)
         {
            State.ControlTakenOver = true;
            Architecture.SendEvent(new BattlePlayerDiedEvent());
         }
      }
   }
}
