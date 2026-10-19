using UnityEditor;
using UnityEngine;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// T3.1C 步骤2：生成三份预设逻辑关卡数据（幂等）。
   /// 菜单 Thrud/Battle/Level Data/Create Preset 01-03。
   /// 三预设 = 清剿/护送/防守 三种测试入口占位（T3.2 换模式规则后即为其正式关卡），
   /// 本卡共用同一美术场景 BattleGraybox 与同一份波次内容。
   /// 波次内容已并入 BattleLevelData 内嵌拷贝：生成时从「首个含波次内容的既有预设」深拷贝独立副本，
   /// 之后各关卡改表互不影响；不持有任何对旧 StageConfig 资产的引用（过渡资产已删除，步骤6）。
   /// </summary>
   public static class BattleLevelDataSetup
   {
      private const string AssetFolder = "Assets/Data/BattleLevels";
      private const string ArtSceneName = "scene_0004_battle_graybox";
      private const string StageConfigPath = "Assets/04_Prefabs/Enemy/StageConfig_Demo.asset";
      private const string EnemyCatalogPath = "Assets/04_Prefabs/Enemy/EnemyCatalog.asset";
      private const string PlayerRigPrefabPath = "Assets/04_Prefabs/Battle_PlayerControl.prefab";
      private static readonly Vector3 PlayerSpawn = new Vector3(0f, 2f, 0f);

      private static readonly PresetDef[] Presets =
      {
         new PresetDef(
            "level_01",
            "测试关 01 · 清剿（占位）",
            "三种测试玩法之一：清剿。\nT3.1C 阶段沿用现波次规则，正式清剿模式规则于 T3.2 接入。",
            BattleLevelMode.Clear),
         new PresetDef(
            "level_02",
            "测试关 02 · 护送（占位）",
            "三种测试玩法之一：护送。\nT3.1C 阶段沿用现波次规则，正式护送模式规则于 T3.2 接入。",
            BattleLevelMode.Escort),
         new PresetDef(
            "level_03",
            "测试关 03 · 防守（占位）",
            "三种测试玩法之一：防守。\nT3.1C 阶段沿用现波次规则，正式防守模式规则于 T3.2 接入。",
            BattleLevelMode.Defend),
      };

      private sealed class PresetDef
      {
         public readonly string LevelId;
         public readonly string DisplayName;
         public readonly string Intro;
         public readonly BattleLevelMode Mode;

         public PresetDef(string levelId, string displayName, string intro, BattleLevelMode mode)
         {
            LevelId = levelId;
            DisplayName = displayName;
            Intro = intro;
            Mode = mode;
         }
      }

      [MenuItem("Thrud/Battle/Level Data/Create Preset 01-03")]
      public static void CreatePresetLevels()
      {
         EnsureFolderExists(AssetFolder);
         var createdCount = 0;

         foreach (var def in Presets)
         {
            var path = $"{AssetFolder}/{def.LevelId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BattleLevelData>(path);
            var isNew = asset == null;
            if (isNew)
            {
               asset = ScriptableObject.CreateInstance<BattleLevelData>();
               AssetDatabase.CreateAsset(asset, path);
               createdCount++;
            }

            // 元数据字段始终同步（重跑 = 刷新名称/介绍/归属/出生点等）
            asset.LevelId = def.LevelId;
            asset.DisplayName = def.DisplayName;
            asset.Intro = def.Intro;
            asset.Mode = def.Mode;
            asset.ArtSceneName = ArtSceneName;
            asset.PlayerSpawnPosition = PlayerSpawn;
            asset.PlayerSpawnEuler = Vector3.zero;

            // 波次内容：新建或缺失内容时，从「波次来源」深拷贝一份内嵌副本
            if (asset.Sites == null || asset.Sites.Length == 0)
            {
               var source = ResolveStageSource(path);
               if (source != null && TryReadStage(source, out var limit, out var sites) && sites != null && sites.Length > 0)
               {
                  asset.TimeLimitSeconds = limit;
                  asset.Sites = CloneSites(sites);
                  Debug.Log($"[BattleLevelDataSetup] {def.LevelId}：波次内容已内嵌拷贝（限时 {limit}s，站点 {sites.Length} 个）。");
               }
               else
               {
                  Debug.LogWarning($"[BattleLevelDataSetup] {def.LevelId}：未找到波次来源，请运行『Re-copy Demo Wave into Presets』或手动填写。");
               }
            }

            EditorUtility.SetDirty(asset);
         }

         // 关卡目录同步注册（D12/Runner 装配数据源；幂等）
         CreateOrRefreshCatalog();

         AssetDatabase.SaveAssets();
         AssetDatabase.Refresh();
         Debug.Log($"[BattleLevelDataSetup] 预设就绪（新建 {createdCount} 个，其余复用）。目录：{AssetFolder}");
      }

      /// <summary>
      /// 创建/刷新关卡目录资产：登记 3 预设关 + 敌人目录 + 玩家 rig prefab（D6）。
      /// 供 Runner 统一装配与后续 LevelSelectPanel 列表数据源使用。
      /// </summary>
      [MenuItem("Thrud/Battle/Level Data/Create Level Catalog (register presets)")]
      public static void CreateOrRefreshCatalog()
      {
         EnsureFolderExists(AssetFolder);

         var path = $"{AssetFolder}/BattleLevelCatalog.asset";
         var catalog = AssetDatabase.LoadAssetAtPath<BattleLevelCatalog>(path);
         var isNew = catalog == null;
         if (isNew)
         {
            catalog = ScriptableObject.CreateInstance<BattleLevelCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
         }

         // 预设按展示顺序注册（仅收录已存在的）
         var levelList = new System.Collections.Generic.List<BattleLevelData>();
         foreach (var def in Presets)
         {
            var level = AssetDatabase.LoadAssetAtPath<BattleLevelData>($"{AssetFolder}/{def.LevelId}.asset");
            if (level != null)
            {
               levelList.Add(level);
            }
         }

         catalog.Levels = levelList.ToArray();
         catalog.EnemyCatalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(EnemyCatalogPath);
         catalog.PlayerRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPrefabPath);

         EditorUtility.SetDirty(catalog);
         AssetDatabase.SaveAssets();
         AssetDatabase.Refresh();

         var enemyOk = catalog.EnemyCatalog != null ? "已找到" : "未找到(将导致敌人无法生成,请核对路径)";
         var rigOk = catalog.PlayerRigPrefab != null ? "已找到" : "未找到(将导致玩家无法装配,请核对路径)";
         Debug.Log($"[BattleLevelDataSetup] 关卡目录就绪（{(isNew ? "新建" : "刷新")}）：登记关卡 {catalog.Levels.Length} 个；" +
                   $"EnemyCatalog {enemyOk}；PlayerRigPrefab {rigOk}。路径：{path}");
      }

      [MenuItem("Thrud/Battle/Level Data/Re-copy Demo Wave into Presets (overwrite)")]
      public static void RecopyDemoWaveIntoPresets()
      {
         EnsureFolderExists(AssetFolder);

         var source = ResolveStageSource(null);
         if (source == null || !TryReadStage(source, out var limit, out var sites) || sites == null || sites.Length == 0)
         {
            Debug.LogError("[BattleLevelDataSetup] 未找到可用的波次来源（含波次内容的既有预设）。");
            return;
         }

         foreach (var def in Presets)
         {
            var path = $"{AssetFolder}/{def.LevelId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BattleLevelData>(path);
            if (asset == null) continue;

            asset.TimeLimitSeconds = limit;
            asset.Sites = CloneSites(sites);
            EditorUtility.SetDirty(asset);
         }

         AssetDatabase.SaveAssets();
         AssetDatabase.Refresh();
         Debug.Log("[BattleLevelDataSetup] 三份预设波次内容已整体覆盖为最新源数据。");
      }

      // ---------------- 波次来源与深拷贝 ----------------

      /// <summary>
      /// 波次来源解析：首个含波次内容的既有预设（作模板；过渡期 StageConfig_Demo 已删除，步骤6）。
      /// 幂等重跑：先建关再拷贝 = 从已拷贝的预设继续传播。
      /// </summary>
      private static ScriptableObject ResolveStageSource(string excludePath)
      {
         var guids = AssetDatabase.FindAssets("t:BattleLevelData", new[] { AssetFolder });
         foreach (var guid in guids)
         {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == excludePath) continue;

            var candidate = AssetDatabase.LoadAssetAtPath<BattleLevelData>(path);
            if (candidate != null && candidate.Sites != null && candidate.Sites.Length > 0)
            {
               return candidate;
            }
         }

         return null;
      }

      /// <summary>通用读取「限时 + 站点表」：来源为 BattleLevelData（字段与行类型统一于该类）。</summary>
      private static bool TryReadStage(ScriptableObject src, out int timeLimit, out BattleSiteRow[] sites)
      {
         timeLimit = 0;
         sites = null;
         if (src == null) return false;

         var type = src.GetType();
         var limitField = type.GetField("TimeLimitSeconds");
         var sitesField = type.GetField("Sites");
         if (limitField == null || sitesField == null) return false;

         if (limitField.GetValue(src) is int limit)
         {
            timeLimit = limit;
         }

         sites = sitesField.GetValue(src) as BattleSiteRow[];
         return true;
      }

      private static BattleSiteRow[] CloneSites(BattleSiteRow[] rows)
      {
         if (rows == null) return null;

         var result = new BattleSiteRow[rows.Length];
         for (var i = 0; i < rows.Length; i++)
         {
            var row = rows[i];
            if (row == null)
            {
               result[i] = null;
               continue;
            }

            result[i] = new BattleSiteRow
            {
               Note = row.Note,
               Position = row.Position,
               ArriveRadius = row.ArriveRadius,
               Batches = CloneBatches(row.Batches),
            };
         }

         return result;
      }

      private static BattleBatchRow[] CloneBatches(BattleBatchRow[] rows)
      {
         if (rows == null) return null;

         var result = new BattleBatchRow[rows.Length];
         for (var i = 0; i < rows.Length; i++)
         {
            var row = rows[i];
            if (row == null)
            {
               result[i] = null;
               continue;
            }

            result[i] = new BattleBatchRow
            {
               Note = row.Note,
               KillRatio = row.KillRatio,
               Spawns = CloneSpawns(row.Spawns),
            };
         }

         return result;
      }

      private static BattleSpawnRow[] CloneSpawns(BattleSpawnRow[] rows)
      {
         if (rows == null) return null;

         var result = new BattleSpawnRow[rows.Length];
         for (var i = 0; i < rows.Length; i++)
         {
            var row = rows[i];
            if (row == null)
            {
               result[i] = null;
               continue;
            }

            result[i] = new BattleSpawnRow
            {
               EnemyId = row.EnemyId,
               Element = row.Element,
               Count = row.Count,
               AngleDeg = row.AngleDeg,
               Distance = row.Distance,
               Y = row.Y,
               Scatter = row.Scatter,
            };
         }

         return result;
      }

      private static void EnsureFolderExists(string folder)
      {
         if (AssetDatabase.IsValidFolder(folder)) return;

         var parent = folder.Substring(0, folder.LastIndexOf('/'));
         EnsureFolderExists(parent);

         AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
      }
   }
}
