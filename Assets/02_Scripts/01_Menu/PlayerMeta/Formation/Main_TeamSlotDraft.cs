using System;

namespace TeaGame
{
   /// <summary>
   /// 预编队槽草稿（v1.0：局外出战配置 + 局内出战会话共用的槽级绑定）。
   /// 每槽 = 角色 + 武器两个独立半区，均可为 null（空槽不参战）；
   /// 绑定关系是“槽位级配装方案”，不是角色私有装备。
   /// 变更由 <see cref="Main_FormationModel"/> 统一执行并触发事件。
   /// </summary>
   [Serializable]
   public sealed class Main_TeamSlotDraft
   {
      public RoleItem_Data Role;
      public WeaponItem_Data Weapon;

      public bool IsRoleFilled => Role != null;
      public bool IsWeaponFilled => Weapon != null;

      /// <summary>是否完整（角色+武器都填）——只有完整槽才出战。</summary>
      public bool IsComplete => Role != null && Weapon != null;
   }
}
