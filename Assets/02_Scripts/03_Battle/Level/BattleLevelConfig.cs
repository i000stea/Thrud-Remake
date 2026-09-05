using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T3.1 关卡定义（数据优先，ScriptableObject）。
   /// 灰盒阶段由 BattleLevelFlow 持有"运行时兜底默认值"即可运行（场景零改动）；
   /// 真实资产（每个灰盒关卡一个）在 T3.2 关卡模式接入时按本类型创建并绑定，
   /// 波次内容本身复用 T2.2 BattleStageAsset（BattleStageDirector 引用）组织。
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
