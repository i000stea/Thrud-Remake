using TeaGame.App;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame
{
   /// <summary>
   /// 战斗场景进出统一过渡入口（v1.3）。
   /// 「进入 Battle」与「离开 Battle」都经全局 <see cref="SceneFlow"/> 切换，
   /// 由 0ConfigScene 的 LoadingGroup 加载遮罩过渡（渐显 → 预载 → 激活 → 渐隐）。
   /// SceneFlow 不可用时（编辑器单场景直跑、入口场景未挂载）回退旧式直载，避免死路径。
   /// </summary>
   public static class BattleSceneGate
   {
      /// <summary>
      /// 请求带加载遮罩过渡到目标场景。
      /// </summary>
      /// <param name="sceneName">目标场景名（场景 key，决策 D25）。</param>
      public static void TransitionTo(string sceneName)
      {
         if (string.IsNullOrEmpty(sceneName))
         {
            Debug.LogError("[BattleSceneGate] 目标场景名为空，无法过渡。");
            return;
         }

         var flow = SceneFlow.Instance;
         if (flow == null)
         {
            Debug.LogWarning($"[BattleSceneGate] SceneFlow 不可用（0ConfigScene 未常驻），回退直接加载场景：{sceneName}");
            SceneManager.LoadScene(sceneName);
            return;
         }

         flow.RequestLoad(new SceneRequest { SceneName = sceneName, ShowLoading = true });
      }

      /// <summary>
      /// 原地重载（重开本关）：先卸载旧玩法场景再加载目标，经 SceneFlow 加载遮罩过渡。
      /// 避免「目标=当前战斗场景」时新旧同场景实例并存导致状态/死亡标记/HUD 错乱。
      /// SceneFlow 不可用时回退旧式直载。
      /// </summary>
      public static void ReloadCurrent(string sceneName)
      {
         if (string.IsNullOrEmpty(sceneName))
         {
            Debug.LogError("[BattleSceneGate] 目标场景名为空，无法重载。");
            return;
         }

         var flow = SceneFlow.Instance;
         if (flow == null)
         {
            Debug.LogWarning($"[BattleSceneGate] SceneFlow 不可用（0ConfigScene 未常驻），回退直接加载场景：{sceneName}");
            SceneManager.LoadScene(sceneName);
            return;
         }

         flow.RequestLoad(new SceneRequest
         {
            SceneName = sceneName,
            ShowLoading = true,
            UnloadFirst = true,
         });
      }
   }
}
