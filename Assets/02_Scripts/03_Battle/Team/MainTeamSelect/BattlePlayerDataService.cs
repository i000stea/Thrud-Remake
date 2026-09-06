using System.Collections.Generic;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 局外玩家数据访问口（v1.0 设计 A2 方案 A）。
   /// 本面板只依赖该接口：可用角色/武器（未来=账号持有过滤）+ 出战配置读写（局外·槽位级 角色×武器 绑定）。
   /// 局外属性/强化（M2）不在此接口内，避免灰盒期以未定系统为前提。
   /// 真实存档实现（账号）完成后替换 <see cref="MemoryBattlePlayerDataService"/>，View/Controller/Model 不动。
   /// </summary>
   public interface IBattlePlayerDataService
   {
      /// <summary>可用角色（编目→账号持有过滤的升级点）。</summary>
      IReadOnlyList<RoleItem_Data> Roles { get; }

      /// <summary>可用武器。</summary>
      IReadOnlyList<WeaponItem_Data> Weapons { get; }

      /// <summary>把已保存的出战配置读出到 slots（数量对齐 BattleTeamSession.MaxTeamSize）。</summary>
      void LoadFormation(Main_TeamSlotDraft[] slots);

      /// <summary>保存当前出战配置（灰盒=内存；未来=账号存档）。每次变更即保存。</summary>
      void SaveFormation(Main_TeamSlotDraft[] slots);
   }

   /// <summary>
   /// 灰盒内存实现：编目全量开放 + 出战配置仅存内存（进程内跨场景保留，重载即重置，绝不落盘）。
   /// </summary>
   public sealed class MemoryBattlePlayerDataService : IBattlePlayerDataService
   {
      private readonly RoleItem_Data[] mRoles;
      private readonly WeaponItem_Data[] mWeapons;
      private readonly Main_TeamSlotDraft[] mSaved = new Main_TeamSlotDraft[BattleTeamSession.MaxTeamSize];

      /// <summary>生成时的编目源（用于定位器判断是否需要重建）。</summary>
      public BattleTeamCatalog Catalog { get; }

      public MemoryBattlePlayerDataService(BattleTeamCatalog catalog)
      {
         Catalog = catalog;
         mRoles = catalog != null && catalog.Roles != null ? catalog.Roles : System.Array.Empty<RoleItem_Data>();
         mWeapons = catalog != null && catalog.Weapons != null ? catalog.Weapons : System.Array.Empty<WeaponItem_Data>();
         for (var i = 0; i < mSaved.Length; i++)
         {
            mSaved[i] = new Main_TeamSlotDraft();
         }
      }

      public IReadOnlyList<RoleItem_Data> Roles => mRoles;
      public IReadOnlyList<WeaponItem_Data> Weapons => mWeapons;

      public void LoadFormation(Main_TeamSlotDraft[] slots)
      {
         for (var i = 0; i < slots.Length && i < mSaved.Length; i++)
         {
            slots[i].Role = mSaved[i].Role;
            slots[i].Weapon = mSaved[i].Weapon;
         }
      }

      public void SaveFormation(Main_TeamSlotDraft[] slots)
      {
         for (var i = 0; i < slots.Length && i < mSaved.Length; i++)
         {
            mSaved[i].Role = slots[i].Role;
            mSaved[i].Weapon = slots[i].Weapon;
         }
      }
   }

   /// <summary>
   /// 灰盒服务定位器：持有单一进程级实例（内存保存跨场景有效）。
   /// 编目实例变化时重建（内存配置随之重置，避免脏引用）。
   /// </summary>
   public static class BattlePlayerDataServiceLocator
   {
      private static IBattlePlayerDataService sCurrent;

      /// <summary>确保存在并返回与当前编目匹配的服务实例。</summary>
      public static IBattlePlayerDataService Ensure(BattleTeamCatalog catalog)
      {
         if (sCurrent is MemoryBattlePlayerDataService memory && ReferenceEquals(memory.Catalog, catalog))
         {
            return sCurrent;
         }

         if (catalog == null)
         {
            return sCurrent; // 无编目：不重建（可能为历史实例），由面板层做空编目提示
         }

         sCurrent = new MemoryBattlePlayerDataService(catalog);
         return sCurrent;
      }
   }
}
