using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame
{
   /// <summary>
   /// 预编队 Controller（v1.2 MVC 细则：C 层）。
   /// 纯 C#：持有交互状态机、唯一性应用、校验与数据写回；只经 <see cref="IMainTeamSelectView"/> 触达 UI，
   /// 不直接引用任何 serialized 字段。
   /// 数据读写走 <see cref="Main_BattleTeamSelectModel"/>（M）+ <see cref="IBattlePlayerDataService"/>（M·Data，
   /// M2 换账号实现接入 Profile/Grow/Bag）。
   /// 战斗只消费 <see cref="BattleTeamSession"/> 快照（写入口在本类 StartBattle）。
   /// </summary>
   public sealed class Main_BattleTeamSelectController
   {
      private const float DoubleClickWindow = 0.3f;

      private readonly IMainTeamSelectView mView;
      private Main_BattleTeamSelectModel mModel;
      private IBattlePlayerDataService mService;
      private bool mOpen;

      // 弹窗状态机（v1.1：状态内聚 Controller）
      private enum PopupKind
      {
         None,
         Role,
         Weapon,
      }

      private sealed class ListEntry
      {
         public bool IsEmpty;
         public RoleItem_Data Role;
         public WeaponItem_Data Weapon;
         public bool Disabled;
         public int OccupiedSlot = -1;
         public string Label;
      }

      private PopupKind mPopupKind = PopupKind.None;
      private int mPopupSlot = -1;
      private readonly List<ListEntry> mEntries = new();
      private int mLastClickEntry = -1;
      private float mLastClickTime = -1000f;

      public Main_BattleTeamSelectController(IMainTeamSelectView view)
      {
         mView = view ?? throw new ArgumentNullException(nameof(view));
      }

      // ---------------- 生命周期 ----------------

      /// <summary>面板打开：装载服务/Model、订阅事件、首次刷新。</summary>
      public void Open(BattleTeamCatalog catalog)
      {
         if (mOpen) return;
         mOpen = true;

         mService = BattlePlayerDataServiceLocator.Ensure(catalog);
         mModel = new Main_BattleTeamSelectModel(mService);
         mModel.FormationChanged += OnFormationChanged;

         mView.SlotClicked += OnSlotClicked;
         mView.ClearClicked += OnClearAll;
         mView.StartClicked += OnStartBattle;
         mView.BackClicked += OnBack;
         mView.ItemClicked += OnItemClicked;
         mView.CancelClicked += OnCancel;

         if (catalog == null)
         {
            mView.ShowToast("编目为空：请检查场景注入或 prefab 上的 catalog 引用");
         }

         RefreshSlots();
      }

      /// <summary>面板关闭：解事件、隐藏弹窗、释放 Model 引用。</summary>
      public void Close()
      {
         if (!mOpen) return;
         mOpen = false;

         if (mModel != null)
         {
            mModel.FormationChanged -= OnFormationChanged;
            mModel = null;
         }

         mView.SlotClicked -= OnSlotClicked;
         mView.ClearClicked -= OnClearAll;
         mView.StartClicked -= OnStartBattle;
         mView.BackClicked -= OnBack;
         mView.ItemClicked -= OnItemClicked;
         mView.CancelClicked -= OnCancel;

         ClosePopupInternal();
      }

      // ---------------- Model 变更 → 刷新 ----------------

      private void OnFormationChanged()
      {
         RefreshSlots();
      }

      private void RefreshSlots()
      {
         var roleLabels = new string[BattleTeamSession.MaxTeamSize];
         var weaponLabels = new string[BattleTeamSession.MaxTeamSize];
         for (var i = 0; i < BattleTeamSession.MaxTeamSize; i++)
         {
            var slot = mModel?.GetSlot(i);
            roleLabels[i] = slot != null && slot.IsRoleFilled ? FormatRole(slot.Role) : "空";
            weaponLabels[i] = slot != null && slot.IsWeaponFilled ? FormatWeapon(slot.Weapon) : "空";
         }

         mView.RefreshSlots(roleLabels, weaponLabels);
      }

      // ---------------- 槽点击 → 弹窗 ----------------

      private void OnSlotClicked(int slotIndex, bool isRole)
      {
         if (!mOpen || mModel == null) return;
         if (slotIndex < 0 || slotIndex >= BattleTeamSession.MaxTeamSize) return;

         mPopupSlot = slotIndex;
         mPopupKind = isRole ? PopupKind.Role : PopupKind.Weapon;
         mLastClickEntry = -1;
         mLastClickTime = -1000f;

         BuildEntries();
         var title = isRole ? "选择角色" : "选择武器";
         mView.SetPopupVisible(true, title);
         mView.PopulateItems(ToViewItems());
         mView.SetDetail(string.Empty,
            "单击条目查看简介；双击应用（300ms 内第二击）。\n「空」为第 1 项；占用项置灰排在末尾。");
      }

      /// <summary>组装条目：[空] 第 1 项 → 未占用编目项 → 占用项置灰末尾。</summary>
      private void BuildEntries()
      {
         mEntries.Clear();
         mEntries.Add(new ListEntry { IsEmpty = true, Label = "空（清空当前选择）" });

         var roleMode = mPopupKind == PopupKind.Role;
         var roles = mModel.Roles;
         var weapons = mModel.Weapons;
         var count = roleMode ? roles.Count : weapons.Count;

         for (var i = 0; i < count; i++)
         {
            if (roleMode)
            {
               var role = roles[i];
               if (role == null || mModel.FindRoleOccupiedSlot(mPopupSlot, role) >= 0) continue;
               mEntries.Add(new ListEntry { Role = role, Label = FormatRole(role) });
            }
            else
            {
               var weapon = weapons[i];
               if (weapon == null || mModel.FindWeaponOccupiedSlot(mPopupSlot, weapon) >= 0) continue;
               mEntries.Add(new ListEntry { Weapon = weapon, Label = FormatWeapon(weapon) });
            }
         }

         for (var i = 0; i < count; i++)
         {
            if (roleMode)
            {
               var role = roles[i];
               if (role == null) continue;
               var occupied = mModel.FindRoleOccupiedSlot(mPopupSlot, role);
               if (occupied < 0) continue;
               mEntries.Add(new ListEntry
               {
                  Role = role,
                  Disabled = true,
                  OccupiedSlot = occupied,
                  Label = $"{FormatRole(role)}（已占用·槽 {occupied + 1}）",
               });
            }
            else
            {
               var weapon = weapons[i];
               if (weapon == null) continue;
               var occupied = mModel.FindWeaponOccupiedSlot(mPopupSlot, weapon);
               if (occupied < 0) continue;
               mEntries.Add(new ListEntry
               {
                  Weapon = weapon,
                  Disabled = true,
                  OccupiedSlot = occupied,
                  Label = $"{FormatWeapon(weapon)}（已占用·槽 {occupied + 1}）",
               });
            }
         }
      }

      private List<MainTeamSelectViewItem> ToViewItems()
      {
         var items = new List<MainTeamSelectViewItem>(mEntries.Count);
         foreach (var e in mEntries)
         {
            items.Add(new MainTeamSelectViewItem { Label = e.Label, Disabled = e.Disabled });
         }

         return items;
      }

      // ---------------- 条目单击/双击（300ms） ----------------

      private void OnItemClicked(int entryIndex)
      {
         if (!mOpen || mModel == null) return;
         if (entryIndex < 0 || entryIndex >= mEntries.Count) return;
         var entry = mEntries[entryIndex];
         if (entry.Disabled) return;

         var now = Time.realtimeSinceStartup;
         if (entryIndex == mLastClickEntry && now - mLastClickTime <= DoubleClickWindow)
         {
            ApplyEntry(entry);
            mLastClickEntry = -1;
            mLastClickTime = -1000f;
            return;
         }

         // 单击：预览简介（DetailPane 为后续强化/微调扩展点）
         mLastClickEntry = entryIndex;
         mLastClickTime = now;
         ShowDetail(entry);
      }

      private void ApplyEntry(ListEntry entry)
      {
         if (mModel == null) return;

         if (mPopupKind == PopupKind.Role)
         {
            if (entry.IsEmpty)
            {
               mModel.ClearHalf(mPopupSlot, true);
            }
            else if (!mModel.TrySetRole(mPopupSlot, entry.Role))
            {
               mView.ShowToast("该角色已被其他槽占用");
               return;
            }
         }
         else
         {
            if (entry.IsEmpty)
            {
               mModel.ClearHalf(mPopupSlot, false);
            }
            else if (!mModel.TrySetWeapon(mPopupSlot, entry.Weapon))
            {
               mView.ShowToast("该武器已被其他槽占用");
               return;
            }
         }

         SaveFormation();
         RefreshSlots();
         ClosePopupInternal();
      }

      private void ShowDetail(ListEntry entry)
      {
         if (entry.IsEmpty)
         {
            mView.SetDetail("清空", "双击「空」将清空当前槽位的这一半选择。");
            return;
         }

         if (mPopupKind == PopupKind.Role && entry.Role != null)
         {
            var r = entry.Role;
            mView.SetDetail(FormatRole(r),
               $"角色：{r.itemName}（{r.enName}）\n" +
               $"元素：{ToRoleTag(r.damageElement)}　　最大生命：{r.health}\n" +
               $"小队：{r.onTeam}　势力：{r.onFactions}\n\n简介：{r.profile}");
         }
         else if (mPopupKind == PopupKind.Weapon && entry.Weapon != null)
         {
            var w = entry.Weapon;
            mView.SetDetail(FormatWeapon(w),
               $"武器：{w.itemName}\n" +
               $"类型：{ToWeaponTag(w.weaponType)}　子类：{w.weaponSubType}\n" +
               $"伤害：{w.damage}　射程：{w.gunshot}　换弹：{w.reload}s\n" +
               $"弹匣：{w.magazineSize}　射速：{w.firingRate}\n" +
               $"技能：{w.skillName}——{w.skillDescribe}");
         }
      }

      // ---------------- 底部操作 ----------------

      private void OnClearAll()
      {
         if (!mOpen || mModel == null) return;
         mModel.ClearAll();
         SaveFormation();
         mView.ShowToast("已清空全队");
      }

      private void OnStartBattle()
      {
         if (!mOpen || mModel == null)
         {
            mView.ShowToast("预编队数据未就绪");
            return;
         }

         var completeCount = 0;
         var firstHalfSlot = -1;
         for (var i = 0; i < BattleTeamSession.MaxTeamSize; i++)
         {
            var slot = mModel.GetSlot(i);
            if (slot == null) continue;
            if (slot.IsComplete)
            {
               completeCount++;
            }
            else if (slot.IsRoleFilled || slot.IsWeaponFilled)
            {
               firstHalfSlot = firstHalfSlot < 0 ? i : firstHalfSlot;
            }
         }

         if (completeCount == 0 && firstHalfSlot < 0)
         {
            mView.ShowToast("请先选择出战成员（角色 + 武器）");
            return;
         }

         if (firstHalfSlot >= 0)
         {
            mView.ShowToast($"第 {firstHalfSlot + 1} 槽仅选择了角色或武器，请补齐或清空该槽");
            return;
         }

         var drafts = new List<BattleTeamMemberDraft>(BattleTeamSession.MaxTeamSize);
         for (var i = 0; i < BattleTeamSession.MaxTeamSize; i++)
         {
            var slot = mModel.GetSlot(i);
            if (slot != null && slot.IsComplete)
            {
               drafts.Add(new BattleTeamMemberDraft(slot.Role, slot.Weapon));
            }
         }

         BattleTeamSession.Write(drafts);
         Debug.Log($"[MainTeamSelect] 编队确认：{drafts.Count} 人出战（槽序=切人序），已写入 BattleTeamSession。");
         mView.ShowToast($"出战确认：{drafts.Count} 人");
         // 场景切换由 Shell 处理（避免 Controller 直持 UIKit）
         OnRequestExitScene?.Invoke(BattleTeamSession.BattleSceneName);
      }

      private void OnBack()
      {
         OnRequestExitScene?.Invoke(BattleTeamSession.ReturnSceneName);
      }

      /// <summary>请求切换场景（Shell 订阅执行：ClosePanel + LoadScene）。</summary>
      public event Action<string> OnRequestExitScene;

      private void OnCancel()
      {
         ClosePopupInternal();
      }

      private void ClosePopupInternal()
      {
         if (mPopupKind == PopupKind.None) return;
         mPopupKind = PopupKind.None;
         mPopupSlot = -1;
         mEntries.Clear();
         mView.SetPopupVisible(false, string.Empty);
      }

      private void SaveFormation()
      {
         if (mModel == null) return;
         var arr = new Main_TeamSlotDraft[BattleTeamSession.MaxTeamSize];
         for (var i = 0; i < arr.Length; i++)
         {
            arr[i] = mModel.GetSlot(i) ?? new Main_TeamSlotDraft();
         }

         mService?.SaveFormation(arr);
      }

      // ---------------- 展示格式化 ----------------

      private static string FormatRole(RoleItem_Data role)
      {
         return role == null ? "空" : $"{role.itemName} · {ToRoleTag(role.damageElement)}";
      }

      private static string FormatWeapon(WeaponItem_Data weapon)
      {
         return weapon == null ? "空" : $"{weapon.itemName} · {ToWeaponTag(weapon.weaponType)}";
      }

      private static string ToRoleTag(DamageElement element)
      {
         switch (element)
         {
            case DamageElement.Ice: return "冰";
            case DamageElement.Fire: return "火";
            case DamageElement.Wind: return "风";
            case DamageElement.Electric: return "雷";
            default: return "无";
         }
      }

      private static string ToWeaponTag(WeaponType type)
      {
         switch (type)
         {
            case WeaponType.wep_Assault: return "突击";
            case WeaponType.wep_Heavy: return "重装";
            case WeaponType.wep_Scatter: return "散射";
            case WeaponType.wep_Sniper: return "狙击";
            default: return "未分类";
         }
      }
   }
}
