using System;
using System.Collections.Generic;

namespace TeaGame
{
   /// <summary>
   /// 列表条目展示数据（View 只消费展示字段，不接触 Role/Weapon 业务对象）。
   /// </summary>
   public sealed class MainTeamSelectViewItem
   {
      public string Label;
      public bool Disabled;
   }

   /// <summary>
   /// 预编队 View 契约（v1.2 MVC：Shell→Controller→IMainTeamSelectView）。
   /// View 只做：字段渲染、弹窗显隐、列表/详情/提示展示；把点击以「事件」上抛给 Controller。
   /// 事件签名只用基础类型，避免 View 反向依赖数据/业务对象。
   /// </summary>
   public interface IMainTeamSelectView
   {
      /// <summary>点击某槽半区：(槽位 0..3, 是否角色区)。</summary>
      event Action<int, bool> SlotClicked;

      event Action ClearClicked;
      event Action StartClicked;
      event Action BackClicked;

      /// <summary>点击弹窗列表第 index 项。</summary>
      event Action<int> ItemClicked;

      event Action CancelClicked;

      /// <summary>绑定静态按钮 → 事件（幂等，由 Shell 在 OnInit 调用一次）。</summary>
      void WireView();

      /// <summary>刷新 4 槽显示文本（入参为展示字符串，长度=4）。</summary>
      void RefreshSlots(string[] roleLabels, string[] weaponLabels);

      /// <summary>弹窗显隐 + 标题。</summary>
      void SetPopupVisible(bool visible, string title);

      /// <summary>重建列表条目（模板克隆）。</summary>
      void PopulateItems(IReadOnlyList<MainTeamSelectViewItem> items);

      /// <summary>右侧简介区。</summary>
      void SetDetail(string title, string body);

      /// <summary>底部提示。</summary>
      void ShowToast(string message);
   }
}
