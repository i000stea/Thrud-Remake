using System;
using System.Collections.Generic;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 出战成员草稿（实体化之前，方案 B：跨场景/资源生命周期安全的快照）。
   /// 只存"角色 itemID + 武器 itemID"与展示名（写入口从 SO 摘取，之后**不持有任何 Unity 对象引用**）；
   /// 由 <see cref="BattleTeamRuntimeBuilder"/> 在战斗场景内经出战编目
   /// <see cref="BattleTeamCatalog"/> 按 itemID 重解析出 SO，再实体化为 <see cref="BattleRoleRuntimeData"/>。
   /// 背景：旧实现直接持有角色/武器 SO 引用，编队面板随 AB 卸载或编辑器跨 Play 会话重载时
   /// 对象被销毁，静态会话里的引用悬垂成"伪 null"，战斗场景装配即失败（见 09 会话交接）。
   /// 只存 id 后草稿与对象生命周期解耦，仅在写入口短暂依赖 SO。
   /// 未来"试用队伍 / 角色养成"升级：在此草稿上追加等级/成长快照/试用标记等字段。
   /// </summary>
   [Serializable]
   public sealed class BattleTeamMemberDraft
   {
      public int RoleItemId;
      public int WeaponItemId;

      /// <summary>展示名（写入口从 SO 快照，仅供日志核对，不参与寻址）。</summary>
      public string RoleDisplayName = string.Empty;
      public string WeaponDisplayName = string.Empty;

      public BattleTeamMemberDraft()
      {
      }

      public BattleTeamMemberDraft(RoleItem_Data role, WeaponItem_Data weapon)
      {
         RoleItemId = role != null ? role.itemID : 0;
         WeaponItemId = weapon != null ? weapon.itemID : 0;
         RoleDisplayName = role != null ? role.name : string.Empty;
         WeaponDisplayName = weapon != null ? weapon.name : string.Empty;
      }

      /// <summary>id 快照完整（都大于 0）才算有效；写入时 SO 尚在故天然成立，且此后不随对象销毁失效。</summary>
      public bool IsValid => RoleItemId > 0 && WeaponItemId > 0;
   }

   /// <summary>
   /// 出战编队静态临时会话（T3.1A 轻量数据管道；方案 B 起只存 itemID 快照，见 <see cref="BattleTeamMemberDraft"/>）。
   /// 选队场景写入 → 战斗场景内经编目重解析实体化；仅内存，领域重载即清空回落默认。
   /// 场景名以字符串常量统一（对齐 SceneFlow 决策 D25：场景 key=场景名）。
   /// </summary>
   public static class BattleTeamSession
   {  
      /// <summary>0ConfigScene 主入口（无来源时的兜底返回目标，v1.0 A3）。</summary>
      public const string ConfigSceneName = "0ConfigScene";

      /// <summary>
      /// 出战编队入口场景名（v1.3 起主路径 = 2MainScene 的「选关 → 编队」覆盖层；旧 `M1_TeamSelect` 场景已删除）。
      /// 仅用于错误提示指引；实际进出场景由 <c>BattleSceneGate</c> + <see cref="ReturnSceneName"/> 决定。
      /// </summary>
      public const string FormationEntrySceneName = "2MainScene";

      /// <summary>灰盒战斗场景名（选队确认后加载；后续关卡接入可换）。</summary>
      public const string BattleSceneName = "BattleGraybox";

      /// <summary>编队人数上限（=切人上限）。</summary>
      public const int MaxTeamSize = 4;

      private static string sSourceSceneName;

      /// <summary>
      /// 来源场景（v1.0 A3）：由流程入口/未来 ConfigScene battle 启动类写入。
      /// 暂停/结算「退出」与预编队「返回」统一回到该场景。
      /// </summary>
      public static string SourceSceneName
      {
         get => sSourceSceneName;
         set => sSourceSceneName = value;
      }

      /// <summary>实际返回目标：有来源回来源，否则回 0ConfigScene。</summary>
      public static string ReturnSceneName =>
         string.IsNullOrEmpty(sSourceSceneName) ? ConfigSceneName : sSourceSceneName;

      private static readonly List<BattleTeamMemberDraft> sMembers = new(MaxTeamSize);

      /// <summary>本次出战编队（槽位序=切人顺序）。</summary>
      public static IReadOnlyList<BattleTeamMemberDraft> Members => sMembers;

      public static int Count => sMembers.Count;

      /// <summary>是否存在有效选队（正式构建以此判定是否允许直接进战斗）。</summary>
      public static bool HasSelection => sMembers.Count > 0;

      /// <summary>写入本次编队（自动截断到上限并过滤不完整成员）。</summary>
      public static void Write(IEnumerable<BattleTeamMemberDraft> members)
      {
         sMembers.Clear();
         if (members == null)
         {
            return;
         }

         foreach (var member in members)
         {
            if (member == null || !member.IsValid)
            {
               continue;
            }

            if (sMembers.Count >= MaxTeamSize)
            {
               break;
            }

            sMembers.Add(member);
         }

         // 打印本次写入结果（供「选关 → 选队 → 出战」链路联调）
         var slots = new string[sMembers.Count];
         for (var i = 0; i < sMembers.Count; i++)
         {
            var member = sMembers[i];
            var roleTag = string.IsNullOrEmpty(member.RoleDisplayName)
               ? member.RoleItemId.ToString()
               : member.RoleDisplayName;
            var weaponTag = string.IsNullOrEmpty(member.WeaponDisplayName)
               ? member.WeaponItemId.ToString()
               : member.WeaponDisplayName;
            slots[i] = $"{i + 1}.{roleTag}+{weaponTag}";
         }

         Debug.Log($"[BattleTeamSession] 编队写入完成：{sMembers.Count}/{MaxTeamSize} 人" +
                   (sMembers.Count > 0 ? " -> " + string.Join("  ", slots) : "（空队伍）"));
      }

      /// <summary>清空编队（主场景/重开选队前调用）。</summary>
      public static void Clear()
      {
         sMembers.Clear();
      }
   }
}
