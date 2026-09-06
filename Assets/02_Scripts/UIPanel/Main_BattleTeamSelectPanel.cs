using System.Collections;
using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// 预编队面板数据（v1.0）：Catalog 可由场景 Bootstrap 注入；
   /// 为 null 时回退使用面板 prefab 上序列化的 <see cref="Main_BattleTeamSelectPanel.catalog"/>。
   /// </summary>
   public class Main_BattleTeamSelectPanelData : UIPanelData
   {
      public BattleTeamCatalog Catalog;
   }

   /// <summary>
   /// M1_TeamSelect 预编队面板控制器（v1.0 重构）。
   /// 交互：横排 4 槽（上角色/下武器）；点半区弹列表（「空」恒在第 1 项，占用项置灰排末尾）；
   /// 单击条目 = 右侧 DetailPane 显示简介；双击(300ms) = 应用并关闭；
   /// 底部：清空全队 / 开始战斗（校验 → BattleTeamSession）/ 返回（来源场景，默认 0ConfigScene）。
   /// MVC：本类为 Controller+View 刷新；数据在 <see cref="Main_BattleTeamSelectModel"/>；
   /// 局外读写走 <see cref="IBattlePlayerDataService"/>（灰盒内存实现，v1.0 A2 方案 A）。
   /// prefab：Assets/04_Prefabs/Panel/Main_BattleTeamSelectPanel.prefab（Editor 生成器产出，字段同名绑定）。
   /// </summary>
   public partial class Main_BattleTeamSelectPanel : UIPanel
   {
      [Header("标题")]
      [SerializeField] private Text titleText;

      [Header("4 槽（上=角色 下=武器）")]
      [SerializeField] private Button[] slotRoleButtons;
      [SerializeField] private Text[] slotRoleTexts;
      [SerializeField] private Button[] slotWeaponButtons;
      [SerializeField] private Text[] slotWeaponTexts;

      [Header("底部控制")]
      [SerializeField] private Button clearAllButton;
      [SerializeField] private Button startButton;
      [SerializeField] private Button backButton;
      [SerializeField] private Text toastText;

      [Header("选择弹窗（单一容器复用）")]
      [SerializeField] private GameObject popupWindow;
      [SerializeField] private Button popupMask;
      [SerializeField] private Text popupTitleText;
      [SerializeField] private RectTransform itemListRoot;
      [SerializeField] private Button itemTemplate;
      [SerializeField] private Text detailTitleText;
      [SerializeField] private Text detailBodyText;
      [SerializeField] private Button popupCancelButton;

      [Header("编目（可选回退：未注入 Data 时使用）")]
      [SerializeField] private BattleTeamCatalog catalog;

      /// <summary>双击判定窗口（v1.0 D14：300ms）。</summary>
      private const float DoubleClickWindow = 0.3f;

      private enum PopupKind
      {
         None,
         Role,
         Weapon,
      }

      private sealed class ListEntry
      {
         public bool IsEmpty;          // 「空」条目
         public RoleItem_Data Role;    // 角色条目（Role 弹窗）
         public WeaponItem_Data Weapon; // 武器条目（Weapon 弹窗）
         public bool Disabled;         // 已被其他槽占用
         public int OccupiedSlot = -1; // 占用它的槽（1 起）
         public string Label;
      }

      private Main_BattleTeamSelectModel mModel;
      private IBattlePlayerDataService mService;
      private bool mWired;
      private bool mPopupOpen;
      private PopupKind mPopupKind;
      private int mPopupSlot;
      private readonly List<ListEntry> mEntries = new();
      private int mLastClickEntry = -1;
      private float mLastClickTime = -1000f;
      private Coroutine mToastCoroutine;

      protected override void OnInit(IUIData uiData)
      {
         Wire();
      }

      protected override void OnOpen(IUIData uiData)
      {
         mData = uiData as Main_BattleTeamSelectPanelData ?? new Main_BattleTeamSelectPanelData();
         var resolvedCatalog = ResolveCatalog();
         if (resolvedCatalog == null)
         {
            ShowToast("编目为空：请检查场景注入或 prefab 上的 catalog 引用");
         }

         mService = BattlePlayerDataServiceLocator.Ensure(resolvedCatalog);
         mModel = new Main_BattleTeamSelectModel(mService);
         mModel.FormationChanged += OnFormationChanged;

         if (titleText)
         {
            titleText.text = "出战编队";
         }

         RefreshAll();
      }

      protected override void OnClose()
      {
         if (mModel != null)
         {
            mModel.FormationChanged -= OnFormationChanged;
            mModel = null;
         }

         ClosePopupImmediate();
      }

      // ============================================================
      // 初始化与绑定
      // ============================================================

      private void Wire()
      {
         if (mWired) return;
         mWired = true;

         for (var i = 0; i < GetSlotCount(); i++)
         {
            var index = i;
            var roleBtn = GetSlotArray(slotRoleButtons, i);
            if (roleBtn)
            {
               roleBtn.onClick.AddListener(() => OpenPopup(index, PopupKind.Role));
            }

            var weaponBtn = GetSlotArray(slotWeaponButtons, i);
            if (weaponBtn)
            {
               weaponBtn.onClick.AddListener(() => OpenPopup(index, PopupKind.Weapon));
            }
         }

         if (clearAllButton)
         {
            clearAllButton.onClick.AddListener(OnClearAll);
         }

         if (startButton)
         {
            startButton.onClick.AddListener(OnStartBattle);
         }

         if (backButton)
         {
            backButton.onClick.AddListener(OnBack);
         }

         if (popupMask)
         {
            popupMask.onClick.AddListener(() => { }); // 模态：点击遮罩不关闭
         }

         if (popupCancelButton)
         {
            popupCancelButton.onClick.AddListener(ClosePopup);
         }
      }

      private int GetSlotCount()
      {
         return Mathf.Min(BattleTeamSession.MaxTeamSize,
            slotRoleButtons != null ? slotRoleButtons.Length : 0);
      }

      private static T GetSlotArray<T>(T[] array, int index) where T : Object
      {
         return array != null && index >= 0 && index < array.Length ? array[index] : null;
      }

      private BattleTeamCatalog ResolveCatalog()
      {
         var data = mData as Main_BattleTeamSelectPanelData;
         if (data != null && data.Catalog != null)
         {
            return data.Catalog;
         }

         return catalog;
      }

      // ============================================================
      // 刷新（Model → View）
      // ============================================================

      private void OnFormationChanged()
      {
         RefreshAll();
      }

      private void RefreshAll()
      {
         for (var i = 0; i < BattleTeamSession.MaxTeamSize; i++)
         {
            var slot = mModel != null ? mModel.GetSlot(i) : null;
            var roleText = GetSlotArray(slotRoleTexts, i);
            if (roleText)
            {
               roleText.text = slot != null && slot.IsRoleFilled ? FormatRole(slot.Role) : "空";
            }

            var weaponText = GetSlotArray(slotWeaponTexts, i);
            if (weaponText)
            {
               weaponText.text = slot != null && slot.IsWeaponFilled ? FormatWeapon(slot.Weapon) : "空";
            }
         }
      }

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

      // ============================================================
      // 底部按钮
      // ============================================================

      private void OnClearAll()
      {
         if (mModel == null) return;
         mModel.ClearAll();
         mService?.SaveFormation(ToSlotArray(mModel));
         ShowToast("已清空全队");
      }

      private void OnStartBattle()
      {
         if (mModel == null)
         {
            ShowToast("预编队数据未就绪");
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
            ShowToast("请先选择出战成员（角色 + 武器）");
            return;
         }

         if (firstHalfSlot >= 0)
         {
            ShowToast($"第 {firstHalfSlot + 1} 槽仅选择了角色或武器，请补齐或清空该槽");
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

         ClosePanelThenLoad(BattleTeamSession.BattleSceneName);
      }

      private void OnBack()
      {
         ClosePanelThenLoad(BattleTeamSession.ReturnSceneName);
      }

      private void ClosePanelThenLoad(string sceneName)
      {
         if (UIKit.GetPanel<Main_BattleTeamSelectPanel>() != null)
         {
            UIKit.ClosePanel<Main_BattleTeamSelectPanel>();
         }

         SceneManager.LoadScene(sceneName);
      }

      private static Main_TeamSlotDraft[] ToSlotArray(Main_BattleTeamSelectModel model)
      {
         var arr = new Main_TeamSlotDraft[BattleTeamSession.MaxTeamSize];
         for (var i = 0; i < arr.Length; i++)
         {
            arr[i] = model.GetSlot(i) ?? new Main_TeamSlotDraft();
         }

         return arr;
      }

      // ============================================================
      // 弹窗
      // ============================================================

      private void OpenPopup(int slotIndex, PopupKind kind)
      {
         if (mModel == null || kind == PopupKind.None) return;
         if (slotIndex < 0 || slotIndex >= BattleTeamSession.MaxTeamSize) return;

         mPopupSlot = slotIndex;
         mPopupKind = kind;
         mLastClickEntry = -1;
         mLastClickTime = -1000f;

         BuildEntries();
         RebuildItemList();
         ClearDetail();

         if (popupTitleText)
         {
            popupTitleText.text = kind == PopupKind.Role ? "选择角色" : "选择武器";
         }

         if (popupWindow)
         {
            popupWindow.SetActive(true);
            mPopupOpen = true;
         }
      }

      /// <summary>组装条目：[空] 第 1 项 → 未被占用的编目项 → 已被占用的项置灰排末尾。</summary>
      private void BuildEntries()
      {
         mEntries.Clear();

         // 第 1 项：空（双击=清空该半区）
         mEntries.Add(new ListEntry { IsEmpty = true, Label = "空（清空当前选择）" });

         var roles = mModel.Roles;
         var weapons = mModel.Weapons;
         var roleMode = mPopupKind == PopupKind.Role;

         // 第一遍：未被其他槽占用（按编目序）
         for (var i = 0; i < (roleMode ? roles.Count : weapons.Count); i++)
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

         // 第二遍：已被占用项置灰排末尾
         for (var i = 0; i < (roleMode ? roles.Count : weapons.Count); i++)
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

      /// <summary>重建列表 UI（克隆模板；条目数量很小，灰盒可接受）。</summary>
      private void RebuildItemList()
      {
         if (!itemListRoot || !itemTemplate)
         {
            Debug.LogWarning("[MainTeamSelect] 弹窗列表容器/模板缺失，请重新生成 prefab。");
            return;
         }

         for (var i = itemListRoot.childCount - 1; i >= 0; i--)
         {
            var child = itemListRoot.GetChild(i);
            if (child != itemTemplate.transform)
            {
               Destroy(child.gameObject);
            }
         }

         for (var i = 0; i < mEntries.Count; i++)
         {
            var entry = mEntries[i];
            var go = Instantiate(itemTemplate.gameObject, itemListRoot, false);
            go.name = $"Item_{i}";
            go.SetActive(true);

            var button = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<Text>(true);
            if (label)
            {
               label.text = entry.Label;
               label.color = entry.Disabled ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            }

            if (button)
            {
               button.interactable = !entry.Disabled;
               var index = i;
               button.onClick.AddListener(() => OnEntryClicked(index));
            }
         }
      }

      private void OnEntryClicked(int entryIndex)
      {
         if (entryIndex < 0 || entryIndex >= mEntries.Count) return;
         var entry = mEntries[entryIndex];
         if (entry.Disabled) return;

         var now = Time.realtimeSinceStartup;
         if (entryIndex == mLastClickEntry && now - mLastClickTime <= DoubleClickWindow)
         {
            // 双击：立即应用并关闭
            ApplyEntry(entry);
            mLastClickEntry = -1;
            mLastClickTime = -1000f;
            return;
         }

         // 单击：仅预览简介（后续在此扩展强化/微调操作区）
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
               ShowToast("该角色已被其他槽占用");
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
               ShowToast("该武器已被其他槽占用");
               return;
            }
         }

         mService?.SaveFormation(ToSlotArray(mModel));
         RefreshAll();
         ClosePopup();
      }

      private void ClearDetail()
      {
         if (detailTitleText)
         {
            detailTitleText.text = string.Empty;
         }

         if (detailBodyText)
         {
            detailBodyText.text = "单击条目查看简介；双击应用（300ms 内第二击）。\n「空」为第 1 项；占用项置灰排在末尾。";
         }
      }

      private void ShowDetail(ListEntry entry)
      {
         if (detailTitleText)
         {
            detailTitleText.text = entry.IsEmpty ? "清空" : entry.Label;
         }

         if (!detailBodyText)
         {
            return;
         }

         if (entry.IsEmpty)
         {
            detailBodyText.text = "双击「空」将清空当前槽位的这一半选择。";
            return;
         }

         if (mPopupKind == PopupKind.Role && entry.Role != null)
         {
            var r = entry.Role;
            detailBodyText.text =
               $"角色：{r.itemName}（{r.enName}）\n" +
               $"元素：{ToRoleTag(r.damageElement)}　　最大生命：{r.health}\n" +
               $"小队：{r.onTeam}　势力：{r.onFactions}\n\n简介：{r.profile}";
         }
         else if (mPopupKind == PopupKind.Weapon && entry.Weapon != null)
         {
            var w = entry.Weapon;
            detailBodyText.text =
               $"武器：{w.itemName}\n" +
               $"类型：{ToWeaponTag(w.weaponType)}　子类：{w.weaponSubType}\n" +
               $"伤害：{w.damage}　射程：{w.gunshot}　换弹：{w.reload}s\n" +
               $"弹匣：{w.magazineSize}　射速：{w.firingRate}\n" +
               $"技能：{w.skillName}——{w.skillDescribe}";
         }
      }

      private void ClosePopup()
      {
         if (!mPopupOpen) return;
         ClosePopupImmediate();
      }

      private void ClosePopupImmediate()
      {
         mPopupOpen = false;
         mPopupKind = PopupKind.None;
         mPopupSlot = -1;
         mEntries.Clear();
         if (popupWindow)
         {
            popupWindow.SetActive(false);
         }
      }

      // ============================================================
      // 提示
      // ============================================================

      private void ShowToast(string message)
      {
         if (!toastText)
         {
            Debug.Log($"[MainTeamSelect] {message}");
            return;
         }

         toastText.text = message;
         if (mToastCoroutine != null)
         {
            StopCoroutine(mToastCoroutine);
         }

         mToastCoroutine = StartCoroutine(ClearToastLater());
      }

      private IEnumerator ClearToastLater()
      {
         yield return new WaitForSecondsRealtime(2.5f);
         if (toastText)
         {
            toastText.text = string.Empty;
         }

         mToastCoroutine = null;
      }
   }
}
