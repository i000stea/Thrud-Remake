using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T3.1 关卡定义（数据优先，ScriptableObject）。
   /// 灰盒阶段由 BattleLevelFlow 持有"运行时兜底默认值"即可运行（场景零改动）；
   /// 真实关卡数据（每个逻辑关卡一个）已由 BattleLevelData(SO) 承载（T3.1C 起，
   /// 含出生点/波次/限时等；本类仅保留流程侧标识字段，T3.2 统一时并入）。
   /// </summary>
   [CreateAssetMenu(menuName = "TeaGame/Battle/Level Config", fileName = "LevelConfig_")]
   public sealed class BattleLevelConfig : ScriptableObject
   {
      [Header("关卡标识")]
      public string levelId = "demo";

      [Header("展示")]
      public string displayName = "灰盒演示";

      [Tooltip("胜利后是否允许直接重开（灰盒默认允许）。")]
      public bool allowRestart = true;
   }
}
