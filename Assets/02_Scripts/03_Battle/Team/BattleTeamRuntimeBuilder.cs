using System.Collections.Generic;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 编队实体化器（T3.1A + 方案 B）。
   /// 由选队草稿（<see cref="BattleTeamMemberDraft"/>，仅存 itemID 快照）在战斗场景内经出战编目
   /// <see cref="BattleTeamCatalog"/> 重解析出角色/武器 SO，再实体化为 <see cref="BattleRoleRuntimeData"/>：
   /// 逐成员实例化角色/武器并装配武器模式（复用现有构造）。
   /// 选队写入与战斗解析之间不再传递 Unity 对象引用，草稿不随编目 AB 卸载 / 跨 Play 会话重载而失效；
   /// 该"实体化"步骤即用户要求的"选队后将属性实体化一份"，
   /// 便于后续"试用队伍"（同模板不同数值）与"角色养成"（草稿附加成长快照后实体化）。
   /// </summary>
   public static class BattleTeamRuntimeBuilder
   {
      public static BattleRoleRuntimeData[] Build(IReadOnlyList<BattleTeamMemberDraft> members, BattleTeamCatalog teamCatalog)
      {
         var results = new List<BattleRoleRuntimeData>(members?.Count ?? 0);
         if (members == null)
         {
            return results.ToArray();
         }

         if (teamCatalog == null)
         {
            if (members.Count > 0)
            {
               Debug.LogWarning("[Team] 出战编目为 null，无法把成员草稿重解析为 SO，整队跳过。");
            }

            return results.ToArray();
         }

         var slot = 0;
         foreach (var member in members)
         {
            slot++;
            if (member == null)
            {
               Debug.LogWarning($"[Team] 第 {slot} 位出战成员引用为 null（草稿列表中出现空位），已跳过。");
               continue;
            }

            if (!member.IsValid)
            {
               Debug.LogWarning($"[Team] 第 {slot} 位出战成员快照不完整：RoleItemId={member.RoleItemId}，WeaponItemId={member.WeaponItemId}，已跳过。");
               continue;
            }

            var role = teamCatalog.FindRoleById(member.RoleItemId);
            var weapon = teamCatalog.FindWeaponById(member.WeaponItemId);
            if (role == null || weapon == null)
            {
               Debug.LogWarning($"[Team] 第 {slot} 位出战成员未从编目解析到完整 SO：角色「{member.RoleDisplayName}」" +
                                $"({member.RoleItemId}) 命中={role != null}，武器「{member.WeaponDisplayName}」" +
                                $"({member.WeaponItemId}) 命中={weapon != null}，目录缺项已跳过。");
               continue;
            }

            results.Add(new BattleRoleRuntimeData(role, weapon));
         }

         // 打印实体化汇总，便于与选队会话（BattleTeamSession.Write）核对
         Debug.Log($"[Team] 编队实体化完成：成功 {results.Count} / 总数 {members.Count}，槽位 {slot}。");

         return results.ToArray();
      }
   }
}
