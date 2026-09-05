using System;
using System.Collections.Generic;
using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗角色数据仓库。
   /// 该组件取代旧版的 `StoreBattleRoleData` 单例，
   /// 负责根据角色与武器的 ScriptableObject 数据构建运行时实体。
   /// </summary>
   public class BattleRoleRepository : MonoBehaviour
   {
      [Header("角色与武器配置")]
      [SerializeField] private RoleItem_Data[] loadRoles;
      [SerializeField] private WeaponItem_Data[] loadWeapons;
      [SerializeField] private bool dontDestroyOnLoad = true;

      /// <summary>
      /// 已构建的运行时角色数据缓存。
      /// 缓存存在的目的是避免重复实例化角色与武器预制体。
      /// </summary>
      private BattleRoleRuntimeData[] mCachedRuntimeDatas;

      /// <summary>
      /// 可供外部读取的运行时缓存。
      /// 在尚未构建时，该属性会返回空数组而不是 null。
      /// </summary>
      public BattleRoleRuntimeData[] RuntimeDatas => mCachedRuntimeDatas ?? Array.Empty<BattleRoleRuntimeData>();

      /// <summary>
      /// 初始化数据仓库。
      /// 若开启常驻，则在场景切换时保留该对象。
      /// </summary>
      private void Awake()
      {
         if (dontDestroyOnLoad)
         {
            DontDestroyOnLoad(gameObject);
         }
      }

      /// <summary>
      /// 构建战斗运行时数据。
      /// 第一次调用时会实例化角色与武器对象，后续直接返回缓存结果。
      /// </summary>
      public BattleRoleRuntimeData[] BuildRuntimeDatas()
      {
         if (mCachedRuntimeDatas != null && mCachedRuntimeDatas.Length > 0)
         {
            return mCachedRuntimeDatas;
         }

         var runtimeDatas = new List<BattleRoleRuntimeData>();
         var pairCount = Mathf.Min(loadRoles?.Length ?? 0, loadWeapons?.Length ?? 0);

         for (var i = 0; i < pairCount; i++)
         {
            var roleData = loadRoles[i];
            var weaponData = loadWeapons[i];

            if (roleData == null || weaponData == null)
            {
               Debug.LogWarning($"第 {i} 组战斗数据不完整，已跳过构建。");
               continue;
            }

            runtimeDatas.Add(new BattleRoleRuntimeData(roleData, weaponData));
         }

         mCachedRuntimeDatas = runtimeDatas.ToArray();
         return mCachedRuntimeDatas;
      }

      /// <summary>
      /// 依据"出战编队会话"构建运行时数据（T3.1A）。
      /// 每名出战成员 = 一个 <see cref="BattleTeamMemberDraft"/>（角色 SO + 武器 SO），
      /// 实体化逻辑统一走 <see cref="BattleTeamRuntimeBuilder"/>；
      /// 本方法不清空默认编队缓存（选队路径与默认路径互不影响）。
      /// </summary>
      public BattleRoleRuntimeData[] BuildFromSession(IReadOnlyList<BattleTeamMemberDraft> members)
      {
         return BattleTeamRuntimeBuilder.Build(members);
      }
   }

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
      /// 目前只做运行时缓存，为后续正式接入玩家血量系统做准备。
      /// </summary>
      public float CurrentHealth { get; set; }
   }
}
