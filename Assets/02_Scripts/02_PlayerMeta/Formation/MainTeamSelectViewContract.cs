using System;
using System.Collections.Generic;

namespace TeaGame
{
   /// <summary>
   /// 槽位展示数据（View 消费，不接触 Role/Weapon 业务对象）。
   /// Formation_Slot 拆分显示：主名 + 副标签（角色=元素，武器=类型）。
   /// </summary>
   public sealed class MainFormationSlotView
   {
      public string Label = string.Empty;     // 「槽 1」
      public string RoleName = "空";          // Role_Text
      public string RoleTag = string.Empty;   // Role_Text2（元素）
      public string WeaponName = "空";        // Wep_Text
      public string WeaponTag = string.Empty; // Wep_Text2（类型）
   }

   /// <summary>
   /// 列表条目展示数据（View 只消费展示字段，不接触业务对象）。
   /// </summary>
   public sealed class MainTeamSelectViewItem
   {
      public string Label;
      public bool Disabled;
   }

   /// <summary>
   /// 预编队 View 契约（v1.2 MVC：Shell→Controller→IMainFormationPanelView）。
   /// View 只做：按 Designer/Formation_Slot 渲染、弹窗显隐、列表/详情/提示展示；把点击以「事件」上抛给 Controller。
   /// 事件签名只用基础类型，避免 View 反向依赖数据/业务对象。
   /// 弹窗关闭：popup 底部「取消」按钮（prefab 层级名 PopupCancel）点击 = 放弃本次未应用的选择。
   /// </summary>
   public interface IMainFormationPanelView
   {
      /// <summary>点击某槽半区：(槽位 0..3, 是否角色区)。</summary>
      event Action<int, bool> SlotClicked;

      event Action ClearClicked;
      event Action StartClicked;
      event Action BackClicked;

      /// <summary>点击弹窗列表第 index 项。</summary>
      event Action<int> ItemClicked;

      /// <summary>点击弹窗「取消/退出」按钮：关闭弹窗、放弃未应用的选择。</summary>
      event Action PopupCancelClicked;

      /// <summary>绑定静态按钮/槽区域 → 事件（幂等，Shell 在 OnInit 调用一次）。</summary>
      void WireView();

      /// <summary>刷新 4 槽显示（每槽拆 主名+副标签）。</summary>
      void RefreshSlots(IReadOnlyList<MainFormationSlotView> slots);

      /// <summary>弹窗显隐 + 标题。</summary>
      void SetPopupVisible(bool visible, string title);

      /// <summary>重建列表条目（克隆 ItemTemplate_Base）。</summary>
      void PopulateItems(IReadOnlyList<MainTeamSelectViewItem> items);

      /// <summary>右侧简介区。</summary>
      void SetDetail(string title, string body);

      /// <summary>底部提示。</summary>
      void ShowToast(string message);
   }
}
