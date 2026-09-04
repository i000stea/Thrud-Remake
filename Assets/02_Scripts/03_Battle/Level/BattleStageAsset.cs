using TeaFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.2 区域波次配置资产（数据优先，单一配置源）。
   /// 结构：整场限时 → 站点[位置+到达半径+批次表] → 批次[击杀比例+刷怪条目]。
   /// 敌人以"字符串 id"引用（字段标注 <see cref="EnemyIdAttribute"/>，
   /// Inspector 重绘为 EnemyCatalog 提供的下拉），底层为字符串，便于热更新增敌人。
   /// 站点位置为水平 (x,z)，刷怪位置按"相对站点方位角+距离+高度"自动换算——改配置只需在本表调整。
   /// </summary>
   [CreateAssetMenu(fileName = "BattleStageConfig", menuName = "Thrud/Battle/Stage Config")]
   public class BattleStageAsset : ScriptableObject
   {
      /// <summary>资产结构版本（向导据此重建旧资产，避免字段变更后旧资产缺数据）。</summary>
      public const int CurrentVersion = 5;

      [Tooltip("资产结构版本（勿手改；版本过低时运行 Create Default Stage Assets 向导会重建）。")]
      public int Version = CurrentVersion;

      [Tooltip("整场限时（秒）；0 = 不限时。")]
      public int TimeLimitSeconds = 180;

      [Tooltip("站点序列（按顺序激活）。")]
      public BattleSiteRow[] Sites;
   }

   /// <summary>站点行：位置 + 到达半径 + 批次表。</summary>
   [System.Serializable]
   public class BattleSiteRow
   {
      [Tooltip("备注（便于识别该站）。")]
      public string Note;

      [Tooltip("目标点水平位置 (x, z)。")]
      public Vector2 Position = Vector2.zero;

      [Tooltip("玩家进入该半径（水平）后开始刷首批。")]
      public float ArriveRadius = 8f;

      [Tooltip("批次表（顺序执行；首批玩家到达触发，其余按击杀比例触发）。")]
      public BattleBatchRow[] Batches;
   }

   /// <summary>批次行：在场击杀比例 + 本批刷怪条目。</summary>
   [System.Serializable]
   public class BattleBatchRow
   {
      [Tooltip("备注。")]
      public string Note;

      [Tooltip("触发条件：在场击杀比例（如 0.8 = 清掉 80% 刷下一批）。")]
      [Range(0.05f, 1f)]
      public float KillRatio = 0.8f;

      [Tooltip("本批刷怪条目（每条目=一种敌人+数量+相对站点位置）。")]
      public BattleSpawnRow[] Spawns;
   }

   /// <summary>刷怪条目：敌人类型（字符串 id，下拉重绘）+ 数量 + 相对站点方位角/距离/高度 + 敌人元素。</summary>
   [System.Serializable]
   public class BattleSpawnRow
   {
      [Tooltip("敌人类型（来自 EnemyCatalog 下拉；底层存字符串）。")]
      [EnemyId]
      public string EnemyId;

      [Tooltip("敌人自身元素（T2.3 克制演示用；none=无元素按类型色）。")]
      public DamageElement Element = DamageElement.none;

      [Tooltip("数量。")]
      public int Count = 1;

      [Tooltip("相对站点的方位角（度）：0 = 站点正前方(+Z)，正值为顺时针偏转。")]
      public float AngleDeg = 0f;

      [Tooltip("相对站点的水平距离。")]
      public float Distance = 6f;

      [Tooltip("生成高度（世界 y）。")]
      public float Y = 1f;

      [Tooltip("多个单位在生成点周围的随机散布半径。")]
      public float Scatter = 1.5f;
   }
}
