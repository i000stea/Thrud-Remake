using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 出战编目（T3.1A：选队场景的数据源）。
   /// 直接引用现有 <see cref="RoleItem_Data"/> / <see cref="WeaponItem_Data"/> 资产（不改旧类字段，Gacha/旧流程零影响）；
   /// 灰盒=全量开放。升级点：按"账号持有 + 关卡允许"过滤（见 07 T3.1A 升级预估）。
   /// 创建：Assets 右键 → Create → TeaGame/Battle → 出战编目；编辑器菜单可一键从仓库数组生成。
   /// </summary>
   [CreateAssetMenu(fileName = "BattleTeamCatalog", menuName = "TeaGame/Battle/出战编目")]
   public class BattleTeamCatalog : ScriptableObject
   {
      [Header("可出战角色（灰盒=全量；升级=账号持有过滤）")]
      [SerializeField] private RoleItem_Data[] roles;

      [Header("可配武器（灰盒=全量；升级=配装约束）")]
      [SerializeField] private WeaponItem_Data[] weapons;

      public RoleItem_Data[] Roles => roles ?? System.Array.Empty<RoleItem_Data>();
      public WeaponItem_Data[] Weapons => weapons ?? System.Array.Empty<WeaponItem_Data>();

      /// <summary>按 itemID 查找可出战角色（方案 B：会话草稿只存 id，装配前经本目录重解析 SO）。</summary>
      public RoleItem_Data FindRoleById(int itemId)
      {
         var list = Roles;
         for (var i = 0; i < list.Length; i++)
         {
            var role = list[i];
            if (role != null && role.itemID == itemId)
            {
               return role;
            }
         }

         return null;
      }

      /// <summary>按 itemID 查找可配武器（方案 B）。</summary>
      public WeaponItem_Data FindWeaponById(int itemId)
      {
         var list = Weapons;
         for (var i = 0; i < list.Length; i++)
         {
            var weapon = list[i];
            if (weapon != null && weapon.itemID == itemId)
            {
               return weapon;
            }
         }

         return null;
      }
   }
}
