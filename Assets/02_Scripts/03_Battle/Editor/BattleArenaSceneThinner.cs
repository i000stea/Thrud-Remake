using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// T3.1C 步骤3-4 编辑器工具（幂等，可重复执行）：「检查并放置战斗固定固件」。
   ///
   /// 用户决策（2026-09-06）：战斗场景**不追求完全脱离场景**——角色控制 rig、飘字服务、
   /// 敌人管理（波次 Director）是战斗固定存在，直接放场景；场景内另设引导对象 battle_runtime
   /// （<see cref="BattleSceneRuntimeHost"/>）负责框架初始化 + 从关卡数据（BattleLevelData）初始化场景配置。
   /// 本工具把 BattleGraybox 收敛到目标态：
   ///   A. 关卡数据资产幂等就绪（3 预设 + BattleLevelCatalog）；
   ///   B. 撤销旧「rig prefab 内建飘字」迁移（若之前执行过：移除 DamageTexts 子节点并清 env 基值，回到干净基态）；
   ///   C. 场景治理：移除悬空/过时对象（BattleRoleRepository、LevelSceneAnchor），
   ///      放置固定固件（PlayerControl rig 实例、Damage-Texts 飘字、BattleStageDirector、battle_runtime），
   ///      接线：rig 实例 env 覆盖 → 场景 Damage-Texts；battle_runtime 回填 catalog/rig/director 引用；
   ///   D. 保存并输出根对象清单报告。
   /// 移除对象均按「场景根对象 + 组件/来源」双条件命中；UIRoot（UIKit 框架根）保留待步骤6 核对。
   /// </summary>
   public static class BattleArenaSceneThinner
   {
      private const string ScenePath = "Assets/01_Scenes/BattleGraybox.unity";
      private const string RigPrefabPath = "Assets/04_Prefabs/Battle_PlayerControl.prefab";
      private const string RigInstanceName = "PlayerControl";
      private const string DamageTextsName = "Damage-Texts";
      private const string DirectorName = "BattleStageDirector";
      private const string RuntimeHostName = "battle_runtime";
      private const string CatalogPath = "Assets/Data/BattleLevels/BattleLevelCatalog.asset";
      private const string DamageTextPrefabGuid = "c9ea3a00046bf7446b0766abfd67297f";

      private const string MenuRoot = "Thrud/Battle/T3.1C/";

      [MenuItem(MenuRoot + "1. 检查并放置战斗固定固件 (BattleGraybox)")]
      public static void EnsureArenaSceneConfigured()
      {
         if (EditorApplication.isPlaying)
         {
            Debug.LogError("[T3.1C] 请在退出 Play Mode 后执行本菜单。");
            return;
         }

         // A. 关卡数据资产就绪（幂等；CreatePresetLevels 末尾自动刷新目录）
         BattleLevelDataSetup.CreatePresetLevels();

         // B. 撤销旧 prefab 内建飘字（若存在）
         UnbakeDamageTextsFromRigPrefab();

         // C. 场景治理 + 固件放置
         var scene = ResolveSceneForEdit(ScenePath, out var openedByUs);
         if (!scene.IsValid())
         {
            Debug.LogError($"[T3.1C] 无法打开场景：{ScenePath}，中止。");
            return;
         }

         var report = new System.Text.StringBuilder();
         try
         {
            RemoveStaleObjects(scene);
            var (rig, rigCreated) = EnsureRigInstance(scene);
            var damageController = EnsureDamageTexts(scene);
            var director = EnsureStageDirector(scene);
            WireRigEnvironmentOverride(rig, damageController);
            var host = EnsureRuntimeHost(scene, rig, director, out var hostCreated);
            WireRuntimeHost(host, rig, director, out var hostWired);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            report.AppendLine("[T3.1C] 检查并放置完成：");
            report.AppendLine($"  rig 实例：{(rigCreated ? "新建" : "已存在")}（{RigPrefabPath}）");
            report.AppendLine($"  飘字服务：{(damageController != null ? "Damage-Texts 就绪" : "缺失(见警告)")}");
            report.AppendLine($"  敌人管理 Director：{(director != null ? "BattleStageDirector 就绪" : "缺失(见警告)")}");
            report.AppendLine($"  battle_runtime：{(hostCreated ? "新建" : "已存在")}；引用回填：{(hostWired ? "完成(含本就一致)" : "未接线(见警告/错误)")}");
            report.Append("  场景根对象：");
            var names = new System.Collections.Generic.List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
               names.Add(root.name);
            }

            report.AppendLine(string.Join("、", names));
            Debug.Log(report.ToString());
         }
         catch (System.Exception e)
         {
            Debug.LogException(e);
         }
         finally
         {
            if (openedByUs)
            {
               EditorSceneManager.CloseScene(scene, true);
            }
         }
      }

      // ---------------- prefab 基态回滚 ----------------

      /// <summary>撤销旧「rig 内建飘字」迁移：移除 prefab 内 DamageTexts 子节点并清 rig.env 基值。</summary>
      private static void UnbakeDamageTextsFromRigPrefab()
      {
         var root = PrefabUtility.LoadPrefabContents(RigPrefabPath);
         if (root == null)
         {
            Debug.LogError($"[T3.1C] 无法打开 prefab：{RigPrefabPath}，跳过回滚。");
            return;
         }

         var changed = false;
         try
         {
            var rig = root.GetComponent<Battle_PlayerControl>();
            if (rig != null)
            {
               var childTr = root.transform.Find("DamageTexts");
               if (childTr != null)
               {
                  Object.DestroyImmediate(childTr.gameObject);
                  changed = true;
               }

               // 若 base env 仍指向刚删除的内建节点，清空回净态
               var rigSo = new SerializedObject(rig);
               var envProp = rigSo.FindProperty("environmentEffectController");
               if (envProp != null && envProp.objectReferenceValue != null)
               {
                  envProp.objectReferenceValue = null;
                  rigSo.ApplyModifiedPropertiesWithoutUndo();
                  changed = true;
               }
            }

            if (changed)
            {
               PrefabUtility.SaveAsPrefabAsset(root, RigPrefabPath);
               AssetDatabase.SaveAssets();
               Debug.Log("[T3.1C] 已撤销 rig prefab 内建飘字（移除 DamageTexts 子节点，env 基值清空）。");
            }
         }
         finally
         {
            PrefabUtility.UnloadPrefabContents(root);
         }
      }

      // ---------------- 场景治理与固件放置 ----------------

      private static void RemoveStaleObjects(Scene scene)
      {
         foreach (var root in scene.GetRootGameObjects())
         {
            if (root.name == "BattleRoleRepository")
            {
               Object.DestroyImmediate(root);
               Debug.Log("[T3.1C] 移除悬空对象：BattleRoleRepository（无人消费）。");
            }
            else if (root.name == "LevelSceneAnchor")
            {
               Object.DestroyImmediate(root);
               Debug.Log("[T3.1C] 移除过时对象：LevelSceneAnchor（装配职责已并入 battle_runtime）。");
            }
         }
      }

      private static (Battle_PlayerControl rig, bool created) EnsureRigInstance(Scene scene)
      {
         var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
         var existing = FindSceneRoot(scene, RigInstanceName);
         var rig = existing != null ? existing.GetComponentInChildren<Battle_PlayerControl>() : null;
         if (rig != null)
         {
            return (rig, false);
         }

         if (rigAsset == null)
         {
            Debug.LogError($"[T3.1C] 找不到 rig prefab：{RigPrefabPath}，无法放置角色控制 rig。");
            return (null, false);
         }

         var instance = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset, scene);
         instance.name = RigInstanceName;
         rig = instance.GetComponent<Battle_PlayerControl>();
         if (rig == null)
         {
            Debug.LogError($"[T3.1C] rig prefab 根节点缺少 Battle_PlayerControl：{RigPrefabPath}");
            return (null, false);
         }

         Debug.Log("[T3.1C] 已放置角色控制 rig 实例（PlayerControl，出生点由 battle_runtime 按关卡数据应用）。");
         return (rig, true);
      }

      private static BattleEnvironmentEffectController EnsureDamageTexts(Scene scene)
      {
         var root = FindSceneRoot(scene, DamageTextsName);
         if (root == null)
         {
            root = CreateSceneRoot(scene, DamageTextsName);
         }

         var controller = root.GetComponent<BattleEnvironmentEffectController>();
         if (controller == null)
         {
            controller = root.AddComponent<BattleEnvironmentEffectController>();
         }

         var damagePrefab = LoadAssetByGuid<GameObject>(DamageTextPrefabGuid);
         var so = new SerializedObject(controller);
         var prefabProp = so.FindProperty("damageTextPrefab");
         var parentProp = so.FindProperty("damageTextParent");
         var dirty = false;
         if (prefabProp != null && prefabProp.objectReferenceValue != damagePrefab)
         {
            prefabProp.objectReferenceValue = damagePrefab;
            dirty = true;
         }

         if (parentProp != null && parentProp.objectReferenceValue != root.transform)
         {
            parentProp.objectReferenceValue = root.transform;
            dirty = true;
         }

         if (dirty)
         {
            so.ApplyModifiedPropertiesWithoutUndo();
         }

         if (damagePrefab == null)
         {
            Debug.LogWarning($"[T3.1C] 未按 GUID {DamageTextPrefabGuid} 找到飘字 prefab，请核对资产。");
         }

         return controller;
      }

      private static BattleStageDirector EnsureStageDirector(Scene scene)
      {
         var root = FindSceneRoot(scene, DirectorName);
         if (root == null)
         {
            root = CreateSceneRoot(scene, DirectorName);
         }

         var director = root.GetComponent<BattleStageDirector>();
         if (director == null)
         {
            director = root.AddComponent<BattleStageDirector>();
         }

         return director;
      }

      private static BattleSceneRuntimeHost EnsureRuntimeHost(Scene scene, Battle_PlayerControl rig, BattleStageDirector director, out bool created)
      {
         var root = FindSceneRoot(scene, RuntimeHostName);
         if (root == null)
         {
            root = CreateSceneRoot(scene, RuntimeHostName);
            created = true;
         }
         else
         {
            created = false;
         }

         var host = root.GetComponent<BattleSceneRuntimeHost>();
         if (host == null)
         {
            host = root.AddComponent<BattleSceneRuntimeHost>();
         }

         return host;
      }

      private static void WireRuntimeHost(BattleSceneRuntimeHost host, Battle_PlayerControl rig, BattleStageDirector director, out bool wired)
      {
         wired = false;
         if (host == null)
         {
            return;
         }

         var catalog = AssetDatabase.LoadAssetAtPath<BattleLevelCatalog>(CatalogPath);
         var so = new SerializedObject(host);
         var catalogProp = so.FindProperty("catalog");
         var rigProp = so.FindProperty("playerControl");
         var directorProp = so.FindProperty("stageDirector");
         if (catalogProp != null && catalogProp.objectReferenceValue != catalog)
         {
            catalogProp.objectReferenceValue = catalog;
         }

         if (rigProp != null && rigProp.objectReferenceValue != rig)
         {
            rigProp.objectReferenceValue = rig;
         }

         if (directorProp != null && directorProp.objectReferenceValue != director)
         {
            directorProp.objectReferenceValue = director;
         }

         if (so.hasModifiedProperties)
         {
            so.ApplyModifiedPropertiesWithoutUndo();
            wired = true;
         }
         else if (catalog != null && catalogProp != null
                  && rigProp != null && rigProp.objectReferenceValue == rig
                  && directorProp != null && directorProp.objectReferenceValue == director)
         {
            // 引用本就一致（幂等收敛）：无需回填，但视为接线完成
            wired = true;
         }

         if (catalog == null)
         {
            Debug.LogWarning($"[T3.1C] 关卡目录资产缺失：{CatalogPath}，battle_runtime 直连默认关解析不可用。");
         }
      }

      /// <summary>rig 实例 env 覆盖 → 场景 Damage-Texts（预置体实例属性修改，写入 override）。</summary>
      private static void WireRigEnvironmentOverride(Battle_PlayerControl rig, BattleEnvironmentEffectController damageController)
      {
         if (rig == null || damageController == null)
         {
            return;
         }

         var so = new SerializedObject(rig);
         var envProp = so.FindProperty("environmentEffectController");
         if (envProp != null && envProp.objectReferenceValue != damageController)
         {
            envProp.objectReferenceValue = damageController;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
         }
      }

      // ---------------- 工具 ----------------

      private static Scene ResolveSceneForEdit(string scenePath, out bool openedByUs)
      {
         openedByUs = false;
         for (var i = 0; i < SceneManager.sceneCount; i++)
         {
            var loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == scenePath)
            {
               return loaded;
            }
         }

         var opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
         openedByUs = opened.IsValid() && opened.isLoaded;
         return opened;
      }

      private static GameObject FindSceneRoot(Scene scene, string name)
      {
         foreach (var root in scene.GetRootGameObjects())
         {
            if (root.name == name)
            {
               return root;
            }
         }

         return null;
      }

      private static GameObject CreateSceneRoot(Scene scene, string name)
      {
         var go = new GameObject(name);
         SceneManager.MoveGameObjectToScene(go, scene);
         return go;
      }

      private static T LoadAssetByGuid<T>(string guid) where T : Object
      {
         var path = AssetDatabase.GUIDToAssetPath(guid);
         return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
      }
   }
}
