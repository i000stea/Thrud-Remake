using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame.App
{
   /// <summary>
   /// ConfigScene 懒加载守卫。
   /// 需求语义（决策 D16）：检查"管理场景是否已加载"，未加载则 additive 加载。
   /// 运行时正常路径 ConfigScene 为入口必然已加载；编辑器直接 Play 玩法场景时自动兜底加载。
   /// </summary>
   public static class ConfigSceneGuard
   {
      /// <summary>
      /// 检查 ConfigScene 是否已加载（按名称匹配，避免路径差异）。
      /// </summary>
      public static bool IsLoaded()
      {
         for (var i = 0; i < SceneManager.sceneCount; i++)
         {
            if (SceneManager.GetSceneAt(i).name == SceneNames.ConfigScene)
            {
               return true;
            }
         }

         return false;
      }

#if UNITY_EDITOR
      /// <summary>
      /// 编辑器守卫：直接 Play 玩法场景（非 ConfigScene）时自动 additive 加载 ConfigScene。
      /// 仅在编辑器生效；打包后 ConfigScene 为入口场景，本守卫不触发。
      /// </summary>
      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
      private static void AutoLoadConfigSceneInEditor()
      {
         if (!Application.isPlaying) return;

         var active = SceneManager.GetActiveScene();
         if (active.name == SceneNames.ConfigScene) return;
         if (IsLoaded()) return;

         if (!SceneInBuildSettings(SceneNames.ConfigScenePath))
         {
            Debug.LogWarning(
               $"[ConfigSceneGuard] ConfigScene 未加入 Build Settings，已跳过自动加载。" +
               $"请在编辑器执行菜单 Thrud/Setup ConfigScene 完成初始化。");
            return;
         }

         SceneManager.LoadScene(SceneNames.ConfigScenePath, LoadSceneMode.Additive);
         Debug.Log("[ConfigSceneGuard] 已自动加载 ConfigScene（常驻管理场景）。");
      }

      /// <summary>
      /// 检查场景是否在 Build Settings 中且启用。
      /// </summary>
      private static bool SceneInBuildSettings(string path)
      {
         foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
         {
            if (scene.enabled && scene.path == path)
            {
               return true;
            }
         }

         return false;
      }
#endif
   }
}
