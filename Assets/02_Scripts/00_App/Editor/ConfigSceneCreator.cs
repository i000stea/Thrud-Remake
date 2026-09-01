using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeaGame.App.Editor
{
   /// <summary>
   /// ConfigScene 一键创建工具（C1+C2）。
   /// 菜单：Thrud/Setup ConfigScene
   /// 执行内容：
   ///   1. 确保 SceneRefTable 资产存在并已填充
   ///   2. 新建空场景并命名为 ConfigScene
   ///   3. 创建 GameRoot 并挂载 AppBootstrapper + SceneFlow（注入场景引用表）
   ///   4. 实例化 LoadingGroup 预制件并接给 SceneFlow
   ///   5. 保存场景到 Assets/01_Scenes/ConfigScene.unity
   ///   6. 同步 Build Settings（ConfigScene 置 [0]）
   /// 由编辑器执行生成，避免手写场景 YAML 的序列化风险。
   /// 场景名称/路径统一走 SceneRefTable（GUID 锚定），不写死。
   /// </summary>
   public static class ConfigSceneCreator
   {
      private const string ScenePath = "Assets/01_Scenes/ConfigScene.unity";
      private const string LoadingGroupPrefabPath = "Assets/Resources/Prefabs/LoadingGroup.prefab";

      [MenuItem("Thrud/Setup ConfigScene")]
      public static void SetupConfigScene()
      {
         // 0. 确保场景引用表就绪并填充
         var table = SceneRefTableCreator.EnsureAsset();
         SceneRefTableCreator.Populate(table);
         EditorUtility.SetDirty(table);
         AssetDatabase.SaveAssets();

         // 1. 新建空场景
         var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
         scene.name = "ConfigScene";

         // 2. 创建 GameRoot 并挂载启动组件
         var root = new GameObject("GameRoot");
         var bootstrapper = root.AddComponent<AppBootstrapper>();
         var sceneFlow = root.AddComponent<SceneFlow>();

         // 注入场景引用表到组件（私有序列化字段经 SerializedObject 赋值）
         AssignSceneRefTable(bootstrapper, table);
         AssignSceneRefTable(sceneFlow, table);

         // 3. 实例化 LoadingGroup 预制件并接给 SceneFlow
         var loadingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingGroupPrefabPath);
         if (loadingPrefab != null)
         {
            var loadingInstance = (GameObject)PrefabUtility.InstantiatePrefab(loadingPrefab, scene);
            sceneFlow.LoadingGroupRoot = loadingInstance.transform;
         }
         else
         {
            Debug.LogWarning($"[ConfigSceneCreator] 未找到 LoadingGroup 预制件：{LoadingGroupPrefabPath}，SceneFlow 将在运行时回退 Resources 加载。");
         }

         // 4. 保存场景
         var directory = Path.GetDirectoryName(ScenePath);
         if (!string.IsNullOrEmpty(directory))
         {
            Directory.CreateDirectory(directory);
         }

         EditorSceneManager.SaveScene(scene, ScenePath);

         // 5. 同步 Build Settings（ConfigScene 置 [0]，表内场景全部加入）
         SceneRefTableCreator.SyncBuildSettings(table);

         // 6. 打开场景并提示
         EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
         AssetDatabase.Refresh();
         Debug.Log($"[ConfigSceneCreator] 创建完成：{ScenePath}，已设为 Build Settings[0]。可直接 Play 验证启动链路。");
      }

      /// <summary>
      /// 通过 SerializedObject 为组件的私有 sceneRefTable 字段赋值。
      /// </summary>
      private static void AssignSceneRefTable(Object component, SceneRefTable table)
      {
         var serialized = new SerializedObject(component);
         var property = serialized.FindProperty("sceneRefTable");
         if (property != null)
         {
            property.objectReferenceValue = table;
            serialized.ApplyModifiedPropertiesWithoutUndo();
         }
      }
   }
}
