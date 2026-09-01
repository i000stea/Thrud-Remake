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
   }
}
