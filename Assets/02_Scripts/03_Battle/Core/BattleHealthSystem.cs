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
      /// 治疗玩家（T2.4 技能效果调用）。
      /// 非受击事件驱动，供 Heal 类公共效果 / 角色技能直接回血，
      /// 当前生命值不越过最大生命值。
      /// </summary>
      /// <param name="amount">治疗量（内部截断为 ≥0）。</param>
      public void HealPlayer(float amount)
      {
         State.PlayerCurrentHealth = Mathf.Min(
            State.PlayerMaxHealth,
            State.PlayerCurrentHealth + Mathf.Max(0f, amount));
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

            // 让控制器的序列化字段同步为接管状态。
            // PlayerControl.Update 每帧会用自身字段覆盖 State.ControlTakenOver，
            // 不同步的话死亡接管会被立即写回 false，导致角色死亡后仍可操作。
            if (State.Controller)
            {
               State.Controller.ControlTakeOff = true;
            }

            Architecture.SendEvent(new BattlePlayerDiedEvent());
         }
      }
   }
}
