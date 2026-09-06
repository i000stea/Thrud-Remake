using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 关卡会话（T3.1C D11：关卡选择 → 缓存 → 编队「开始出战」→ Runner 装配 之间跨面板/跨场景读取）。
   /// 与 <see cref="BattleTeamSession"/> 同风格：静态内存缓存，重选即覆盖，领域重载回落默认。
   /// 无选择时的默认关卡由 Runner 按「引用当前美术场景的首个 BattleLevelData」约定（D12）解析。
   /// </summary>
   public static class BattleLevelSession
   {
      private static BattleLevelData sSelected;

      /// <summary>当前选中的关卡数据（未选择时返回 null）。</summary>
      public static BattleLevelData Selected => sSelected;

      /// <summary>是否存在已缓存的关卡选择。</summary>
      public static bool HasSelection => sSelected != null;

      /// <summary>写入关卡选择（重选即覆盖；null 等价于 Clear）。</summary>
      public static void Select(BattleLevelData level)
      {
         sSelected = level;
         if (level != null)
         {
            Debug.Log($"[BattleLevelSession] 已缓存关卡：{level.LevelId}（{level.DisplayName} → {level.ArtSceneName}）。");
         }
      }

      /// <summary>清空关卡选择。</summary>
      public static void Clear()
      {
         sSelected = null;
      }
   }
}
