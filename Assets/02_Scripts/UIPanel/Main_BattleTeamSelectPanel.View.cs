using System;
using System.Collections;
using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// 预编队面板数据（v1.2）：Catalog 可由场景/入口注入；为 null 时回退 prefab 上序列化 catalog。
   /// </summary>
   public class Main_BattleTeamSelectPanelData : UIPanelData
   {
      public BattleTeamCatalog Catalog;
   }

   /// <summary>
   /// 预编队面板 View partial（v1.2 MVC 细则：V 层）。
   /// 只做四件事：持有 serialized 字段、按入参渲染（槽文本/列表/详情/提示/弹窗显隐）、
   /// 把静态按钮点击转成 <see cref="IMainTeamSelectView"/> 事件上抛。不做业务判定、不写数据。
   /// </summary>
   public partial class Main_BattleTeamSelectPanel
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

      // ---------------- IMainTeamSelectView 事件 ----------------
      public event Action<int, bool> SlotClicked;
      public event Action ClearClicked;
      public event Action StartClicked;
      public event Action BackClicked;
      public event Action<int> ItemClicked;
      public event Action CancelClicked;

      private bool mViewWired;
      private Coroutine mToastCoroutine;

      void IMainTeamSelectView.WireView()
      {
         if (mViewWired) return;
         mViewWired = true;

         if (titleText)
         {
            titleText.text = "出战编队";
         }

         for (var i = 0; i < GetSlotCount(); i++)
         {
            var index = i;
            var roleBtn = GetSlotArray(slotRoleButtons, i);
            if (roleBtn)
            {
               roleBtn.onClick.AddListener(() => SlotClicked?.Invoke(index, true));
            }

            var weaponBtn = GetSlotArray(slotWeaponButtons, i);
            if (weaponBtn)
            {
               weaponBtn.onClick.AddListener(() => SlotClicked?.Invoke(index, false));
            }
         }

         if (clearAllButton)
         {
            clearAllButton.onClick.AddListener(() => ClearClicked?.Invoke());
         }

         if (startButton)
         {
            startButton.onClick.AddListener(() => StartClicked?.Invoke());
         }

         if (backButton)
         {
            backButton.onClick.AddListener(() => BackClicked?.Invoke());
         }

         if (popupMask)
         {
            popupMask.onClick.AddListener(() => { }); // 模态：点击遮罩不关闭
         }

         if (popupCancelButton)
         {
            popupCancelButton.onClick.AddListener(() => CancelClicked?.Invoke());
         }
      }

      void IMainTeamSelectView.RefreshSlots(string[] roleLabels, string[] weaponLabels)
      {
         for (var i = 0; i < BattleTeamSession.MaxTeamSize; i++)
         {
            var roleText = GetSlotArray(slotRoleTexts, i);
            if (roleText)
            {
               roleText.text = roleLabels != null && i < roleLabels.Length ? roleLabels[i] : "空";
            }

            var weaponText = GetSlotArray(slotWeaponTexts, i);
            if (weaponText)
            {
               weaponText.text = weaponLabels != null && i < weaponLabels.Length ? weaponLabels[i] : "空";
            }
         }
      }

      void IMainTeamSelectView.SetPopupVisible(bool visible, string title)
      {
         if (!string.IsNullOrEmpty(title) && popupTitleText)
         {
            popupTitleText.text = title;
         }

         if (popupWindow)
         {
            popupWindow.SetActive(visible);
         }
      }

      void IMainTeamSelectView.PopulateItems(IReadOnlyList<MainTeamSelectViewItem> items)
      {
         if (!itemListRoot || !itemTemplate)
         {
            Debug.LogWarning("[MainTeamSelect.View] 弹窗列表容器/模板缺失，请重新生成 prefab。");
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

         for (var i = 0; i < items.Count; i++)
         {
            var item = items[i];
            var go = Instantiate(itemTemplate.gameObject, itemListRoot, false);
            go.name = $"Item_{i}";
            go.SetActive(true);

            var button = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<Text>(true);
            if (label)
            {
               label.text = item.Label;
               label.color = item.Disabled ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            }

            if (button)
            {
               button.interactable = !item.Disabled;
               var index = i;
               button.onClick.AddListener(() => ItemClicked?.Invoke(index));
            }
         }
      }

      void IMainTeamSelectView.SetDetail(string title, string body)
      {
         if (detailTitleText)
         {
            detailTitleText.text = title ?? string.Empty;
         }

         if (detailBodyText)
         {
            detailBodyText.text = body ?? string.Empty;
         }
      }

      void IMainTeamSelectView.ShowToast(string message)
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

      private int GetSlotCount()
      {
         return Mathf.Min(BattleTeamSession.MaxTeamSize,
            slotRoleButtons != null ? slotRoleButtons.Length : 0);
      }

      private static T GetSlotArray<T>(T[] array, int index) where T : UnityEngine.Object
      {
         return array != null && index >= 0 && index < array.Length ? array[index] : null;
      }
   }
}
