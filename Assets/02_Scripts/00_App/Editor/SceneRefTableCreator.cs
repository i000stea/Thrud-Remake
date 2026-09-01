using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TeaGame.App.Editor
{
   /// <summary>
   /// 场景引用表创建与维护工具。
   /// 菜单：Thrud/Setup SceneRefTable
   /// 职责：
   ///   1. 确保 Assets/Resources/SceneRefTable.asset 存在
   ///   2. 扫描 Assets/01_Scenes 按文件名映射填充条目（一次性初始化便利；
   ///      后续场景重命名/移动由 GUID 锚定，路径经 OnValidate 自动同步）
   ///   3. 同步 Build Settings，确保表内场景全部加入且 ConfigScene 置 [0]
   /// </summary>
   public static class SceneRefTableCreator
   {
      private const string TablePath = "Assets/Resources/SceneRefTable.asset";
      private const string ScenesRoot = "Assets/01_Scenes";

      [MenuItem("Thrud/Setup SceneRefTable")]
      public static void SetupSceneRefTableMenu()
      {
         var table = EnsureAsset();
         Populate(table);
         EditorUtility.SetDirty(table);
         AssetDatabase.SaveAssets();
         SyncBuildSettings(table);
         Debug.Log($"[SceneRefTableCreator] 场景引用表已就绪：{TablePath}");
      }

      /// <summary>
      /// 确保场景引用表资产存在，不存在则创建。
      /// </summary>
      public static SceneRefTable EnsureAsset()
      {
         var existing = AssetDatabase.LoadAssetAtPath<SceneRefTable>(TablePath);
         if (existing != null) return existing;

         Directory.CreateDirectory("Assets/Resources");
         var table = ScriptableObject.CreateInstance<SceneRefTable>();
         AssetDatabase.CreateAsset(table, TablePath);
         AssetDatabase.SaveAssets();
         Debug.Log($"[SceneRefTableCreator] 已创建场景引用表：{TablePath}");
         return table;
      }

      /// <summary>
      /// 按 GameScene 枚举建立固定条目列表，扫描场景目录按文件名映射填充。
      /// 未匹配的场景（自定义/新场景）条目 Asset 留空，由开发者在 Inspector 手动拖入。
      /// </summary>
      public static void Populate(SceneRefTable table)
      {
         // 固定条目列表：逻辑 ID 与文件名解耦（保留枚举，决策已确认）
         var sceneValues = (GameScene[])System.Enum.GetValues(typeof(GameScene));
         var entries = new SceneRefTable.SceneEntry[sceneValues.Length];
         for (var i = 0; i < entries.Length; i++)
         {
            entries[i] = new SceneRefTable.SceneEntry { Scene = sceneValues[i] };
         }

         // 扫描场景目录
         foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot }))
         {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (asset == null) continue;

            var fileName = Path.GetFileNameWithoutExtension(path);

            // ConfigScene 独立字段
            if (fileName == "ConfigScene")
            {
               table.ConfigScene = new SceneRefTable.SceneEntry { Asset = asset };
               table.ConfigScene.SyncPath();
               continue;
            }

            // 玩法场景按文件名映射（一次性初始化便利）
            if (TryMapFileName(fileName, out var scene))
            {
               for (var i = 0; i < entries.Length; i++)
               {
                  if (entries[i].Scene == scene)
                  {
                     entries[i].Asset = asset;
                     entries[i].SyncPath();
                     break;
                  }
               }
            }
         }

         table.Entries = entries;
      }

      /// <summary>
      /// 同步 Build Settings：表内场景全部加入且启用，ConfigScene 强制置 [0]。
      /// 保留表外但已启用的场景（如自定义测试场景）。
      /// </summary>
      public static void SyncBuildSettings(SceneRefTable table)
      {
         var list = new List<EditorBuildSettingsScene>();

         if (table != null && table.ConfigScene != null && !string.IsNullOrEmpty(table.ConfigScene.Path))
         {
            list.Insert(0, new EditorBuildSettingsScene(table.ConfigScene.Path, true));
         }

         if (table != null && table.Entries != null)
         {
            foreach (var entry in table.Entries)
            {
               if (entry != null && !string.IsNullOrEmpty(entry.Path) && !list.Any(s => s.path == entry.Path))
               {
                  list.Add(new EditorBuildSettingsScene(entry.Path, true));
               }
            }
         }

         // 保留表外已启用的场景
         foreach (var scene in EditorBuildSettings.scenes)
         {
            if (scene.enabled && !list.Any(s => s.path == scene.path))
            {
               list.Add(scene);
            }
         }

         EditorBuildSettings.scenes = list.ToArray();
         Debug.Log("[SceneRefTableCreator] Build Settings 已同步。");
      }

      /// <summary>
      /// 文件名 → 逻辑枚举 的映射（仅初始化便利；重命名/移动后由 GUID 锚定，不再依赖名称）。
      /// </summary>
      private static bool TryMapFileName(string fileName, out GameScene scene)
      {
         switch (fileName)
         {
            case "0LoginScene": scene = GameScene.Login; return true;
            case "1MainScene": scene = GameScene.MainCity; return true;
            case "2GachaScene": scene = GameScene.Gacha; return true;
            case "3BattleScene": scene = GameScene.Battle; return true;
            case "4BattleScene-Test0": scene = GameScene.BattleGraybox; return true;
            default: scene = default; return false;
         }
      }
   }
}
