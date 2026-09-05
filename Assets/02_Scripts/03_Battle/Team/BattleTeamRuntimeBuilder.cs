using System.Collections.Generic;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 编队实体化器（T3.1A）。
   /// 由选队草稿（<see cref="BattleTeamMemberDraft"/>）在战斗场景内实体化为
   /// <see cref="BattleRoleRuntimeData"/>：逐成员实例化角色/武器并装配武器模式（复用现有构造）。
   /// 这一"实体化"步骤即用户要求的"选队后将属性实体化一份"，
   /// 便于后续"试用队伍"（同模板不同数值）与"角色养成"（草稿附加成长快照后实体化）。
   /// </summary>
   public static class BattleTeamRuntimeBuilder
   {
      public static BattleRoleRuntimeData[] Build(IReadOnlyList<BattleTeamMemberDraft> members)
      {
         var results = new List<BattleRoleRuntimeData>(members?.Count ?? 0);
         if (members == null)
         {
            return results.ToArray();
         }

         var slot = 0;
         foreach (var member in members)
         {
            slot++;
            if (member == null || !member.IsValid)
            {
               Debug.LogWarning($"[Team] 第 {slot} 位出战成员不完整（缺角色或武器 SO），已跳过。");
               continue;
            }

            results.Add(new BattleRoleRuntimeData(member.Role, member.Weapon));
         }

         return results.ToArray();
      }
   }
}
