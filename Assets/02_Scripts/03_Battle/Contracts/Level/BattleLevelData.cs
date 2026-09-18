using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 关卡模式（T3.2 起：清剿/护送/防守 三种测试玩法；本卡先预留枚举与字段）。
   /// T3.1C 阶段三种模式仍沿用现波次规则，仅作为入口占位区分。
   /// </summary>
   public enum BattleLevelMode
   {
      Clear = 0,
      Escort = 1,
      Defend = 2,
   }

   /// <summary>
   /// 逻辑关卡数据（T3.1C 薄场景化）。
   /// 场景=纯美术素材（Art Scene），关卡的一切布局与玩法数据都在本 SO 中：
   /// 玩家出生点、引用美术场景名、**波次/刷怪配置（原 BattleStageAsset 已并入本类）**等。
   /// 同一美术场景可被多个 BattleLevelData 复用（1:N，D8/D9）。
   /// 波次结构：整场限时 → 站点[位置+到达半径+批次表] → 批次[击杀比例+刷怪条目]；
   /// 敌人以"字符串 id"引用（<see cref="EnemyIdAttribute"/> 下拉），站点位置为水平 (x,z)。
   /// </summary>
   [CreateAssetMenu(fileName = "BattleLevel", menuName = "Thrud/Battle/Level Data")]
   public class BattleLevelData : ScriptableObject
   {
      // 文本信息 ================================================
      [Header("标识")]
      [Tooltip("关卡 id（跨场景流程/缓存用 key）。")]
      public string LevelId;

      [Tooltip("展示名（关卡选择页显示）。")]
      public string DisplayName;

      [TextArea]
      [Tooltip("关卡介绍（关卡选择页显示）。")]
      public string Intro;


      // 美术信息 ================================================
      [Header("归属（D8：场景=纯美术，多关卡复用）")]
      [Tooltip("美术场景名（本 SO 在该场景的坐标系内定义布局；须加入 Build Settings）。")]
      public string ArtSceneName;


      // 玩家信息 ================================================
      [Tooltip("玩家出生点（ArtScene 世界坐标系，基准=美术场景原点 +Y 上，D9）。")]
      public Vector3 PlayerSpawnPosition = new Vector3(0f, 2f, 0f);

      [Tooltip("玩家出生朝向（欧拉角，ArtScene 坐标系）。")]
      public Vector3 PlayerSpawnEuler = Vector3.zero;

      // 关卡信息 ================================================
      [Header("模式（T3.2 起生效；本卡占位）")]
      [Tooltip("模式规则类型（T3.2 三种测试玩法接入后驱动不同 LevelDirector 规则）。")]
      public BattleLevelMode Mode;

      // 敌人信息 ================================================
      [Header("波次/刷怪（原 BattleStageAsset 已并入，改关卡只编辑本表）")]
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
