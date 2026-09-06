using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T3.1C battle 场景固定引导（对象名约定：battle_runtime）。
   /// 用户决策（2026-09-06）：战斗场景不追求"纯美术零逻辑"——角色控制 rig、飘字服务、
   /// 敌人管理（波次 Director）等**战斗固定固件直接放场景**，由本宿主完成框架初始化与
   /// 「从关卡数据初始化场景配置」：
   /// 1. Awake：确保战斗架构创建（BattleArchitecture.Interface，幂等）；
   /// 2. 解析本场关卡：BattleLevelSession.Selected（D11 选关缓存）优先，
   ///    否则按 D12 回落「首个引用本场景的 BattleLevelData」（关卡目录中查）；
   /// 3. 把关卡数据应用到场景固件：rig 移到 PlayerSpawnPosition/Euler（D9 坐标系）、
   ///    波次 Director 注入 BattleLevelData + EnemyCatalog（替代旧场景 config 资产引用）。
   /// 场景对象清单与放置/校验由编辑器工具 Thrud/Battle/T3.1C 负责（检查并放置，幂等）。
   /// </summary>
   [DisallowMultipleComponent]
   public sealed class BattleSceneRuntimeHost : MonoBehaviour
   {
      [Header("装配资产（编辑器工具检查并放置时回填）")]
      [Tooltip("关卡目录：提供 EnemyCatalog 与 D12 直连默认关解析。")]
      [SerializeField] private BattleLevelCatalog catalog;

      [Tooltip("场景固定 rig（Battle_PlayerControl.prefab 实例，角色控制 + 战斗 HUD）。")]
      [SerializeField] private Battle_PlayerControl playerControl;

      [Tooltip("场景固定敌人管理（波次/目标点/刷怪 Director，数据由本宿主注入）。")]
      [SerializeField] private BattleStageDirector stageDirector;

      [Tooltip("启动后按关卡数据应用玩家出生点/朝向（D9：数据定义出生点，场景不硬编）。")]
      [SerializeField] private bool applyPlayerSpawn = true;

      private void Awake()
      {
         // 框架初始化：确保战斗架构创建（rig.Awake 同样会触发，幂等）
         _ = BattleArchitecture.Interface;

         var level = ResolveLevel();
         if (level == null)
         {
            Debug.LogWarning(
               $"[battle_runtime] 场景「{gameObject.scene.name}」未解析到关卡数据" +
               "（关卡目录未登记本场景或目录缺失），battle 场景仅保底运行（无波次数据）。");
            return;
         }

         if (applyPlayerSpawn && playerControl != null)
         {
            playerControl.transform.SetPositionAndRotation(
               level.PlayerSpawnPosition,
               Quaternion.Euler(level.PlayerSpawnEuler));
         }

         if (stageDirector != null)
         {
            stageDirector.Setup(level, catalog != null ? catalog.EnemyCatalog : null);
         }
         else
         {
            Debug.LogWarning("[battle_runtime] 未配置 BattleStageDirector（场景固件缺失：敌人/波次不会生成）。" +
                             "请用编辑器菜单 Thrud/Battle/T3.1C/1. 检查并放置战斗固定固件。");
         }

         var siteCount = level.Sites == null ? 0 : level.Sites.Length;
         Debug.Log($"[battle_runtime] 关卡 {level.LevelId}「{level.DisplayName}」装配完成：" +
                   $"出生 {level.PlayerSpawnPosition}（朝向 {level.PlayerSpawnEuler}），站点 {siteCount} 个，限时 {level.TimeLimitSeconds}s。");
      }

      /// <summary>
      /// 解析本场关卡：BattleLevelSession 选关缓存（须指向本场景）优先；否则 D12 直连默认。
      /// </summary>
      private BattleLevelData ResolveLevel()
      {
         var selected = BattleLevelSession.Selected;
         if (selected != null)
         {
            if (selected.ArtSceneName == gameObject.scene.name)
            {
               return selected;
            }

            Debug.LogWarning($"[battle_runtime] 会话缓存关卡 {selected.LevelId} 属于场景 {selected.ArtSceneName}，" +
                             $"与本场景 {gameObject.scene.name} 不符，回落直连默认（D12）。");
         }

         if (catalog != null)
         {
            var fallback = catalog.FindFirstByArtScene(gameObject.scene.name);
            if (fallback != null)
            {
               return fallback;
            }
         }

         return null;
      }
   }
}
