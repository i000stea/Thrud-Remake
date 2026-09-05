using System.Collections.Generic;
using QFramework;
using TeaGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// 选队面板数据：编目（角色/武器来源）由场景 Bootstrap 注入。
   /// </summary>
   public class BattleTeamSelectPanelData : UIPanelData
   {
      public BattleTeamCatalog Catalog;
   }

   /// <summary>
   /// T3.1A 出战编队面板（灰盒列表）。
   /// prefab：Assets/Resources/Panels/BattleTeamSelectPanel.prefab（Editor 工具生成，根节点挂本组件，AB=battleteamselectpanel_prefab）。
   /// 交互：
   /// - 点左侧「角色」、中间「武器」→ 组成"待加入组合"；
   /// - 「加入」→ 追加到队伍槽（最多 4）；「移除选中/清空」管理；
   /// - 「↑上移/↓下移」调整所选槽顺序（槽位序 = 战斗切人顺序）；
   /// - 「开始战斗」→ 写入 BattleTeamSession（实体化数据管道）→ 进入战斗场景；
   /// - 「返回主场景」→ 回 BattleTeamSession.MainSceneName。
   /// </summary>
   public class BattleTeamSelectPanel : UIPanel
   {
      [SerializeField] private Button[] roleButtons;
      [SerializeField] private Button[] weaponButtons;
      [SerializeField] private Text summaryText;
      [SerializeField] private Text rosterText;
      [SerializeField] private Button joinButton;
      [SerializeField] private Button removeButton;
      [SerializeField] private Button clearButton;
      [SerializeField] private Button upButton;
      [SerializeField] private Button downButton;
      [SerializeField] private Button startButton;
      [SerializeField] private Button backButton;

      private BattleTeamCatalog mCatalog;
      private int mSelRole;
      private int mSelWeapon;
      private int mSelSlot;
      private bool mWired;
      private readonly List<BattleTeamMemberDraft> mTeam = new(BattleTeamSession.MaxTeamSize);

      private int RoleCount => mCatalog != null && mCatalog.Roles != null ? mCatalog.Roles.Length : 0;
      private int WeaponCount => mCatalog != null && mCatalog.Weapons != null ? mCatalog.Weapons.Length : 0;

      protected override void OnInit(IUIData uiData)
      {
         Wire();
      }

      protected override void OnOpen(IUIData uiData)
      {
         mCatalog = (uiData as BattleTeamSelectPanelData)?.Catalog;
         if (mCatalog == null)
         {
            Debug.LogWarning("[T3.1A] 选队面板缺少编目数据（BattleTeamCatalog），请检查场景 Bootstrap 引用。");
         }

         RefreshAll();
      }

      protected override void OnClose()
      {
      }

      // ---------------- 交互 ----------------

      private void Wire()
      {
         if (mWired) return;
         mWired = true;

         if (joinButton) joinButton.onClick.AddListener(AddPending);
         if (removeButton) removeButton.onClick.AddListener(RemoveSelectedSlot);
         if (clearButton) clearButton.onClick.AddListener(ClearTeam);
         if (upButton) upButton.onClick.AddListener(() => SwapSlot(mSelSlot - 1));
         if (downButton) downButton.onClick.AddListener(() => SwapSlot(mSelSlot + 1));
         if (startButton) startButton.onClick.AddListener(StartBattle);
         if (backButton) backButton.onClick.AddListener(BackToMain);

         WireSelectionButtons(roleButtons, isRole: true);
         WireSelectionButtons(weaponButtons, isRole: false);
      }

      private void WireSelectionButtons(Button[] buttons, bool isRole)
      {
         if (buttons == null) return;
         for (var i = 0; i < buttons.Length; i++)
         {
            var index = i; // 捕获闭包
            if (buttons[index] == null) continue;
            if (isRole)
            {
               buttons[index].onClick.AddListener(() => SelectRole(index));
            }
            else
            {
               buttons[index].onClick.AddListener(() => SelectWeapon(index));
            }
         }
      }

      private void SelectRole(int index)
      {
         if (index >= 0 && index < RoleCount)
         {
            mSelRole = index;
            RefreshSummary();
         }
      }

      private void SelectWeapon(int index)
      {
         if (index >= 0 && index < WeaponCount)
         {
            mSelWeapon = index;
            RefreshSummary();
         }
      }

      private void AddPending()
      {
         if (mCatalog == null || RoleCount == 0 || WeaponCount == 0)
         {
            ShowMessage("编目为空，无法加入。");
            return;
         }

         if (mTeam.Count >= BattleTeamSession.MaxTeamSize)
         {
            ShowMessage($"队伍已满（{BattleTeamSession.MaxTeamSize} 人）。");
            return;
         }

         mTeam.Add(new BattleTeamMemberDraft(mCatalog.Roles[mSelRole], mCatalog.Weapons[mSelWeapon]));
         mSelSlot = mTeam.Count - 1;
         RefreshRoster();
      }

      private void RemoveSelectedSlot()
      {
         if (mTeam.Count == 0 || mSelSlot < 0 || mSelSlot >= mTeam.Count) return;
         mTeam.RemoveAt(mSelSlot);
         mSelSlot = Mathf.Clamp(mSelSlot, 0, mTeam.Count - 1);
         RefreshRoster();
      }

      private void ClearTeam()
      {
         mTeam.Clear();
         mSelSlot = 0;
         RefreshRoster();
      }

      private void SwapSlot(int with)
      {
         if (with < 0 || with >= mTeam.Count || mTeam.Count < 2 || with == mSelSlot) return;
         (mTeam[mSelSlot], mTeam[with]) = (mTeam[with], mTeam[mSelSlot]);
         mSelSlot = with;
         RefreshRoster();
      }

      private void StartBattle()
      {
         if (mTeam.Count == 0)
         {
            ShowMessage("请先加入至少 1 名角色再开始。");
            return;
         }

         BattleTeamSession.Write(mTeam);
         Debug.Log($"[T3.1A] 编队确认：{mTeam.Count} 人出战，已写入 BattleTeamSession。");
         if (UIKit.GetPanel<BattleTeamSelectPanel>() != null)
         {
            UIKit.ClosePanel<BattleTeamSelectPanel>();
         }

         SceneManager.LoadScene(BattleTeamSession.BattleSceneName);
      }

      private void BackToMain()
      {
         if (UIKit.GetPanel<BattleTeamSelectPanel>() != null)
         {
            UIKit.ClosePanel<BattleTeamSelectPanel>();
         }

         SceneManager.LoadScene(BattleTeamSession.MainSceneName);
      }

      // ---------------- 刷新 ----------------

      private void RefreshAll()
      {
         RefreshButtons(roleButtons, isRole: true);
         RefreshButtons(weaponButtons, isRole: false);
         mSelRole = Mathf.Clamp(mSelRole, 0, Mathf.Max(0, RoleCount - 1));
         mSelWeapon = Mathf.Clamp(mSelWeapon, 0, Mathf.Max(0, WeaponCount - 1));
         RefreshSummary();
         RefreshRoster();
      }

      private void RefreshButtons(Button[] buttons, bool isRole)
      {
         if (buttons == null) return;
         for (var i = 0; i < buttons.Length; i++)
         {
            if (buttons[i] == null) continue;
            var label = buttons[i].GetComponentInChildren<Text>(true);
            if (label == null) continue;

            if (isRole)
            {
               var role = GetRole(i);
               label.text = role != null ? $"{role.itemName}" : "空";
            }
            else
            {
               var weapon = GetWeapon(i);
               label.text = weapon != null ? weapon.itemName : "空";
            }
         }
      }

      private void RefreshSummary()
      {
         if (!summaryText) return;
         var role = GetRole(mSelRole);
         var weapon = GetWeapon(mSelWeapon);
         summaryText.text = role != null && weapon != null
            ? $"待加入：{role.itemName}（{role.damageElement}）× {weapon.itemName}"
            : "待加入：-";
      }

      private void RefreshRoster()
      {
         if (!rosterText) return;

         var sb = new System.Text.StringBuilder();
         sb.AppendLine($"出战队伍 {mTeam.Count}/{BattleTeamSession.MaxTeamSize}（槽位序=切人顺序）：");
         if (mTeam.Count == 0)
         {
            sb.AppendLine("  （空）");
         }
         else
         {
            for (var i = 0; i < mTeam.Count; i++)
            {
               var member = mTeam[i];
               var marker = i == mSelSlot ? "▶" : "  ";
               var roleName = member.Role != null ? member.Role.itemName : "?";
               var weaponName = member.Weapon != null ? member.Weapon.itemName : "?";
               sb.AppendLine($"{marker}[{i + 1}] {roleName} × {weaponName}");
            }
         }

         rosterText.text = sb.ToString();
      }

      private void ShowMessage(string message)
      {
         if (summaryText)
         {
            summaryText.text = message;
         }
      }

      private RoleItem_Data GetRole(int index)
      {
         if (mCatalog == null || mCatalog.Roles == null || index < 0 || index >= mCatalog.Roles.Length) return null;
         return mCatalog.Roles[index];
      }

      private WeaponItem_Data GetWeapon(int index)
      {
         if (mCatalog == null || mCatalog.Weapons == null || index < 0 || index >= mCatalog.Weapons.Length) return null;
         return mCatalog.Weapons[index];
      }
   }
}
