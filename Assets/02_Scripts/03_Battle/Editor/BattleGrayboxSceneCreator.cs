using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeaFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// T0.3 灰盒战斗场景一键生成工具。
   /// 菜单：Thrud/Battle/Setup BattleGraybox Scene
   ///
   /// 生成 Assets/01_Scenes/BattleGraybox.unity：
   ///   - PlayerControl.prefab 实例（复用现有角色/武器/相机/准星/血条装配）
   ///   - BattleRoleRepository（新数据仓库，复用 5BattleScene-Test0 已核实的 4 角色 × 4 武器数据）
   ///   - 地面 + 四周空气墙（BoxCollider，灰盒透明墙体）
   ///   - 2 个灰盒胶囊敌人（BattleEnemyEntity，验证锁敌目标存在）
   ///   - Damage-Texts（BattleEnvironmentEffectController，供 T1.4 飘字复用）
   ///   - Directional Light / EventSystem
   ///
   /// 由编辑器生成场景，避免手写场景 YAML 的序列化风险（同 ConfigSceneCreator 模式）。
   /// 场景 key 使用文件名 BattleGraybox，加入 Build Settings（追加，不抢占 ConfigScene[0]）。
   /// </summary>
   public static class BattleGrayboxSceneCreator
   {
      public const string ScenePath = "Assets/01_Scenes/BattleGraybox.unity";

      private const string PlayerControlPrefabPath = "Assets/04_Prefabs/PlayerControl.prefab";
      private const string DamageTextPrefabPath = "Assets/04_Prefabs/Damage-Text.prefab";
      private const string EnemyBulletPrefabPath = "Assets/04_Prefabs/Enemy/EnemyBullet/EnemyBullet.prefab";

      /// <summary>Test0 已核实的角色数据 GUID（T0.2 验证通过）。</summary>
      private static readonly string[] RoleGuids =
      {
         "ad3ae915e4a7138479b9953688484145", // 2004001-Willa
         "9c291e91627ba1846979e40973180dbf", // 2011001-GwyrDolyn
         "a27a63b8f76f39048996b7f95b2dfa6f", // 2008001-Yera
         "5b84149f04e74da41add29c24f116a1c", // 2001001-Faria
      };

      /// <summary>Test0 已核实的武器数据 GUID（T0.2 验证通过）。</summary>
      private static readonly string[] WeaponGuids =
      {
         "01d1b6448a6ba434888abe289aad260b", // 5021010-biangezhe-gai
         "20058370950d7c14abf91441865a97d5", // 5023010-xianzhaozhe-gai
         "feffcc04d72a2e64f98bb19f251b9cfd", // 5024010-qihuanzhe-gai
         "34c2c9667ebdcdb4187a36f03496daa6", // 5025010-kuangrezhe-gai
      };

      // 灰盒几何参数（编辑器生成期常量，非运行时玩法数值）
      private const float ArenaHalfExtent = 50f; // 半场边长：±50
      private const float WallHeight = 40f;      // 空气墙高度
      private const float WallThickness = 1f;    // 空气墙厚度
      private const float GroundTop = 0f;        // 地面顶面 y

      /// <summary>
      /// 幂等自动生成钩子（仅编辑器，不进入 PlayMode）。
      /// 场景缺失且当前会话未尝试过时，在编辑器刷新/编译完成后自动生成灰盒场景，
      /// 省去手动点菜单的步骤（菜单 Thrud/Battle/Setup BattleGraybox Scene 仍保留）。
      /// 若当前场景有未保存修改则跳过，避免打断用户编辑。
      /// </summary>
      [InitializeOnLoadMethod]
      private static void RegisterAutoCreate()
      {
         EditorApplication.delayCall += TryAutoCreateOnce;
      }

      private static void TryAutoCreateOnce()
      {
         if (EditorApplication.isPlayingOrWillChangePlaymode) return;
         if (File.Exists(ScenePath)) return;
         if (SessionState.GetBool("BattleGrayboxAutoCreateRan", false)) return;
         SessionState.SetBool("BattleGrayboxAutoCreateRan", true);

         if (EditorSceneManager.GetActiveScene().isDirty)
         {
            Debug.LogWarning("[BattleGrayboxSceneCreator] 灰盒场景缺失，但当前场景有未保存修改，已跳过自动生成。可保存后点击菜单 Thrud/Battle/Setup BattleGraybox Scene 手动生成。");
            return;
         }

         Debug.Log("[BattleGrayboxSceneCreator] 检测到灰盒场景缺失，自动生成 BattleGraybox.unity …");
         CreateGrayboxScene();
      }

      /// <summary>
      /// 生成灰盒战斗场景并加入 Build Settings。
      /// 可由菜单触发，也可由命令行 `-executeMethod TeaGame.Battle.Editor.BattleGrayboxSceneCreator.CreateGrayboxScene` 批量执行。
      /// </summary>
      [MenuItem("Thrud/Battle/Setup BattleGraybox Scene")]
      public static void CreateGrayboxScene()
      {
         var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
         scene.name = "BattleGraybox";

         CreateLighting();
         var environment = CreateEnvironment();
         var player = CreatePlayer();
         DisableLegacyPlayerControl(player);
         var repository = CreateRoleRepository();
         var effectController = CreateDamageTexts();
         WirePlayer(player, repository, effectController);
         CreateEnemies(environment);
         CreateEventSystem();

         player.transform.position = new Vector3(0f, 2f, 0f);

         var directory = Path.GetDirectoryName(ScenePath);
         if (!string.IsNullOrEmpty(directory))
         {
            Directory.CreateDirectory(directory);
         }

         EditorSceneManager.SaveScene(scene, ScenePath);
         AddToBuildSettings();

         EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
         AssetDatabase.Refresh();
         Debug.Log($"[BattleGrayboxSceneCreator] 创建完成：{ScenePath}。已追加到 Build Settings（ConfigScene 仍为 [0]）。可直接 Play 验证：移动不穿墙、相机跟随、锁敌目标存在。");
      }

      /// <summary>
      /// 结构校验（批量模式）。
      /// 检查场景文件、PlayerControl/数据仓库/飘字控制器/地面/空气墙/敌人是否齐全，并核对 Build Settings。
      /// 结果全部通过时进程退出码为 0，否则为 1。
      /// </summary>
      public static void VerifyGrayboxScene()
      {
         var results = new List<string>();

         if (!File.Exists(ScenePath))
         {
            LogVerify(results, $"场景文件不存在：{ScenePath}", false);
         }
         else
         {
            LogVerify(results, $"场景文件存在：{ScenePath}", true);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = FindComponentInScene<PlayerControl>();
            LogVerify(results, "场景包含 PlayerControl（PlayerControl.prefab 实例）", player != null);

            if (player)
            {
               var legacyPlayer = player.GetComponent<TeaFramework.PlayerControl>();
               LogVerify(results, "旧版 TeaFramework.PlayerControl 已禁用（灰盒仅用新版系统，避免越界与准星冲突）", legacyPlayer == null || !legacyPlayer.enabled);
            }

            var repository = FindComponentInScene<BattleRoleRepository>();
            if (repository)
            {
               var roles = GetArrayReferenceCount(repository, "loadRoles");
               var weapons = GetArrayReferenceCount(repository, "loadWeapons");
               LogVerify(results, $"BattleRoleRepository 已配置（角色 {roles} / 武器 {weapons}）", roles > 0 && weapons > 0);
            }
            else
            {
               LogVerify(results, "场景缺少 BattleRoleRepository", false);
            }

            var effectController = FindComponentInScene<BattleEnvironmentEffectController>();
            LogVerify(results, "场景包含 BattleEnvironmentEffectController（Damage-Texts）", effectController != null);

            var ground = GameObject.Find("Graybox_Ground");
            LogVerify(results, "地面 Graybox_Ground 存在且带碰撞体", ground != null && ground.GetComponent<Collider>() != null);

            var wallOk = true;
            foreach (var dir in new[] { "N", "S", "E", "W" })
            {
               var wall = GameObject.Find("Graybox_Wall_" + dir);
               if (wall == null || wall.GetComponent<Collider>() == null) wallOk = false;
            }
            LogVerify(results, "四周空气墙（Graybox_Wall_N/S/E/W）存在且带碰撞体", wallOk);

            var enemyCount = 0;
            foreach (var enemy in FindComponentsInScene<BattleEnemyEntity>()) enemyCount++;
            LogVerify(results, $"灰盒敌人 BattleEnemyEntity 数量为 2（当前 {enemyCount}）", enemyCount == 2);

            var inBuild = EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled);
            LogVerify(results, "BattleGraybox 已勾选进 Build Settings", inBuild);

            EditorSceneManager.SaveScene(scene);
         }

         var passed = results.All(r => r.StartsWith("[PASS]"));
         foreach (var line in results)
         {
            Debug.Log($"[BattleGrayboxSceneCreator] {line}");
         }

         Debug.Log($"[BattleGrayboxSceneCreator] 结构校验 {(passed ? "PASS" : "FAIL")}（{results.Count(r => r.StartsWith("[PASS]"))}/{results.Count}）");
         EditorApplication.Exit(passed ? 0 : 1);
      }

      /// <summary>
      /// 创建主方向光。
      /// </summary>
      private static void CreateLighting()
      {
         var lightObject = new GameObject("Directional Light");
         var light = lightObject.AddComponent<Light>();
         light.type = LightType.Directional;
         light.intensity = 1.1f;
         lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
      }

      /// <summary>
      /// 创建灰盒环境：地面 + 四周空气墙。
      /// </summary>
      private static Transform CreateEnvironment()
      {
         var environment = new GameObject("Environment").transform;

         var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
         ground.name = "Graybox_Ground";
         ground.transform.SetParent(environment, false);
         ground.transform.position = new Vector3(0f, GroundTop - 0.5f, 0f);
         ground.transform.localScale = new Vector3(ArenaHalfExtent * 2f, 1f, ArenaHalfExtent * 2f);
         ground.GetComponent<MeshRenderer>().sharedMaterial = CreateGrayboxMaterial("GrayboxMat_Ground", new Color(0.45f, 0.45f, 0.48f, 1f), false);

         // 四周空气墙：厚 1、高 WallHeight，顶面 y = WallHeight
         var wallY = WallHeight / 2f;
         var half = ArenaHalfExtent;
         CreateWall(environment, "Graybox_Wall_N", new Vector3(0f, wallY, -half), new Vector3(half * 2f, WallHeight, WallThickness));
         CreateWall(environment, "Graybox_Wall_S", new Vector3(0f, wallY, half), new Vector3(half * 2f, WallHeight, WallThickness));
         CreateWall(environment, "Graybox_Wall_E", new Vector3(half, wallY, 0f), new Vector3(WallThickness, WallHeight, half * 2f));
         CreateWall(environment, "Graybox_Wall_W", new Vector3(-half, wallY, 0f), new Vector3(WallThickness, WallHeight, half * 2f));

         return environment;
      }

      private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale)
      {
         var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
         wall.name = name;
         wall.transform.SetParent(parent, false);
         wall.transform.position = position;
         wall.transform.localScale = scale;
         wall.GetComponent<MeshRenderer>().sharedMaterial = CreateGrayboxMaterial("GrayboxMat_Wall", new Color(0.25f, 0.55f, 1f, 0.35f), true);
      }

      /// <summary>
      /// 实例化 PlayerControl.prefab（保持 prefab 链接）。
      /// </summary>
      private static PlayerControl CreatePlayer()
      {
         var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerControlPrefabPath);
         if (prefab == null)
         {
            Debug.LogError($"[BattleGrayboxSceneCreator] 未找到 PlayerControl.prefab：{PlayerControlPrefabPath}");
            throw new FileNotFoundException(PlayerControlPrefabPath);
         }

         var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
         instance.name = "PlayerControl";
         return instance.GetComponent<PlayerControl>();
      }

      /// <summary>
      /// 禁用 prefab 上遗留的旧版 <see cref="TeaFramework.PlayerControl"/> 组件。
      /// 灰盒场景只走新版 <see cref="TeaGame.PlayerControl"/>，原因：
      ///  1. 旧版依赖 StoreBattleRoleData 单例填充 entityBattleDatas，灰盒没有该对象 → IndexOutOfRange 报错；
      ///  2. 旧版 P_Aim 与新版 BattleAimSystem 争抢同一准星（Cross_LockEnemy），导致锁敌表现异常。
      /// 仅对场景实例禁用（场景级覆盖），不影响 Test0/Test1 的既有行为。
      /// </summary>
      private static void DisableLegacyPlayerControl(PlayerControl player)
      {
         var legacyPlayer = player.GetComponent<TeaFramework.PlayerControl>();
         if (!legacyPlayer)
         {
            Debug.LogWarning("[BattleGrayboxSceneCreator] PlayerControl.prefab 上未发现旧版 TeaFramework.PlayerControl，跳过禁用。");
            return;
         }

         legacyPlayer.enabled = false;
         Debug.Log("[BattleGrayboxSceneCreator] 已禁用旧版 TeaFramework.PlayerControl（灰盒场景仅使用新版战斗系统）。");
      }

      /// <summary>
      /// 创建新数据仓库 BattleRoleRepository，并写入 Test0 已核实的角色/武器数据。
      /// 设置 dontDestroyOnLoad=false：数据随灰盒场景生命周期，避免重复进入场景时叠加常驻实例。
      /// </summary>
      private static BattleRoleRepository CreateRoleRepository()
      {
         var repositoryObject = new GameObject("BattleRoleRepository");
         var repository = repositoryObject.AddComponent<BattleRoleRepository>();

         var serialized = new SerializedObject(repository);
         serialized.FindProperty("dontDestroyOnLoad").boolValue = false;

         var rolesProperty = serialized.FindProperty("loadRoles");
         rolesProperty.arraySize = RoleGuids.Length;
         for (var i = 0; i < RoleGuids.Length; i++)
         {
            rolesProperty.GetArrayElementAtIndex(i).objectReferenceValue = LoadAssetByGuid<RoleItem_Data>(RoleGuids[i], "角色");
         }

         var weaponsProperty = serialized.FindProperty("loadWeapons");
         weaponsProperty.arraySize = WeaponGuids.Length;
         for (var i = 0; i < WeaponGuids.Length; i++)
         {
            weaponsProperty.GetArrayElementAtIndex(i).objectReferenceValue = LoadAssetByGuid<WeaponItem_Data>(WeaponGuids[i], "武器");
         }

         serialized.ApplyModifiedPropertiesWithoutUndo();
         return repository;
      }

      /// <summary>
      /// 创建伤害飘字控制器（Damage-Texts）。
      /// </summary>
      private static BattleEnvironmentEffectController CreateDamageTexts()
      {
         var effectObject = new GameObject("Damage-Texts");
         var controller = effectObject.AddComponent<BattleEnvironmentEffectController>();

         var damageTextPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamageTextPrefabPath);
         if (damageTextPrefab == null)
         {
            Debug.LogWarning($"[BattleGrayboxSceneCreator] 未找到飘字预制体：{DamageTextPrefabPath}，飘字将不显示（不影响本卡验证）。");
         }

         var serialized = new SerializedObject(controller);
         serialized.FindProperty("damageTextPrefab").objectReferenceValue = damageTextPrefab;
         serialized.FindProperty("damageTextParent").objectReferenceValue = effectObject.transform;
         serialized.ApplyModifiedPropertiesWithoutUndo();
         return controller;
      }

      /// <summary>
      /// 创建 2 个灰盒胶囊敌人（BattleEnemyEntity），注册进锁敌系统作为可锁定目标。
      /// </summary>
      private static void CreateEnemies(Transform environment)
      {
         var enemyBulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBulletPrefabPath);
         if (enemyBulletPrefab == null)
         {
            Debug.LogWarning($"[BattleGrayboxSceneCreator] 未找到敌人子弹预制体：{EnemyBulletPrefabPath}，敌人将不发射子弹（仅作为锁敌目标）。");
         }

         CreateGrayboxEnemy(environment, "Enemy_Graybox_Chaser_01", new Vector3(5f, 1f, 12f), new Color(0.9f, 0.3f, 0.3f, 1f), enemyBulletPrefab);
         CreateGrayboxEnemy(environment, "Enemy_Graybox_Chaser_02", new Vector3(-5f, 1f, 14f), new Color(0.95f, 0.55f, 0.2f, 1f), enemyBulletPrefab);
      }

      private static void CreateGrayboxEnemy(Transform parent, string name, Vector3 position, Color color, GameObject enemyBulletPrefab)
      {
         var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
         enemy.name = name;
         enemy.transform.SetParent(parent, false);
         enemy.transform.position = position;
         enemy.transform.localScale = new Vector3(1.1f, 1.3f, 1.1f);
         enemy.GetComponent<MeshRenderer>().sharedMaterial = CreateGrayboxMaterial("GrayboxMat_Enemy", color, false);

         var entity = enemy.AddComponent<BattleEnemyEntity>();
         var serialized = new SerializedObject(entity);
         serialized.FindProperty("baseHealth").floatValue = 100f;
         serialized.FindProperty("shotCooldownMin").floatValue = 3f;
         serialized.FindProperty("shotCooldownMax").floatValue = 5f;
         serialized.FindProperty("enemyBulletPrefab").objectReferenceValue = enemyBulletPrefab;
         serialized.FindProperty("enemyBulletSpeed").floatValue = 8f;
         serialized.ApplyModifiedPropertiesWithoutUndo();
      }

      /// <summary>
      /// 把数据仓库与飘字控制器绑定到 PlayerControl 实例。
      /// </summary>
      private static void WirePlayer(PlayerControl player, BattleRoleRepository repository, BattleEnvironmentEffectController effectController)
      {
         var serialized = new SerializedObject(player);
         serialized.FindProperty("dataRepository").objectReferenceValue = repository;
         serialized.FindProperty("environmentEffectController").objectReferenceValue = effectController;
         serialized.ApplyModifiedPropertiesWithoutUndo();
      }

      /// <summary>
      /// 创建 UI 事件系统（灰盒 HUD 为纯展示，EventSystem 备用）。
      /// </summary>
      private static void CreateEventSystem()
      {
         if (Object.FindObjectOfType<EventSystem>()) return;

         var eventSystemObject = new GameObject("EventSystem");
         eventSystemObject.AddComponent<EventSystem>();
         eventSystemObject.AddComponent<StandaloneInputModule>();
      }

      /// <summary>
      /// 把灰盒场景追加进 Build Settings（保持 ConfigScene 为 [0] 入口）。
      /// </summary>
      private static void AddToBuildSettings()
      {
         var buildScenes = EditorBuildSettings.scenes.ToList();
         buildScenes.RemoveAll(s => s.path == ScenePath);
         buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
         EditorBuildSettings.scenes = buildScenes.ToArray();
      }

      private static Material CreateGrayboxMaterial(string name, Color color, bool transparent)
      {
         var shader = Shader.Find("Universal Render Pipeline/Lit");
         if (shader == null)
         {
            shader = Shader.Find("Standard");
         }

         var material = new Material(shader != null ? shader : Shader.Find("Diffuse"))
         {
            name = name,
            color = color
         };

         if (transparent)
         {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f); // URP/Lit 透明表面
            material.SetFloat("_Blend", 0f);   // Alpha 混合
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
         }

         return material;
      }

      private static T LoadAssetByGuid<T>(string guid, string label) where T : Object
      {
         var path = AssetDatabase.GUIDToAssetPath(guid);
         if (string.IsNullOrEmpty(path))
         {
            Debug.LogError($"[BattleGrayboxSceneCreator] 未找到{label}资源 GUID：{guid}");
            return null;
         }

         var asset = AssetDatabase.LoadAssetAtPath<T>(path);
         if (asset == null)
         {
            Debug.LogError($"[BattleGrayboxSceneCreator] 资源加载失败（{label}）：{path}");
         }

         return asset;
      }

      private static void LogVerify(List<string> results, string message, bool passed)
      {
         results.Add((passed ? "[PASS] " : "[FAIL] ") + message);
      }

      private static int GetArrayReferenceCount(Object target, string propertyName)
      {
         var serialized = new SerializedObject(target);
         var property = serialized.FindProperty(propertyName);
         if (property == null || !property.isArray) return 0;

         var count = 0;
         for (var i = 0; i < property.arraySize; i++)
         {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue != null) count++;
         }

         return count;
      }

      private static T FindComponentInScene<T>() where T : Component
      {
         return Object.FindObjectOfType<T>();
      }

      private static IEnumerable<T> FindComponentsInScene<T>() where T : Component
      {
         foreach (var obj in Object.FindObjectsOfType<T>())
         {
            yield return obj;
         }
      }

      // ===================== T2.1 敌人体系（Prefab 化，2026-09-03） =====================
      // 规格：4 类纯弹幕敌人（巡弋者/站桩炮台/自爆·导弹/自爆·地雷），固定姿态、不持续追踪、无护盾。
      // 敌人一律做成 prefab 资产，统一放 Assets/04_Prefabs/Enemy/（灰盒材质在同目录 Materials/），
      // 便于编辑器拖拽/双击修改参数、直接可视化调试；场景中的演示敌人均为 prefab 实例。
      //
      // 菜单：
      //   Thrud/Battle/T2.1 Create Enemy Prefabs —— 生成/确保敌人 prefab 资产（幂等）
      //   Thrud/Battle/T2.1 Add Demo Enemies     —— 生成 prefab + 灰盒场景摆放演示实例（幂等重建）
      // 备注：不再在场景用代码生成 Primitive 敌人；不做精英/Boss（Boss 归 T3.2）。

      private const string T21EnemyPrefabDir = "Assets/04_Prefabs/Enemy";
      private const string T21EnemyMaterialDir = "Assets/04_Prefabs/Enemy/Materials";

      /// <summary>灰盒场景演示布局（prefab 名 + 摆放位置）。</summary>
      private struct T21DemoEntry
      {
         public readonly string PrefabName;
         public readonly Vector3 Position;

         public T21DemoEntry(string prefabName, Vector3 position)
         {
            PrefabName = prefabName;
            Position = position;
         }
      }

      private static readonly T21DemoEntry[] T21DemoLayout =
      {
         new T21DemoEntry("Enemy_Patrol", new Vector3(5f, 1f, 12f)),
         new T21DemoEntry("Enemy_Patrol", new Vector3(-5f, 1f, 14f)),
         new T21DemoEntry("Enemy_GunTurret", new Vector3(14f, 1f, -8f)),
         new T21DemoEntry("Enemy_GunTurret_Heavy", new Vector3(12f, 1f, -15f)),
         new T21DemoEntry("Enemy_Missile", new Vector3(9f, 1f, 18f)),
         new T21DemoEntry("Enemy_Mine", new Vector3(0f, 0.7f, 18f)),
         new T21DemoEntry("Enemy_Mine", new Vector3(-5f, 0.7f, 20f)),
      };

      /// <summary>
      /// 生成/确保全部 T2.1 敌人 prefab 资产（幂等；已有则直接返回）。
      /// </summary>
      [MenuItem("Thrud/Battle/T2.1 Create Enemy Prefabs")]
      public static void CreateT21EnemyPrefabs()
      {
         var enemyBullet = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBulletPrefabPath);
         if (enemyBullet == null)
         {
            Debug.LogWarning($"[T2.1] 未找到敌人子弹预制体：{EnemyBulletPrefabPath}，远程敌人将不发射（仅演示外形与移动）。");
         }

         EnsureAllT21EnemyPrefabs(enemyBullet);
         AssetDatabase.Refresh();
         Debug.Log($"[T2.1] 敌人 prefab 已就绪：{T21EnemyPrefabDir}（Enemy_Patrol / Enemy_GunTurret / Enemy_GunTurret_Heavy / Enemy_Missile / Enemy_Mine）。可在编辑器直接拖入场景或双击改参数。");
      }

      /// <summary>
      /// 重建敌人 prefab（先删后建）：当敌人组件新增字段/结构变化时，旧资产不会自动更新，
      /// 需执行本菜单强制重建（材质资产保留）。之后再用 Add Demo Enemies 重新摆放场景实例。
      /// </summary>
      [MenuItem("Thrud/Battle/T2.1 Rebuild Enemy Prefabs")]
      public static void RebuildT21EnemyPrefabs()
      {
         string[] prefabNames =
         {
            "Enemy_Patrol", "Enemy_GunTurret", "Enemy_GunTurret_Heavy",
            "Enemy_Missile", "Enemy_Mine", "Enemy_BlastRange",
         };

         var deleted = 0;
         foreach (var prefabName in prefabNames)
         {
            var path = T21EnemyPrefabDir + "/" + prefabName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            AssetDatabase.DeleteAsset(path);
            deleted++;
         }

         AssetDatabase.SaveAssets();
         Debug.Log($"[T2.1] 已删除 {deleted} 个旧敌人 prefab，开始重建…");
         CreateT21EnemyPrefabs();
      }

      /// <summary>
      /// 生成 prefab 并摆放灰盒演示敌人（会重建工具管理的 Enemy_Graybox_* 敌人实例）。
      /// </summary>
      [MenuItem("Thrud/Battle/T2.1 Add Demo Enemies")]
      public static void AddT21DemoEnemies()
      {
         var activeScene = EditorSceneManager.GetActiveScene();
         if (activeScene.path != ScenePath)
         {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            activeScene = EditorSceneManager.GetActiveScene();
         }

         var environment = GameObject.Find("Environment");
         if (environment == null)
         {
            Debug.LogError("[T2.1] 当前场景缺少 Environment 根节点，请先运行 Thrud/Battle/Setup BattleGraybox Scene 生成灰盒场景。");
            return;
         }

         var enemyBullet = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBulletPrefabPath);
         EnsureAllT21EnemyPrefabs(enemyBullet);
         PlaceT21DemoEnemies(environment.transform);

         EditorSceneManager.SaveScene(activeScene);
         AssetDatabase.Refresh();
         Debug.Log("[T2.1] 演示敌人已摆放为 prefab 实例并保存。Play 验证：巡弋者(游走速射,不追玩家) / 站桩炮台(扇形弹幕,含重装变体) / 自爆·导弹(追踪后小爆) / 自爆·地雷(感应或被打爆→大爆)。");
      }

      private static void EnsureAllT21EnemyPrefabs(GameObject enemyBullet)
      {
         // 爆炸范围提示环 prefab（Mine 常驻子环与之共用材质）
         var blastRing = EnsureT21BlastRangePrefab();

         EnsureT21EnemyPrefab("Enemy_Patrol", EnemyKind.Patrol, PrimitiveType.Capsule,
            new Color(0.9f, 0.35f, 0.35f, 1f), new Vector3(1f, 1.15f, 1f), null,
            enemyBullet, baseHealth: 35f, moveSpeed: 2.2f, wanderRadius: 4f,
            barrageCount: 1, spreadDeg: 0f, bulletSpeed: 8f, cdMin: 1.2f, cdMax: 2.2f,
            blastRadius: 2f, blastDamage: 14f);

         EnsureT21EnemyPrefab("Enemy_GunTurret", EnemyKind.GunTurret, PrimitiveType.Sphere,
            new Color(1f, 0.62f, 0.15f, 1f), new Vector3(1.6f, 1.6f, 1.6f), null,
            enemyBullet, baseHealth: 60f, moveSpeed: 0f, wanderRadius: 1f,
            barrageCount: 3, spreadDeg: 24f, bulletSpeed: 8f, cdMin: 3f, cdMax: 4.5f,
            blastRadius: 2f, blastDamage: 14f);

         EnsureT21EnemyPrefab("Enemy_GunTurret_Heavy", EnemyKind.GunTurret, PrimitiveType.Cube,
            new Color(0.6f, 0.12f, 0.15f, 1f), new Vector3(2.1f, 2.1f, 2.1f), null,
            enemyBullet, baseHealth: 160f, moveSpeed: 0f, wanderRadius: 1f,
            barrageCount: 3, spreadDeg: 32f, bulletSpeed: 4f, cdMin: 4.5f, cdMax: 6.5f,
            blastRadius: 2f, blastDamage: 14f);

         EnsureT21EnemyPrefab("Enemy_Missile", EnemyKind.Missile, PrimitiveType.Capsule,
            new Color(1f, 0.85f, 0.15f, 1f), new Vector3(0.8f, 1.1f, 0.8f), blastRing,
            null, baseHealth: 20f, moveSpeed: 9f, wanderRadius: 1f,
            barrageCount: 1, spreadDeg: 0f, bulletSpeed: 0f, cdMin: 1f, cdMax: 1f,
            blastRadius: 1.8f, blastDamage: 12f);

         EnsureT21EnemyPrefab("Enemy_Mine", EnemyKind.Mine, PrimitiveType.Sphere,
            new Color(0.55f, 0.1f, 0.1f, 1f), new Vector3(1.3f, 0.7f, 1.3f), blastRing,
            null, baseHealth: 12f, moveSpeed: 0f, wanderRadius: 1f,
            barrageCount: 1, spreadDeg: 0f, bulletSpeed: 0f, cdMin: 1f, cdMax: 1f,
            blastRadius: 6f, blastDamage: 18f);
      }

      /// <summary>
      /// 生成单个敌人 prefab 资产（幂等）。灰盒外形 = 内置几何体 + 资产材质；组件 = BattleEnemyEntity。
      /// Mine 特殊结构：root(空,scale=1) + Body(网格/碰撞体子对象) + RangeRing(常驻感应圈子对象)。
      /// </summary>
      private static GameObject EnsureT21EnemyPrefab(string prefabName, EnemyKind kind, PrimitiveType primitive,
         Color color, Vector3 scale, GameObject blastRingPrefab, GameObject enemyBullet, float baseHealth,
         float moveSpeed, float wanderRadius, int barrageCount, float spreadDeg, float bulletSpeed,
         float cdMin, float cdMax, float blastRadius, float blastDamage)
      {
         var prefabPath = T21EnemyPrefabDir + "/" + prefabName + ".prefab";
         var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
         if (existing != null) return existing;

         var material = EnsureT21EnemyMaterialAsset(prefabName, color);

         GameObject root;
         Transform rangeRing = null;

         if (kind == EnemyKind.Mine)
         {
            // 地雷：根(scale=1) → Body(网格+碰撞体) + RangeRing(常驻感应圈，贴地)
            root = new GameObject(prefabName);

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
            body.transform.localScale = scale;

            var ringMaterial = AssetDatabase.LoadAssetAtPath<Material>(T21EnemyMaterialDir + "/Enemy_BlastRange.mat");
            var ring = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ring.name = "RangeRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, -0.68f, 0f); // 根 y=0.7 摆地面 → 环贴地
            if (ringMaterial != null)
            {
               ring.GetComponent<MeshRenderer>().sharedMaterial = ringMaterial;
            }

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider)
            {
               Object.DestroyImmediate(ringCollider);
            }

            root.transform.localScale = Vector3.one;
            rangeRing = ring.transform;
         }
         else
         {
            root = GameObject.CreatePrimitive(primitive);
            root.name = prefabName;
            root.transform.localScale = scale;
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
         }

         var entity = root.AddComponent<BattleEnemyEntity>();
         var serialized = new SerializedObject(entity);
         serialized.FindProperty("kind").enumValueIndex = (int)kind;
         serialized.FindProperty("baseHealth").floatValue = baseHealth;
         serialized.FindProperty("moveSpeed").floatValue = moveSpeed;
         serialized.FindProperty("wanderRadius").floatValue = wanderRadius;
         serialized.FindProperty("barrageCount").intValue = barrageCount;
         serialized.FindProperty("barrageSpreadDeg").floatValue = spreadDeg;
         serialized.FindProperty("enemyBulletPrefab").objectReferenceValue = enemyBullet;
         serialized.FindProperty("enemyBulletSpeed").floatValue = bulletSpeed;
         serialized.FindProperty("shotCooldownMin").floatValue = cdMin;
         serialized.FindProperty("shotCooldownMax").floatValue = cdMax;
         serialized.FindProperty("blastRadius").floatValue = blastRadius;
         serialized.FindProperty("blastDamage").floatValue = blastDamage;
         serialized.FindProperty("blastRingPrefab").objectReferenceValue = blastRingPrefab;
         if (rangeRing != null)
         {
            serialized.FindProperty("blastRangeRing").objectReferenceValue = rangeRing;
         }

         serialized.ApplyModifiedPropertiesWithoutUndo();

         var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
         Object.DestroyImmediate(root);
         if (saved == null)
         {
            Debug.LogError($"[T2.1] 敌人 prefab 生成失败：{prefabPath}");
         }

         return saved;
      }

      /// <summary>
      /// 生成/获取灰盒敌人材质资产（Assets/04_Prefabs/Enemy/Materials/）。
      /// </summary>
      private static Material EnsureT21EnemyMaterialAsset(string materialName, Color color)
      {
         var materialPath = T21EnemyMaterialDir + "/" + materialName + ".mat";
         var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
         if (existing != null) return existing;

         var shader = Shader.Find("Universal Render Pipeline/Lit");
         if (shader == null) shader = Shader.Find("Standard");

         var material = new Material(shader != null ? shader : Shader.Find("Diffuse"))
         {
            name = materialName,
            color = color
         };

         var directory = Path.GetDirectoryName(materialPath);
         if (!string.IsNullOrEmpty(directory))
         {
            Directory.CreateDirectory(directory);
         }

         AssetDatabase.CreateAsset(material, materialPath);
          return material;
       }

      /// <summary>
      /// 生成/获取爆炸范围提示环 prefab（Plane + 半透明红，无碰撞体）。
      /// 用途：Mine 常驻感应圈子对象（共享材质）+ Missile/Mine 爆炸瞬间 ShowBlastRing 池化提示。
      /// </summary>
      private static GameObject EnsureT21BlastRangePrefab()
      {
         const string prefabName = "Enemy_BlastRange";
         var prefabPath = T21EnemyPrefabDir + "/" + prefabName + ".prefab";
         var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
         var material = EnsureT21BlastMaterialAsset(prefabName);
         if (existing != null) return existing;

         var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
         plane.name = prefabName;
         plane.GetComponent<MeshRenderer>().sharedMaterial = material;

         var collider = plane.GetComponent<Collider>();
         if (collider)
         {
            Object.DestroyImmediate(collider);
         }

         var saved = PrefabUtility.SaveAsPrefabAsset(plane, prefabPath);
         Object.DestroyImmediate(plane);
         if (saved == null)
         {
            Debug.LogError($"[T2.1] 爆炸范围环 prefab 生成失败：{prefabPath}");
         }

         return saved;
      }

      /// <summary>
      /// 生成/获取爆炸范围环的半透明红材质资产。
      /// </summary>
      private static Material EnsureT21BlastMaterialAsset(string materialName)
      {
         var materialPath = T21EnemyMaterialDir + "/" + materialName + ".mat";
         var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
         if (existing != null) return existing;

         var shader = Shader.Find("Universal Render Pipeline/Lit");
         if (shader == null) shader = Shader.Find("Standard");

         var material = new Material(shader != null ? shader : Shader.Find("Diffuse"))
         {
            name = materialName,
            color = new Color(1f, 0.28f, 0.2f, 0.28f)
         };

         material.SetOverrideTag("RenderType", "Transparent");
         material.SetFloat("_Surface", 1f);   // URP/Lit 透明表面
         material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
         material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
         material.SetInt("_ZWrite", 0);
         material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
         material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
         material.renderQueue = (int)RenderQueue.Transparent;

         var directory = Path.GetDirectoryName(materialPath);
         if (!string.IsNullOrEmpty(directory))
         {
            Directory.CreateDirectory(directory);
         }

         AssetDatabase.CreateAsset(material, materialPath);
         return material;
      }

      /// <summary>
      /// 把演示敌人以 prefab 实例形式摆放到灰盒场景 Environment 下（重建工具管理的实例）。
      /// </summary>
      private static void PlaceT21DemoEnemies(Transform environment)
      {
         RemoveManagedDemoEnemies(environment);

         foreach (var entry in T21DemoLayout)
         {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(T21EnemyPrefabDir + "/" + entry.PrefabName + ".prefab");
            if (prefab == null)
            {
               Debug.LogWarning($"[T2.1] 缺少敌人 prefab：{entry.PrefabName}，请先运行 Thrud/Battle/T2.1 Create Enemy Prefabs。");
               continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, environment);
            instance.name = "Enemy_Graybox_" + entry.PrefabName + "_" + instance.GetInstanceID();
            instance.transform.position = entry.Position;
         }
      }

      /// <summary>
      /// 清理 Environment 下工具曾摆放的 Enemy_Graybox_* 敌人（primitive 或旧 prefab 实例）。
      /// </summary>
      private static void RemoveManagedDemoEnemies(Transform environment)
      {
         var managed = new List<GameObject>();
         for (var i = 0; i < environment.childCount; i++)
         {
            var child = environment.GetChild(i);
            if (child.name.StartsWith("Enemy_Graybox_", System.StringComparison.Ordinal))
            {
               managed.Add(child.gameObject);
            }
         }

         foreach (var go in managed)
         {
            Object.DestroyImmediate(go);
         }
      }

      // ===================== T2.2 区域波次与条件生成器（SO 配置方案） =====================
      // 菜单：Thrud/Battle/T2.2 Create Default Stage Assets
      // 一键创建/确保两张配置资产并摆放 BattleStageDirector：
      //   Assets/04_Prefabs/Enemy/EnemyCatalog.asset     —— 敌人 id ↔ prefab 映射表（字符串驱动，热更增敌只需追加条目）
      //   Assets/04_Prefabs/Enemy/StageConfig_Demo.asset —— 3 站示例关卡表（敌人字段在 Inspector 下拉重绘，底层存字符串）
      // 改配置 = 打开 StageConfig 资产编辑表格；场景目标点/刷怪由 Director 运行时自动生成。
      // 会清理旧的 Stage_T22_Demo 根与旧 BattleStageDirector 对象。

      private const string T22EnemyCatalogAssetPath = "Assets/04_Prefabs/Enemy/EnemyCatalog.asset";
      private const string T22StageConfigAssetPath = "Assets/04_Prefabs/Enemy/StageConfig_Demo.asset";
      private const string T22DirectorObjectName = "BattleStageDirector";

      [MenuItem("Thrud/Battle/T2.2 Create Default Stage Assets")]
      public static void CreateT22StageAssets()
      {
         var activeScene = EditorSceneManager.GetActiveScene();
         if (activeScene.path != ScenePath)
         {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            activeScene = EditorSceneManager.GetActiveScene();
         }

         var environment = GameObject.Find("Environment");
         if (environment == null)
         {
            Debug.LogError("[T2.2] 当前场景缺少 Environment 根节点，请先运行 Thrud/Battle/Setup BattleGraybox Scene 生成灰盒场景。");
            return;
         }

         // 确保 T2.1 敌人 prefab 资产存在（目录映射目标）
         var enemyBullet = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBulletPrefabPath);
         EnsureAllT21EnemyPrefabs(enemyBullet);

         // 清理旧演示结构（旧根与旧 Director 对象）
         DestroyObjectByName("Stage_T22_Demo");
         DestroyObjectByName(T22DirectorObjectName);

         var catalog = EnsureEnemyCatalogAsset();
         var stage = EnsureDefaultStageAsset(catalog);

         PlaceStageDirector(environment.transform, catalog, stage);

         EditorSceneManager.SaveScene(activeScene);
         AssetDatabase.Refresh();
         Debug.Log("[T2.2] 默认 Stage 资产已就绪并装配 Director。打开 StageConfig_Demo.asset 即可改表格（敌人字段为下拉）；Play 后目标点与刷怪按表自动生成。");
      }

      /// <summary>
      /// 确保 EnemyCatalog 资产存在（字符串 id ↔ prefab，含 T2.1 五类敌人）。
      /// </summary>
      private static EnemyCatalog EnsureEnemyCatalogAsset()
      {
         var existing = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(T22EnemyCatalogAssetPath);
         if (existing != null) return existing;

         var catalog = ScriptableObject.CreateInstance<EnemyCatalog>();
         catalog.Entries = new[]
         {
            new EnemyCatalogEntry { Id = "patrol", DisplayName = "巡弋者", Prefab = LoadT21EnemyPrefab("Enemy_Patrol") },
            new EnemyCatalogEntry { Id = "gun_turret", DisplayName = "站桩炮台", Prefab = LoadT21EnemyPrefab("Enemy_GunTurret") },
            new EnemyCatalogEntry { Id = "gun_turret_heavy", DisplayName = "重装炮台", Prefab = LoadT21EnemyPrefab("Enemy_GunTurret_Heavy") },
            new EnemyCatalogEntry { Id = "missile", DisplayName = "自爆·导弹", Prefab = LoadT21EnemyPrefab("Enemy_Missile") },
            new EnemyCatalogEntry { Id = "mine", DisplayName = "自爆·地雷", Prefab = LoadT21EnemyPrefab("Enemy_Mine") },
         };

         AssetDatabase.CreateAsset(catalog, T22EnemyCatalogAssetPath);
         AssetDatabase.SaveAssets();
         Debug.Log($"[T2.2] 已创建敌人目录：{T22EnemyCatalogAssetPath}");
         return catalog;
      }

      /// <summary>
      /// 确保默认 3 站 StageConfig 资产存在（演示节奏：到达刷首批 → 击杀80% 刷次批 → 清场进下一站）。
      /// </summary>
      private static BattleStageAsset EnsureDefaultStageAsset(EnemyCatalog catalog)
      {
         var existing = AssetDatabase.LoadAssetAtPath<BattleStageAsset>(T22StageConfigAssetPath);
         if (existing != null) return existing;

         var stage = ScriptableObject.CreateInstance<BattleStageAsset>();
         stage.TimeLimitSeconds = 180;
         stage.Sites = new[]
         {
            new BattleSiteRow
            {
               Note = "站1：巡弋×5 → 炮台",
               Position = new Vector2(0f, 14f),
               ArriveRadius = 8f,
               Batches = new[]
               {
                  new BattleBatchRow
                  {
                     Note = "首批(到达触发): 巡弋×5",
                     KillRatio = 0.8f,
                     Spawns = new[]
                     {
                        new BattleSpawnRow { EnemyId = "patrol", Count = 3, AngleDeg = -30f, Distance = 5.5f, Y = 1f, Scatter = 1.5f },
                        new BattleSpawnRow { EnemyId = "patrol", Count = 2, AngleDeg = 15f, Distance = 5.5f, Y = 1f, Scatter = 1.5f },
                     },
                  },
                  new BattleBatchRow
                  {
                     Note = "次批(击杀80%): 炮台×1",
                     KillRatio = 0.8f,
                     Spawns = new[]
                     {
                        new BattleSpawnRow { EnemyId = "gun_turret", Count = 1, AngleDeg = -40f, Distance = 6.5f, Y = 1f, Scatter = 1f },
                     },
                  },
               },
            },
            new BattleSiteRow
            {
               Note = "站2：巡弋+炮台 → 导弹+巡弋",
               Position = new Vector2(-12f, 28f),
               ArriveRadius = 8f,
               Batches = new[]
               {
                  new BattleBatchRow
                  {
                     Note = "首批: 巡弋×4+炮台×1",
                     KillRatio = 0.8f,
                     Spawns = new[]
                     {
                        new BattleSpawnRow { EnemyId = "patrol", Count = 4, AngleDeg = 0f, Distance = 4.5f, Y = 1f, Scatter = 1.5f },
                        new BattleSpawnRow { EnemyId = "gun_turret", Count = 1, AngleDeg = -90f, Distance = 5.5f, Y = 1f, Scatter = 1f },
                     },
                  },
                  new BattleBatchRow
                  {
                     Note = "次批(击杀80%): 导弹×1+巡弋×2",
                     KillRatio = 0.8f,
                     Spawns = new[]
                     {
                        new BattleSpawnRow { EnemyId = "missile", Count = 1, AngleDeg = 30f, Distance = 6.5f, Y = 1f, Scatter = 1f },
                        new BattleSpawnRow { EnemyId = "patrol", Count = 2, AngleDeg = 0f, Distance = 7f, Y = 1f, Scatter = 1.5f },
                     },
                  },
               },
            },
            new BattleSiteRow
            {
               Note = "站3：巡弋+重装 → 地雷+导弹（收尾）",
               Position = new Vector2(8f, 42f),
               ArriveRadius = 8f,
               Batches = new[]
               {
                  new BattleBatchRow
                  {
                     Note = "首批: 巡弋×4+重装×1",
                     KillRatio = 0.8f,
                     Spawns = new[]
                     {
                        new BattleSpawnRow { EnemyId = "patrol", Count = 4, AngleDeg = 0f, Distance = 4.5f, Y = 1f, Scatter = 1.5f },
                        new BattleSpawnRow { EnemyId = "gun_turret_heavy", Count = 1, AngleDeg = -80f, Distance = 5.5f, Y = 1f, Scatter = 1f },
                     },
                  },
                  new BattleBatchRow
                  {
                     Note = "次批(击杀80%): 地雷×1+导弹×1",
                     KillRatio = 0.8f,
                     Spawns = new[]
                     {
                        new BattleSpawnRow { EnemyId = "mine", Count = 1, AngleDeg = 20f, Distance = 5.5f, Y = 0.7f, Scatter = 0.5f },
                        new BattleSpawnRow { EnemyId = "missile", Count = 1, AngleDeg = 10f, Distance = 6f, Y = 1f, Scatter = 0.5f },
                     },
                  },
               },
            },
         };

         AssetDatabase.CreateAsset(stage, T22StageConfigAssetPath);
         AssetDatabase.SaveAssets();
         Debug.Log($"[T2.2] 已创建默认 3 站关卡表：{T22StageConfigAssetPath}");
         return stage;
      }

      /// <summary>
      /// 在场景放置/装配 BattleStageDirector（引用目录与关卡表；目标点由运行时自动生成）。
      /// </summary>
      private static void PlaceStageDirector(Transform environment, EnemyCatalog catalog, BattleStageAsset stage)
      {
         var directorObject = new GameObject(T22DirectorObjectName);
         directorObject.transform.SetParent(environment, false);

         var director = directorObject.AddComponent<BattleStageDirector>();
         var serialized = new SerializedObject(director);
         serialized.FindProperty("config").objectReferenceValue = stage;
         serialized.FindProperty("catalog").objectReferenceValue = catalog;
         serialized.ApplyModifiedPropertiesWithoutUndo();
      }

      private static GameObject LoadT21EnemyPrefab(string prefabName)
      {
         return AssetDatabase.LoadAssetAtPath<GameObject>(T21EnemyPrefabDir + "/" + prefabName + ".prefab");
      }

      /// <summary>
      /// 按名称销毁场景内第一个同名 GameObject（幂等清理用）。
      /// </summary>
      private static void DestroyObjectByName(string objectName)
      {
         var target = GameObject.Find(objectName);
         if (target != null)
         {
            Object.DestroyImmediate(target);
         }
      }
   }
}
