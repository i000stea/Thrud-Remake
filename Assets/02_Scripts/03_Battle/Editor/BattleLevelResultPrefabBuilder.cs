using TeaGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// T3.1 战斗 UI 面板一键生成工具（QFramework UIKit 面板）。
   /// 生成两个标准 UIPanel prefab 到 Resources（Assets/Resources/Panels/），
   /// 由 UIKit.OpenPanel 在运行时按 "Panels/xxx" 加载并挂到 UIRoot（PopUI 层）。
   /// 不需要任何场景绑定：UIKit 会自动创建/复用 QFramework UIRoot。
   /// 用法：TeaGame → Battle → Rebuild Battle UI Prefabs（可重复执行覆盖重建）。
   /// </summary>
   public static class BattleUiPrefabBuilder
   {
      private const string ResultPrefabPath = "Assets/Resources/Panels/LevelResultPanel.prefab";
      private const string PausePrefabPath = "Assets/Resources/Panels/PauseMenuPanel.prefab";
      private const string ResultFolder = "Assets/Resources/Panels";

      /// <summary>旧版产物（04_Prefabs/UI 下，T3.1 解耦前的 UI 面板），迁移时清理。</summary>
      private const string LegacyResultPrefabPath = "Assets/04_Prefabs/UI/LevelResultPanel.prefab";

      private static Font sUiFont;

      [MenuItem("TeaGame/Battle/Rebuild Battle UI Prefabs")]
      public static void RebuildAll()
      {
         EnsureFolderExists(ResultFolder);
         DeleteAssetIfExists(LegacyResultPrefabPath);
         RebuildResultPrefab();
         RebuildPauseMenuPrefab();
      }

      [MenuItem("TeaGame/Battle/Rebuild LevelResultPanel Prefab")]
      public static void RebuildResultPrefab()
      {
         EnsureFolderExists(ResultFolder);
         DeleteAssetIfExists(ResultPrefabPath);

         var root = BuildResultHierarchy();
         if (root == null)
         {
            Debug.LogError("[T3.1] LevelResultPanel 构建失败：BattleLevelResultPanel 未挂载（请确认脚本已编译）。");
            return;
         }

         root.SetActive(false);
         var prefab = PrefabUtility.SaveAsPrefabAsset(root, ResultPrefabPath);
         Object.DestroyImmediate(root);

         if (prefab == null)
         {
            Debug.LogError($"[T3.1] 保存 {ResultPrefabPath} 失败。");
            return;
         }

         Debug.Log($"[T3.1] 已重建结算面板：{ResultPrefabPath}（UIKit.OpenPanel 加载名 Panels/LevelResultPanel）。");
      }

      [MenuItem("TeaGame/Battle/Rebuild PauseMenuPrefab")]
      public static void RebuildPauseMenuPrefab()
      {
         EnsureFolderExists(ResultFolder);
         DeleteAssetIfExists(PausePrefabPath);

         var root = BuildPauseHierarchy();
         if (root == null)
         {
            Debug.LogError("[T3.1] PauseMenuPanel 构建失败：BattlePauseMenuPanel 未挂载（请确认脚本已编译）。");
            return;
         }

         root.SetActive(false);
         var prefab = PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
         Object.DestroyImmediate(root);

         if (prefab == null)
         {
            Debug.LogError($"[T3.1] 保存 {PausePrefabPath} 失败。");
            return;
         }

         Debug.Log($"[T3.1] 已重建暂停菜单：{PausePrefabPath}（UIKit.OpenPanel 加载名 Panels/PauseMenuPanel）。");
      }

      // ---------------- 结算面板 ----------------

      private static GameObject BuildResultHierarchy()
      {
         var root = CreateRect("LevelResultPanel", null);
         SetStretch(root);
         var overlayImage = root.gameObject.AddComponent<Image>();
         overlayImage.color = new Color(0f, 0f, 0f, 0.55f);

         var panel = root.gameObject.AddComponent<BattleLevelResultPanel>();
         if (panel == null) return null;

         var panelRect = CreateRect("ResultPanel", root);
         SetCentered(panelRect, new Vector2(0f, 0f), new Vector2(520f, 300f));
         var panelImage = panelRect.gameObject.AddComponent<Image>();
         panelImage.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

         var title = CreateRect("ResultTitle", panelRect);
         SetCentered(title, new Vector2(0f, 95f), new Vector2(480f, 70f));
         var titleText = title.gameObject.AddComponent<Text>();
         ApplyText(titleText, string.Empty, 42);
         titleText.raycastTarget = false;

         var restart = CreateButton(panelRect, "RestartButton", new Vector2(0f, -95f), "再来一次");

         var serializedPanel = new SerializedObject(panel);
         serializedPanel.FindProperty("titleText").objectReferenceValue = titleText;
         serializedPanel.FindProperty("restartButton").objectReferenceValue = restart;
         serializedPanel.ApplyModifiedPropertiesWithoutUndo();

         return root.gameObject;
      }

      // ---------------- 暂停面板 ----------------

      private static GameObject BuildPauseHierarchy()
      {
         var root = CreateRect("PauseMenuPanel", null);
         SetStretch(root);
         var overlayImage = root.gameObject.AddComponent<Image>();
         overlayImage.color = new Color(0f, 0f, 0f, 0.55f);

         var panel = root.gameObject.AddComponent<BattlePauseMenuPanel>();
         if (panel == null) return null;

         var panelRect = CreateRect("PausePanel", root);
         SetCentered(panelRect, new Vector2(0f, 0f), new Vector2(420f, 300f));
         var panelImage = panelRect.gameObject.AddComponent<Image>();
         panelImage.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

         var title = CreateRect("PauseTitle", panelRect);
         SetCentered(title, new Vector2(0f, 95f), new Vector2(360f, 60f));
         var titleText = title.gameObject.AddComponent<Text>();
         ApplyText(titleText, "暂停", 40);
         titleText.raycastTarget = false;

         var resume = CreateButton(panelRect, "ResumeButton", new Vector2(0f, -40f), "继续");
         var restart = CreateButton(panelRect, "RestartButton", new Vector2(0f, -120f), "重开本关");

         var serializedPanel = new SerializedObject(panel);
         serializedPanel.FindProperty("resumeButton").objectReferenceValue = resume;
         serializedPanel.FindProperty("restartButton").objectReferenceValue = restart;
         serializedPanel.ApplyModifiedPropertiesWithoutUndo();

         return root.gameObject;
      }

      // ---------------- 工具 ----------------

      /// <summary>
      /// 确保资产目录存在（SaveAsPrefabAsset 不会自动建目录）。
      /// 逐级创建 Assets/Resources/Panels（Assets 下任意层级缺失都会补建）。
      /// </summary>
      private static void EnsureFolderExists(string assetFolderPath)
      {
         if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

         var segments = assetFolderPath.Split('/');
         var current = segments[0]; // Assets
         for (var i = 1; i < segments.Length; i++)
         {
            var next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
               AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
         }

         AssetDatabase.Refresh();
      }

      private static void DeleteAssetIfExists(string path)
      {
         if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
         {
            AssetDatabase.DeleteAsset(path);
         }
      }

      private static RectTransform CreateRect(string name, Transform parent)
      {
         var go = new GameObject(name, typeof(RectTransform));
         var rect = go.GetComponent<RectTransform>();
         if (parent != null)
         {
            rect.SetParent(parent, false);
         }

         return rect;
      }

      private static void SetStretch(RectTransform rect)
      {
         rect.anchorMin = Vector2.zero;
         rect.anchorMax = Vector2.one;
         rect.offsetMin = Vector2.zero;
         rect.offsetMax = Vector2.zero;
      }

      private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
      {
         rect.anchorMin = new Vector2(0.5f, 0.5f);
         rect.anchorMax = new Vector2(0.5f, 0.5f);
         rect.pivot = new Vector2(0.5f, 0.5f);
         rect.sizeDelta = size;
         rect.anchoredPosition = position;
      }

      private static Button CreateButton(RectTransform parent, string name, Vector2 position, string label)
      {
         var button = CreateRect(name, parent);
         SetCentered(button, position, new Vector2(240f, 60f));
         var image = button.gameObject.AddComponent<Image>();
         image.color = new Color(0.2f, 0.45f, 0.9f, 1f);

         var buttonComponent = button.gameObject.AddComponent<Button>();
         buttonComponent.targetGraphic = image;

         var textRect = CreateRect("Text", button);
         SetStretch(textRect);
         var text = textRect.gameObject.AddComponent<Text>();
         ApplyText(text, label, 26);
         text.raycastTarget = false;

         return buttonComponent;
      }

      private static void ApplyText(Text text, string content, int fontSize)
      {
         if (sUiFont == null)
         {
            sUiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
         }

         text.font = sUiFont;
         text.text = content;
         text.fontSize = fontSize;
         text.alignment = TextAnchor.MiddleCenter;
         text.color = Color.white;
         text.horizontalOverflow = HorizontalWrapMode.Overflow;
         text.verticalOverflow = VerticalWrapMode.Overflow;
      }
   }
}
