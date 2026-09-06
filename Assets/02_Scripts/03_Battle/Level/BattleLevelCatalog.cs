using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 逻辑关卡目录（T3.1C Runner/关卡选择页数据源）。
   /// 持有关卡列表 + 装配所需资产（敌人目录、玩家整包装配 prefab，D6）。
   /// 供：
   /// - <see cref="BattleFlowRunner"/>：正式路径按 <see cref="BattleLevelSession"/> 选中的关卡装配；
   ///   编辑器直连按「引用该场景的首个关卡」解析默认关卡（D12）；
   /// - LevelSelectPanel（D10）：展示 3 预设关列表（顺序 = 展示顺序）。
   /// 资产由 Thrud/Battle/Level Data 生成器统一创建/刷新，无需手搭引用。
   /// </summary>
   [CreateAssetMenu(fileName = "BattleLevelCatalog", menuName = "Thrud/Battle/Level Catalog")]
   public class BattleLevelCatalog : ScriptableObject
   {
      [Header("装配资产（D6 整包）")]
      [Tooltip("敌人类型目录：Runner 装配 LevelDirector 时解析敌人 prefab。")]
      public EnemyCatalog EnemyCatalog;

      [Tooltip("玩家整包装配 prefab：Battle_PlayerControl + 数据/配置/HUD 引用，Runner 按关卡出生点实例化。")]
      public GameObject PlayerRigPrefab;

      [Header("关卡列表（选择页展示顺序）")]
      [Tooltip("逻辑关卡列表（同美术场景可挂多关，1:N 复用 D8）。")]
      public BattleLevelData[] Levels;

      /// <summary>按关卡 id 查找（入口链路/流程缓存用）。</summary>
      public BattleLevelData FindByLevelId(string levelId)
      {
         if (string.IsNullOrEmpty(levelId) || Levels == null) return null;

         for (var i = 0; i < Levels.Length; i++)
         {
            var level = Levels[i];
            if (level != null && level.LevelId == levelId)
            {
               return level;
            }
         }

         return null;
      }

      /// <summary>
      /// D12：引用该美术场景的首个关卡（编辑器直连默认关卡解析约定）。
      /// </summary>
      public BattleLevelData FindFirstByArtScene(string artSceneName)
      {
         if (string.IsNullOrEmpty(artSceneName) || Levels == null) return null;

         for (var i = 0; i < Levels.Length; i++)
         {
            var level = Levels[i];
            if (level != null && level.ArtSceneName == artSceneName)
            {
               return level;
            }
         }

         return null;
      }
   }
}
