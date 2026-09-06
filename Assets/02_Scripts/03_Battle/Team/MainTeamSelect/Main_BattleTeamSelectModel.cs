using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 预编队 Model（v1.0 D9：独立 Model + 面板控制器，无全局 Architecture）。
   /// 职责：4 槽草稿（含唯一性规则）、可用列表查询、变更入口、变更事件。
   /// 持久化不在 Model 内：面板每次变更后调用 <see cref="IBattlePlayerDataService.SaveFormation"/>。
   /// </summary>
   public sealed class Main_BattleTeamSelectModel
   {
      private readonly Main_TeamSlotDraft[] mSlots;

      /// <summary>槽位数（=出战人数上限=切人上限）。</summary>
      public int SlotCount => BattleTeamSession.MaxTeamSize;

      /// <summary>任意变更后触发（UI 监听后整体刷新）。</summary>
      public event Action FormationChanged;

      /// <summary>可用角色/武器源（来自服务）。</summary>
      public IReadOnlyList<RoleItem_Data> Roles { get; }
      public IReadOnlyList<WeaponItem_Data> Weapons { get; }

      public Main_BattleTeamSelectModel(IBattlePlayerDataService service)
      {
         Roles = service.Roles;
         Weapons = service.Weapons;

         mSlots = new Main_TeamSlotDraft[SlotCount];
         for (var i = 0; i < mSlots.Length; i++)
         {
            mSlots[i] = new Main_TeamSlotDraft();
         }

         service.LoadFormation(mSlots);
      }

      public Main_TeamSlotDraft GetSlot(int index)
      {
         return index >= 0 && index < mSlots.Length ? mSlots[index] : null;
      }

      // ---------------- 唯一性查询 ----------------

      /// <summary>该角色是否已被其他槽占用（排除 slotIndex 自身）。</summary>
      public int FindRoleOccupiedSlot(int exceptIndex, RoleItem_Data role)
      {
         return FindOccupiedSlot(exceptIndex, r => ReferenceEquals(r, role), w => false);
      }

      /// <summary>该武器是否已被其他槽占用。</summary>
      public int FindWeaponOccupiedSlot(int exceptIndex, WeaponItem_Data weapon)
      {
         return FindOccupiedSlot(exceptIndex, r => false, w => ReferenceEquals(w, weapon));
      }

      private int FindOccupiedSlot(int exceptIndex, Func<RoleItem_Data, bool> matchRole, Func<WeaponItem_Data, bool> matchWeapon)
      {
         for (var i = 0; i < mSlots.Length; i++)
         {
            if (i == exceptIndex) continue;
            var s = mSlots[i];
            if (s == null) continue;
            if ((s.Role != null && matchRole(s.Role)) || (s.Weapon != null && matchWeapon(s.Weapon)))
            {
               return i;
            }
         }

         return -1;
      }

      // ---------------- 变更入口（唯一性约束） ----------------

      /// <summary>尝试设置某槽角色（已在其他槽占用则失败）。成功返回 true 并触发事件。</summary>
      public bool TrySetRole(int slotIndex, RoleItem_Data role)
      {
         var slot = GetSlot(slotIndex);
         if (slot == null) return false;
         if (role != null && FindRoleOccupiedSlot(slotIndex, role) >= 0) return false;

         slot.Role = role;
         RaiseChanged();
         return true;
      }

      /// <summary>尝试设置某槽武器（已在其他槽占用则失败）。</summary>
      public bool TrySetWeapon(int slotIndex, WeaponItem_Data weapon)
      {
         var slot = GetSlot(slotIndex);
         if (slot == null) return false;
         if (weapon != null && FindWeaponOccupiedSlot(slotIndex, weapon) >= 0) return false;

         slot.Weapon = weapon;
         RaiseChanged();
         return true;
      }

      public void ClearSlot(int slotIndex)
      {
         var slot = GetSlot(slotIndex);
         if (slot == null || (!slot.IsRoleFilled && !slot.IsWeaponFilled)) return;
         slot.Role = null;
         slot.Weapon = null;
         RaiseChanged();
      }

      /// <summary>只清某半区（弹窗内双击「空」）。isRole=true 清角色，false 清武器。</summary>
      public void ClearHalf(int slotIndex, bool isRole)
      {
         var slot = GetSlot(slotIndex);
         if (slot == null) return;
         if (isRole)
         {
            if (!slot.IsRoleFilled) return;
            slot.Role = null;
         }
         else
         {
            if (!slot.IsWeaponFilled) return;
            slot.Weapon = null;
         }

         RaiseChanged();
      }

      public void ClearAll()
      {
         var changed = false;
         for (var i = 0; i < mSlots.Length; i++)
         {
            if (mSlots[i].IsRoleFilled || mSlots[i].IsWeaponFilled)
            {
               mSlots[i].Role = null;
               mSlots[i].Weapon = null;
               changed = true;
            }
         }

         if (changed) RaiseChanged();
      }

      private void RaiseChanged()
      {
         FormationChanged?.Invoke();
      }
   }
}
