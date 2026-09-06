using System;
using System.Collections;
using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// 预编队面板数据（v1.2）：Catalog 可由场景/入口注入；为 null 时回退 prefab 上序列化 catalog。
   /// </summary>
   public class Main_FormationPanelData : UIPanelData
   {
      public BattleTeamCatalog Catalog;
   }

   /// <summary>
   /// Main_FormationPanel（Shell + View 合并为单文件，v1.3 MVC 细则调整版）。
   /// Shell 部分：UIKit 生命周期与装配——创建 <see cref="Main_FormationController"/>（C），
   /// 以自身（实现 <see cref="IMainFormationPanelView"/>）注入；「退出」请求落为仅 ClosePanel，
   /// 「开始出战」请求落为记录来源场景 + CloseAllPanel + <see cref="BattleSceneGate"/>（config 加载遮罩过渡）。
   /// View 部分：使用 CodeGen Designer 绑定字段（Title_Text / Formation_Slot_01~04 / 底部按钮 / Toast / 弹窗容器）
   /// 与 <see cref="Formation_Slot"/> 拆分显示（主名+副标签），只渲染 + 把点击上抛事件；不含业务判定、不写数据。
   /// </summary>
   public partial class Main_FormationPanel : UIPanel, IMainFormationPanelView
   {
      // ===================== Shell：装配 =====================

      private Main_FormationController mController;

      [Header("编目（可选回退：未注入 Data 时使用，在 prefab Inspector 挂引用）")]
      [SerializeField] private BattleTeamCatalog catalog;

      protected override void OnInit(IUIData uiData)
      {
         ((IMainFormationPanelView)this).WireView();
      }

      protected override void OnOpen(IUIData uiData)
      {
         mData = uiData as Main_FormationPanelData ?? new Main_FormationPanelData();
         var resolvedCatalog = ResolveCatalog();

         mController = new Main_FormationController(this);
         mController.OnRequestExitScene += HandleRequestExitScene;
         mController.OnRequestClosePanel += HandleRequestClosePanel;
         mController.Open(resolvedCatalog);
      }

      protected override void OnClose()
      {
         if (mController != null)
         {
            mController.OnRequestExitScene -= HandleRequestExitScene;
            mController.OnRequestClosePanel -= HandleRequestClosePanel;
            mController.Close();
            mController = null;
         }
      }

      private BattleTeamCatalog ResolveCatalog()
      {
         var data = mData as Main_FormationPanelData;
         if (data != null && data.Catalog != null)
         {
            return data.Catalog;
         }

         return catalog;
      }

      /// <summary>「退出」：仅关闭本面板（覆盖层语义），不加载任何场景。</summary>
      private void HandleRequestClosePanel()
      {
         UIKit.ClosePanel<Main_FormationPanel>();
      }

      /// <summary>
      /// 「开始出战」：先记录来源场景（供 Battle 离开后经遮罩回到本场景），
      /// 再清空 UIKit 面板表并走 SceneFlow（0ConfigScene 的加载遮罩）过渡到目标场景。
      /// </summary>
      private void HandleRequestExitScene(string sceneName)
      {
         var activeSceneName = SceneManager.GetActiveScene().name;
         if (!string.IsNullOrEmpty(activeSceneName) && activeSceneName != BattleTeamSession.ConfigSceneName)
         {
            BattleTeamSession.SourceSceneName = activeSceneName;
         }

         // 离开前清空 UIKit 面板表，避免场景级 UIRoot 卸载后 Table 残留已销毁面板
         // （IPanel == null 不触发 Unity 判空 → 下次 OpenPanel 复用已销毁实例抛 MissingReference）
         UIKit.CloseAllPanel();

         BattleSceneGate.TransitionTo(sceneName);
      }

      // ===================== View：事件（IMainFormationPanelView） =====================

      public event Action<int, bool> SlotClicked;
      public event Action ClearClicked;
      public event Action StartClicked;
      public event Action BackClicked;
      public event Action<int> ItemClicked;
      public event Action PopupCancelClicked;

      private bool mViewWired;
      private Coroutine mToastCoroutine;

      void IMainFormationPanelView.WireView()
      {
         if (mViewWired) return;
         mViewWired = true;

         if (Title_Text)
         {
            Title_Text.text = "出战编队";
         }

         var slots = GetSlots();
         for (var i = 0; i < slots.Length; i++)
         {
            var index = i;
            if (slots[i] == null) continue;

            if (slots[i].RoleArea)
            {
               slots[i].RoleArea.onClick.AddListener(() => SlotClicked?.Invoke(index, true));
            }

            if (slots[i].WeaponArea)
            {
               slots[i].WeaponArea.onClick.AddListener(() => SlotClicked?.Invoke(index, false));
            }
         }

         if (ClearAllButton)
         {
            ClearAllButton.onClick.AddListener(() => ClearClicked?.Invoke());
         }

         if (StartButton)
         {
            StartButton.onClick.AddListener(() => StartClicked?.Invoke());
         }

         if (BackButton)
         {
            BackButton.onClick.AddListener(() => BackClicked?.Invoke());
         }

         // 弹窗「取消/退出」按钮：点击 = 放弃本次未应用的选择并关闭弹窗
         var popupCancel = ResolvePopupCancelButton();
         if (popupCancel)
         {
            popupCancel.onClick.AddListener(() => PopupCancelClicked?.Invoke());
         }
         else if (PopupWindow)
         {
            Debug.LogWarning("[Main_FormationPanel] 未在 PopupWindow 下找到 PopupCancel 按钮，弹窗只能经条目双击应用/清空来关闭。");
         }

         // 弹窗初始化隐藏（兜底：与 prefab 默认态一致，避免首帧闪现）
         if (PopupWindow)
         {
            PopupWindow.gameObject.SetActive(false);
         }
      }

      /// <summary>
      /// 解析弹窗「取消/退出」按钮。
      /// 优先方式：日后在 prefab 上给该按钮加 CodeGen Bind（如 PopupCancelButton）并重新生成 Designer 后，
      /// 可在此改为直接引用绑定字段。
      /// 当前实现：在 PopupWindow 下按层级名查找名为 PopupCancel 的 Button（其子 Text=「取消」），
      /// 不依赖重新生成 Designer。
      /// </summary>
      private Button ResolvePopupCancelButton()
      {
         if (PopupWindow == null) return null;

         var transforms = PopupWindow.GetComponentsInChildren<Transform>(true);
         for (var i = 0; i < transforms.Length; i++)
         {
            if (transforms[i].name != "PopupCancel") continue;

            var button = transforms[i].GetComponent<Button>();
            if (button != null) return button;
         }

         return null;
      }

      void IMainFormationPanelView.RefreshSlots(IReadOnlyList<MainFormationSlotView> slotViews)
      {
         var slots = GetSlots();
         for (var i = 0; i < slots.Length && i < (slotViews?.Count ?? 0); i++)
         {
            var slot = slots[i];
            var view = slotViews[i];
            if (slot == null || view == null) continue;

            if (slot.SlotLabel_Text)
            {
               slot.SlotLabel_Text.text = string.IsNullOrEmpty(view.Label) ? $"槽 {i + 1}" : view.Label;
            }

            if (slot.Role_Text)
            {
               slot.Role_Text.text = view.RoleName;
            }

            if (slot.Role_Text2)
            {
               slot.Role_Text2.text = view.RoleTag;
            }

            if (slot.Wep_Text)
            {
               slot.Wep_Text.text = view.WeaponName;
            }

            if (slot.Wep_Text2)
            {
               slot.Wep_Text2.text = view.WeaponTag;
            }
         }
      }

      void IMainFormationPanelView.SetPopupVisible(bool visible, string title)
      {
         if (!string.IsNullOrEmpty(title) && PopupTitle_Text)
         {
            PopupTitle_Text.text = title;
         }

         if (PopupWindow)
         {
            PopupWindow.gameObject.SetActive(visible);
         }
      }

      void IMainFormationPanelView.PopulateItems(IReadOnlyList<MainTeamSelectViewItem> items)
      {
         var container = ItemList != null ? ItemList.rectTransform : null;
         if (container == null || ItemTemplate_Base == null)
         {
            Debug.LogWarning("[Main_FormationPanel] 弹窗列表容器/模板缺失，请在 prefab 中核对 ItemList / ItemTemplate_Base 绑定。");
            return;
         }

         for (var i = container.childCount - 1; i >= 0; i--)
         {
            var child = container.GetChild(i);
            if (child != ItemTemplate_Base.transform)
            {
               Destroy(child.gameObject);
            }
         }

         for (var i = 0; i < items.Count; i++)
         {
            var item = items[i];
            var go = Instantiate(ItemTemplate_Base.gameObject, container, false);
            go.name = $"Item_{i}";
            go.SetActive(true);

            var label = go.GetComponentInChildren<Text>(true);
            if (label)
            {
               label.text = item.Label;
               label.color = item.Disabled ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            }

            var button = go.GetComponent<Button>();
            if (button)
            {
               button.interactable = !item.Disabled;
               var index = i;
               button.onClick.AddListener(() => ItemClicked?.Invoke(index));
            }
         }
      }

      void IMainFormationPanelView.SetDetail(string title, string body)
      {
         if (DetailTitle_Text)
         {
            DetailTitle_Text.text = title ?? string.Empty;
         }

         if (DetailBody_Text)
         {
            DetailBody_Text.text = body ?? string.Empty;
         }
      }

      void IMainFormationPanelView.ShowToast(string message)
      {
         if (!Toast_Text)
         {
            Debug.Log($"[Main_FormationPanel] {message}");
            return;
         }

         Toast_Text.text = message;
         if (mToastCoroutine != null)
         {
            StopCoroutine(mToastCoroutine);
         }

         mToastCoroutine = StartCoroutine(ClearToastLater());
      }

      private IEnumerator ClearToastLater()
      {
         yield return new WaitForSecondsRealtime(2.5f);
         if (Toast_Text)
         {
            Toast_Text.text = string.Empty;
         }

         mToastCoroutine = null;
      }

      /// <summary>4 槽组件（Designer 绑定顺序 = 槽序 = 切人序）。</summary>
      private Formation_Slot[] GetSlots()
      {
         return new[] { Formation_Slot_01, Formation_Slot_02, Formation_Slot_03, Formation_Slot_04 };
      }
   }
}
