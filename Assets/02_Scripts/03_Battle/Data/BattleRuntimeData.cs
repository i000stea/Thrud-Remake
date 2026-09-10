using System;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 单个角色在战斗中的运行时数据。
   /// 该类用于承载角色实例、动画、武器视图、武器模式以及生命值等动态信息。
   /// </summary>
   [Serializable]
   public sealed class BattleRoleRuntimeData
   {
      /// <summary>
      /// 构造运行时数据。
      /// 构造过程会实例化角色和武器，并完成武器模式装配。
      /// </summary>
      public BattleRoleRuntimeData(RoleItem_Data roleData, WeaponItem_Data weaponData)
      {
         RoleData = roleData;
         WeaponData = weaponData;
         RoleName = roleData.itemName;
         WeaponName = weaponData.itemName;
         MaxHealth = Mathf.Max(1f, roleData.health);
         CurrentHealth = MaxHealth;

         RoleInstance = UnityEngine.Object.Instantiate(roleData.rolePreafab).transform;
         if (RoleInstance && RoleInstance.childCount > 0)
         {
            // `TryGetComponent` 的 `out` 参数要求是可直接赋值的变量。
            // 这里先使用局部变量接收，再赋值给只读自动属性，
            // 可以避免 IDE 将属性赋值目标误判为“不可作为 out 参数”的问题。
            RoleInstance.GetChild(0).TryGetComponent(out Animator roleAnimator);
            RoleAnimator = roleAnimator;
         }

         WeaponInstance = UnityEngine.Object.Instantiate(weaponData.wepPrefab).transform;
         WeaponView = BattleWeaponViewFactory.Create(WeaponInstance);
         WeaponMode = BattleWeaponModeFactory.Create(weaponData.weaponSubType);
         WeaponMode?.Setup(WeaponView, weaponData);
      }

      /// <summary>
      /// 通过旧版运行时对象构造新版数据结构。
      /// 该构造函数只用于兼容迁移过渡，不会再重复实例化角色和武器。
      /// </summary>
      public BattleRoleRuntimeData(
         string roleName,
         string weaponName,
         Transform roleInstance,
         Animator roleAnimator,
         Transform weaponInstance,
         IBattleWeaponView weaponView,
         BattleWeaponModeBase weaponMode)
      {
         RoleName = roleName;
         WeaponName = weaponName;
         RoleInstance = roleInstance;
         RoleAnimator = roleAnimator;
         WeaponInstance = weaponInstance;
         WeaponView = weaponView;
         WeaponMode = weaponMode;
         MaxHealth = 1f;
         CurrentHealth = 1f;
      }

      /// <summary>
      /// 角色原始配置。
      /// </summary>
      public RoleItem_Data RoleData { get; }

      /// <summary>
      /// 武器原始配置。
      /// </summary>
      public WeaponItem_Data WeaponData { get; }

      /// <summary>
      /// 角色名称，便于调试与日志输出。
      /// </summary>
      public string RoleName { get; }

      /// <summary>
      /// 武器名称，便于调试与日志输出。
      /// </summary>
      public string WeaponName { get; }

      /// <summary>
      /// 角色实例根节点。
      /// </summary>
      public Transform RoleInstance { get; }

      /// <summary>
      /// 角色动画组件。
      /// </summary>
      public Animator RoleAnimator { get; }

      /// <summary>
      /// 武器实例根节点。
      /// </summary>
      public Transform WeaponInstance { get; }

      /// <summary>
      /// 武器视图适配器。
      /// 该对象负责屏蔽新旧武器组件之间的差异。
      /// </summary>
      public IBattleWeaponView WeaponView { get; }

      /// <summary>
      /// 武器模式运行时对象。
      /// 不同子类决定了不同的开火行为。
      /// </summary>
      public BattleWeaponModeBase WeaponMode { get; }

      /// <summary>
      /// 角色最大生命值。
      /// </summary>
      public float MaxHealth { get; }

      /// <summary>
      /// 角色当前生命值。
      /// T3.1B 起为成员级血量：受击只扣当前出战角色的该值。
      /// </summary>
      public float CurrentHealth { get; set; }

      /// <summary>
      /// 成员是否已阵亡（T3.1B）。
      /// 血量归零置 true；阵亡成员不可切换/施放，模型隐藏，本局结束前不恢复。
      /// </summary>
      public bool IsDowned { get; set; }

      /// <summary>
      /// 复位成员血量为满值并清除阵亡态（战斗初始化/重开时调用）。
      /// </summary>
      public void ResetHealth()
      {
         CurrentHealth = Mathf.Max(1f, MaxHealth);
         IsDowned = false;
      }
   }
}
