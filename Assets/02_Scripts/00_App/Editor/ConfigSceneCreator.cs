using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeaGame.App.Editor
{
   /// <summary>
   /// ConfigScene 一键创建工具（C1+C2）。
   /// 菜单：Thrud/Setup ConfigScene
   /// 执行内容：
   ///   1. 新建空场景并命名为 ConfigScene
   ///   2. 创建 GameRoot 并挂载 AppBootstrapper + SceneFlow
   ///   3. 实例化 LoadingGroup 预制件并接给 SceneFlow
   ///   4. 保存场景到 Assets/01_Scenes/ConfigScene.unity
   ///   5. 置入 Build Settings[0]
   /// 由编辑器执行生成，避免手写场景 YAML 的序列化风险。
   /// </summary>
   public static class ConfigSceneCreator
   {
      private const string ScenePath = "Assets/01_Scenes/ConfigScene.unity";
      private const string LoadingGroupPrefabPath = "Assets/Resources/Prefabs/LoadingGroup.prefab";

      [MenuItem("Thrud/Setup ConfigScene")]
      public static void SetupConfigScene()
      {
         // 1. 新建空场景
         var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
         scene.name = SceneNames.ConfigScene;

         // 2. 创建 GameRoot 并挂载启动组件
         var root = new GameObject("GameRoot");
         root.AddComponent<AppBootstrapper>();
         var sceneFlow = root.AddComponent<SceneFlow>();

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

         // 5. 置入 Build Settings 首位（应用入口）
         var buildScenes = EditorBuildSettings.scenes.ToList();
         buildScenes.RemoveAll(s => s.path == ScenePath);
         buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
         EditorBuildSettings.scenes = buildScenes.ToArray();

         // 6. 打开场景并提示
         EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
         AssetDatabase.Refresh();
         Debug.Log($"[ConfigSceneCreator] 创建完成：{ScenePath}，已设为 Build Settings[0]。可直接 Play 验证启动链路。");
      }
   }
}
