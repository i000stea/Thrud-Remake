using System;
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
      /// <param name="onComplete">过渡完成回调（T3.1C Runner 装配用）。</param>
      public static void TransitionTo(string sceneName, Action onComplete = null)
      {
         if (string.IsNullOrEmpty(sceneName))
         {
            Debug.LogError("[BattleSceneGate] 目标场景名为空，无法过渡。");
            return;
         }

         // P1（2026-09-10）：场景即将切换 → 先安全收敛面板表。
         // 若只关场景不关面板，面板 GameObject 会随场景销毁、但 UIKit 表残留"已销毁条目"
         //（IPanel 接口判空不触发 Unity 判空），下次进战斗 OpenPanel 复用该实例即抛
         // MissingReferenceException —— 表现为"战斗面板完全不出现"。此处是"离场必清面板"的
         // 唯一强制点，覆盖此前漏掉的「重开本关」（BattleLevelFlow.Restart → ReloadCurrent）。
         UiPanelFlow.CloseAllSafely();

         var flow = SceneFlow.Instance;
         if (flow == null)
         {
            Debug.LogWarning($"[BattleSceneGate] SceneFlow 不可用（0ConfigScene 未常驻），回退直接加载场景：{sceneName}");
            SceneManager.LoadScene(sceneName);
            onComplete?.Invoke();
            return;
         }

         flow.RequestLoad(BuildRequest(sceneName, onComplete));
      }

      /// <summary>
      /// 原地重载（重开本关）：先卸载旧玩法场景再加载目标，经 SceneFlow 加载遮罩过渡。
      /// 避免「目标=当前战斗场景」时新旧同场景实例并存导致状态/死亡标记/HUD 错乱。
      /// SceneFlow 不可用时回退旧式直载。
      /// </summary>
      public static void ReloadCurrent(string sceneName, Action onComplete = null)
      {
         if (string.IsNullOrEmpty(sceneName))
         {
            Debug.LogError("[BattleSceneGate] 目标场景名为空，无法重载。");
            return;
         }

         // P1：重开本关同样先收敛面板表（触摸层/暂停面板随旧场景销毁后不得残留在 UIKit 表里）
         UiPanelFlow.CloseAllSafely();

         var flow = SceneFlow.Instance;
         if (flow == null)
         {
            Debug.LogWarning($"[BattleSceneGate] SceneFlow 不可用（0ConfigScene 未常驻），回退直接加载场景：{sceneName}");
            SceneManager.LoadScene(sceneName);
            onComplete?.Invoke();
            return;
         }

         var request = BuildRequest(sceneName, onComplete);
         request.UnloadFirst = true;
         flow.RequestLoad(request);
      }

      /// <summary>
      /// 构造带遮罩的切换请求。
      ///
      /// 必须经 <see cref="SceneCatalog.CreateRequest"/> 而不是直接 new：主菜单已移出 Build Settings
      /// 改走场景 AssetBundle，而本类的调用点里既有「进战斗」（Build Settings 场景）也有
      /// 「回主菜单」（AssetBundle 场景，来自 <see cref="BattleTeamSession.ReturnSceneName"/>）。
      /// 由场景目录决定通路，回跳路径才不会因为场景在不在 Build Settings 而时好时坏。
      /// </summary>
      private static SceneRequest BuildRequest(string sceneName, Action onComplete)
      {
         var request = SceneCatalog.CreateRequest(sceneName);
         request.ShowLoading = true;
         request.OnComplete = onComplete;
         return request;
      }
   }
}
