using System.IO;
using UnityEngine.SceneManagement;

namespace TeaGame.App
{
   /// <summary>
   /// 入口场景锚点。
   /// 常驻管理场景（ConfigScene）是 Build Settings[0]，应用入口。
   /// 其名称/路径从 Build Settings 动态获取，**不写死**：
   /// 场景重命名/移动后，此处自动跟随，代码零改动。
   /// </summary>
   public static class SceneAnchor
   {
      /// <summary>
      /// 获取入口场景路径（Build Settings[0]）。
      /// </summary>
      public static string GetEntryScenePath()
      {
         if (SceneManager.sceneCountInBuildSettings <= 0)
         {
            return null;
         }

         return SceneUtility.GetScenePathByBuildIndex(0);
      }

      /// <summary>
      /// 获取入口场景名称（由路径推导）。
      /// </summary>
      public static string GetEntrySceneName()
      {
         var path = GetEntryScenePath();
         return string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);
      }

      /// <summary>
      /// 检查入口场景是否已加载。
      /// </summary>
      public static bool IsEntrySceneLoaded()
      {
         var entrySceneName = GetEntrySceneName();
         if (string.IsNullOrEmpty(entrySceneName))
         {
            return false;
         }

         for (var i = 0; i < SceneManager.sceneCount; i++)
         {
            if (SceneManager.GetSceneAt(i).name == entrySceneName)
            {
               return true;
            }
         }

         return false;
      }
   }
}
