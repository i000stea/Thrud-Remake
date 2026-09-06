using System;
using System.Collections.Generic;
using TeaGame;

namespace TeaGame
{
   /// <summary>
   /// 出战成员草稿（实体化之前）。
   /// 仅持有"角色 SO + 武器 SO"引用对，不实例化任何 Unity 对象；
   /// 由 <see cref="BattleTeamRuntimeBuilder"/> 在战斗场景内实体化为 <see cref="BattleRoleRuntimeData"/>。
   /// 未来"试用队伍 / 角色养成"升级：在此草稿上追加等级/成长快照/试用标记等字段。
   /// </summary>
   [Serializable]
   public sealed class BattleTeamMemberDraft
   {
      public RoleItem_Data Role;
      public WeaponItem_Data Weapon;

      public BattleTeamMemberDraft()
      {
      }

      public BattleTeamMemberDraft(RoleItem_Data role, WeaponItem_Data weapon)
      {
         Role = role;
         Weapon = weapon;
      }

      public bool IsValid => Role != null && Weapon != null;
   }

   /// <summary>
   /// 出战编队静态临时会话（T3.1A 轻量数据管道）。
   /// 选队场景写入 → 战斗场景只读实体化；仅内存，领域重载即清空回落默认。
   /// 场景名以字符串常量统一（对齐 SceneFlow 决策 D25：场景 key=场景名）。
   /// </summary>
   public static class BattleTeamSession
   {
      /// <summary>极简主场景名（历史 T3.1A-4b：M1_MainHub 已随 v1.0 废弃，勿再用于跳转）。</summary>
      public const string MainSceneName = "M1_MainHub";

      /// <summary>0ConfigScene 主入口（无来源时的兜底返回目标，v1.0 A3）。</summary>
      public const string ConfigSceneName = "0ConfigScene";

      /// <summary>出战编队场景名（主场景入口进入）。</summary>
      public const string TeamSelectSceneName = "M1_TeamSelect";

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
      }

      /// <summary>清空编队（主场景/重开选队前调用）。</summary>
      public static void Clear()
      {
         sMembers.Clear();
      }
   }
}
