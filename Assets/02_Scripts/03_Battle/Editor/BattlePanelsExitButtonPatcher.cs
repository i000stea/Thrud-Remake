using TeaGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// T3.1A：为现有两个战斗 UI prefab（CodeGen 绑定产物）幂等追加「退出」按钮。
   /// 不重建 prefab、不重命名资源，保留用户对 prefab 的所有权：
   /// - 暂停面板 BattlePauseMenuPanel.prefab：按钮集 = 继续游戏 / 重新开始 / 退出（文本同步改名）；
   /// - 结算面板 BattleLevelResultPanel.prefab：按钮集 = 再来一次 / 退出。
   /// 按钮复制"重新开始"按钮外观，清空其 onClick（避免带入重开逻辑），再绑定到面板新增的 exitButton 字段。
   /// 用法：TeaGame → Battle → Patch Battle Panels (加退出按钮)（可重复执行）。
   /// </summary>
   public static class BattlePanelsExitButtonPatcher
   {
      private const string PausePath = "Assets/Resources/Panels/BattlePauseMenuPanel.prefab";
      private const string ResultPath = "Assets/Resources/Panels/BattleLevelResultPanel.prefab";

      [MenuItem("TeaGame/Battle/Patch Battle Panels (加退出按钮)")]
      public static void PatchAll()
      {
         var pausePatched = PatchPause();
         var resultPatched = PatchResult();
         AssetDatabase.SaveAssets();
         AssetDatabase.Refresh();

         Debug.Log($"[T3.1A] 面板补丁完成：暂停={pausePatched}，结算={resultPatched}。" +
                   "若为 False：请确认已重编译（exitButton 字段存在）且 prefab 未改名。");
      }

      private static bool PatchPause()
      {
         var ok = PatchSingle(PausePath,
            out var panel,
            pause: true);
         if (!ok || panel == null) return false;

         var so = new SerializedObject(panel);
         // 文案对齐 T3.1A：继续游戏 / 重新开始 / 退出
         SetButtonText(so.FindProperty("resumeButton"), "继续游戏");
         SetButtonText(so.FindProperty("restartButton"), "重新开始");
         so.ApplyModifiedPropertiesWithoutUndo();
         return true;
      }

      private static bool PatchResult()
      {
         var ok = PatchSingle(ResultPath,
            out var panel,
            pause: false);
         return ok && panel != null;
      }

      /// <summary>
      /// 通用补丁：删除旧 ExitButton → 复制 RestartButton → 清 onClick → 下移一行 → 改文本"退出" → 绑定 exitButton。
      /// </summary>
      private static bool PatchSingle(string prefabPath, out Component panel, bool pause)
      {
         panel = null;
         if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
         {
            Debug.LogError($"[T3.1A] 未找到 prefab：{prefabPath}");
            return false;
         }

         var root = PrefabUtility.LoadPrefabContents(prefabPath);
         try
         {
            panel = pause
               ? (Component)root.GetComponentInChildren<BattlePauseMenuPanel>(true)
               : root.GetComponentInChildren<BattleLevelResultPanel>(true);

            if (panel == null)
            {
               Debug.LogError($"[T3.1A] {prefabPath} 未挂载对应 UIPanel 组件，中止补丁。");
               return false;
            }

            var so = new SerializedObject(panel);
            var restartRef = so.FindProperty("restartButton")?.objectReferenceValue as Button;
            if (restartRef == null)
            {
               Debug.LogError($"[T3.1A] {prefabPath} 缺少 restartButton 引用，中止补丁。");
               return false;
            }

            var column = restartRef.transform.parent;
            var existing = column.Find("ExitButton");
            if (existing != null)
            {
               Object.DestroyImmediate(existing.gameObject);
            }

            // 复制"重新开始"按钮外观
            var exitGo = (GameObject)Object.Instantiate(restartRef.gameObject, column, false);
            exitGo.name = "ExitButton";

            // 清空 onClick 持久化监听（避免带入重开逻辑）
            var exitButton = exitGo.GetComponent<Button>();
            var exitSo = new SerializedObject(exitButton);
            var onClick = exitSo.FindProperty("m_OnClick");
            if (onClick != null)
            {
               onClick.ClearArray();
               exitSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 无布局容器时下移一行，避免与"重新开始"重叠
            var layout = column.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (layout == null)
            {
               var exitRect = exitGo.GetComponent<RectTransform>();
               var anchorRect = restartRef.GetComponent<RectTransform>();
               exitRect.anchoredPosition = anchorRect.anchoredPosition + new Vector2(0f, -90f);
            }
            else
            {
               exitGo.transform.SetSiblingIndex(restartRef.transform.GetSiblingIndex() + 1);
            }

            var label = exitGo.GetComponentInChildren<Text>(true);
            if (label != null)
            {
               label.text = "退出";
            }

            so.FindProperty("exitButton").objectReferenceValue = exitButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[T3.1A] 已补丁：{prefabPath}（ExitButton + exitButton 绑定）。" +
                      "若按钮重叠/位置不佳，请在 prefab 中手动微调 RectTransform。");
            return true;
         }
         finally
         {
            PrefabUtility.UnloadPrefabContents(root);
         }
      }

      private static void SetButtonText(SerializedProperty buttonProp, string text)
      {
         if (buttonProp?.objectReferenceValue is Button button)
         {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
               label.text = text;
            }
         }
      }
   }
}
