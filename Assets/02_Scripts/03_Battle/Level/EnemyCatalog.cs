using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.2 敌人类型目录（字符串 id ↔ 敌人 prefab 映射表）。
   /// 敌人配置表（BattleStageAsset）中的条目以字符串 id 引用敌人；
   /// 运行时由目录解析出实际 prefab（含 BattleEnemyEntity，T2.1 资产）。
   /// 后续热更新新增敌人类型：只需在本目录追加一条（id + prefab），配置表即可下拉到新类型。
   /// </summary>
   [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "Thrud/Battle/Enemy Catalog")]
   public class EnemyCatalog : ScriptableObject
   {
      [Tooltip("字符串 id ↔ 敌人 prefab 映射条目。")]
      public EnemyCatalogEntry[] Entries;

      /// <summary>
      /// 按 id 解析敌人 prefab；未命中返回 null（并尝试给出提示）。
      /// </summary>
      public GameObject ResolvePrefab(string enemyId)
      {
         if (string.IsNullOrEmpty(enemyId) || Entries == null) return null;

         for (var i = 0; i < Entries.Length; i++)
         {
            var entry = Entries[i];
            if (entry == null || entry.Prefab == null) continue;
            if (entry.Id == enemyId) return entry.Prefab;
         }

         return null;
      }

      /// <summary>
      /// 取条目显示名（供日志/UI 占位；无则退回 id）。
      /// </summary>
      public static string DisplayNameOf(string enemyId)
      {
         return string.IsNullOrEmpty(enemyId) ? "（未配置）" : enemyId;
      }
   }

   /// <summary>
   /// 目录单条映射：id 显示名 + prefab。
   /// </summary>
   [System.Serializable]
   public class EnemyCatalogEntry
   {
      [Tooltip("敌人类型字符串标识（配置表通过它引用；热更新增只需保证全局唯一）。")]
      public string Id;

      [Tooltip("显示名（Inspector 下拉/日志用）。")]
      public string DisplayName;

      [Tooltip("对应敌人 prefab（含 BattleEnemyEntity）。")]
      public GameObject Prefab;
   }
}
