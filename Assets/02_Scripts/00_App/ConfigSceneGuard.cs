using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame.App
{
   /// <summary>
   /// 入口常驻场景懒加载守卫。
   /// 需求语义（决策 D16）：检查"管理场景是否已加载"，未加载则 additive 加载。
   /// 入口场景经 <see cref="SceneAnchor"/> 从 Build Settings[0] 动态锚定，名称/路径不写死。
   /// 运行时正常路径入口场景必然已加载；编辑器直接 Play 玩法场景时自动兜底加载。
   /// </summary>
   public static class ConfigSceneGuard
   {
      /// <summary>
      /// 检查入口常驻场景是否已加载。
      /// </summary>
      public static bool IsEntrySceneLoaded()
      {
         return SceneAnchor.IsEntrySceneLoaded();
      }

#if UNITY_EDITOR
      /// <summary>
      /// 编辑器守卫：直接 Play 玩法场景（非入口场景）时自动 additive 加载入口常驻场景。
      /// 仅在编辑器生效；打包后入口场景为 Build Settings[0]，必然已加载，本守卫不触发。
      /// </summary>
      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
      private static void AutoLoadEntrySceneInEditor()
      {
         if (!Application.isPlaying) return;

         var entrySceneName = SceneAnchor.GetEntrySceneName();
         if (string.IsNullOrEmpty(entrySceneName))
         {
            Debug.LogWarning("[ConfigSceneGuard] Build Settings 为空或入口场景无效，跳过自动加载。请在编辑器执行 Thrud/Setup ConfigScene。");
            return;
         }

         var active = SceneManager.GetActiveScene();
         if (active.name == entrySceneName) return;
         if (SceneAnchor.IsEntrySceneLoaded()) return;

         var entryScenePath = SceneAnchor.GetEntryScenePath();
         if (!SceneInBuildSettings(entryScenePath))
         {
            Debug.LogWarning(
               $"[ConfigSceneGuard] 入口场景未加入 Build Settings，已跳过自动加载。" +
               $"请在编辑器执行菜单 Thrud/Setup ConfigScene 完成初始化。");
            return;
         }

         SceneManager.LoadScene(entryScenePath, LoadSceneMode.Additive);
         Debug.Log($"[ConfigSceneGuard] 已自动加载入口常驻场景：{entrySceneName}");
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
