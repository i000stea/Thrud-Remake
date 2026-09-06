using TeaGame.Battle.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// T3.1C 步骤7：创建 LevelSelectPanel 预置体（幂等）。
   /// 生成内容（灰盒即可用，可后续在 Prefab 模式美化）：
   /// 根 RectTransform（全屏，挂 <see cref="LevelSelectPanel"/>）
   ///  ├─ Backdrop（半透明 Image）
   ///  ├─ Title_Text（顶部标题）
   ///  ├─ LevelList（左栏，VerticalLayoutGroup；含 LevelItemTemplate 按钮[常隐]）
   ///  ├─ IntroTitle_Text / IntroBody_Text（右侧介绍）
   ///  └─ EnterButton / BackButton（底部）
   /// 并设置 assetBundleName = levelselectpanel_prefab（与 <see cref="LevelSelectPanel.AssetBundleName"/> 一致）、
   /// 回填面板 catalog 字段为 BattleLevelCatalog 资产（直连默认关/列表数据源）。
   /// </summary>
   public static class LevelSelectPanelBuilder
   {
      private const string PanelFolder = "Assets/04_Prefabs/Panel/LevelSelect";
      private const string PanelPath = PanelFolder + "/LevelSelectPanel.prefab";
      private const string CatalogPath = "Assets/Data/BattleLevels/BattleLevelCatalog.asset";

      [MenuItem("Thrud/Battle/T3.1C/7. Create LevelSelect Panel (prefab)")]
      public static void CreateOrRefreshLevelSelectPanel()
      {
         if (EditorApplication.isPlaying)
         {
            Debug.LogError("[T3.1C] 请在退出 Play Mode 后执行本菜单。");
            return;
         }

         // 数据就绪：3 预设关卡 + 目录（CreatePresetLevels 末尾自动刷新目录）
         BattleLevelDataSetup.CreatePresetLevels();

         EnsureFolder(PanelFolder);

         var catalog = AssetDatabase.LoadAssetAtPath<BattleLevelCatalog>(CatalogPath);
         if (catalog == null)
         {
            Debug.LogError($"[T3.1C] 关卡目录资产缺失：{CatalogPath}，中止创建关卡选择面板。");
            return;
         }

         if (AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) != null)
         {
            RefreshExisting(catalog);
         }
         else
         {
            CreateNew(catalog);
         }
      }

      private static void CreateNew(BattleLevelCatalog catalog)
      {
         var root = new GameObject("LevelSelectPanel", typeof(RectTransform));
         try
         {
            BuildUi(root, catalog, out _);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            Debug.Log($"[T3.1C] 已创建 {PanelPath}（含绑定与 AB 标签）。");
         }
         finally
         {
            Object.DestroyImmediate(root);
         }

         ApplyAssetBundle();
      }

      private static void RefreshExisting(BattleLevelCatalog catalog)
      {
         var root = PrefabUtility.LoadPrefabContents(PanelPath);
         if (root == null)
         {
            Debug.LogError($"[T3.1C] 无法打开既有预置体：{PanelPath}");
            return;
         }

         try
         {
            BuildUi(root, catalog, out var changed);
            if (changed)
            {
               PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
               Debug.Log("[T3.1C] 已刷新 LevelSelectPanel 预置体结构/绑定。");
            }
            else
            {
               Debug.Log("[T3.1C] LevelSelectPanel 预置体已就绪，无需改动。");
            }
         }
         finally
         {
            PrefabUtility.UnloadPrefabContents(root);
         }

         ApplyAssetBundle();
      }

      /// <summary>在给定根上重建 UI 层级并绑定到 LevelSelectPanel 组件。</summary>
      private static void BuildUi(GameObject root, BattleLevelCatalog catalog, out bool changed)
      {
         changed = false;
         var panel = root.GetComponent<LevelSelectPanel>();
         if (panel == null)
         {
            panel = root.AddComponent<LevelSelectPanel>();
            changed = true;
         }

         // 清理旧子节点（幂等重建）
         for (var i = root.transform.childCount - 1; i >= 0; i--)
         {
            var child = root.transform.GetChild(i);
            Object.DestroyImmediate(child.gameObject);
            changed = true;
         }

         var rootRect = (RectTransform)root.transform;
         rootRect.anchorMin = Vector2.zero;
         rootRect.anchorMax = Vector2.one;
         rootRect.pivot = new Vector2(0.5f, 0.5f);
         rootRect.offsetMin = Vector2.zero;
         rootRect.offsetMax = Vector2.zero;

         // ---- 背景 ----
         var backdrop = CreateRect("Backdrop", rootRect);
         Stretch(backdrop);
         var bgImage = backdrop.gameObject.AddComponent<Image>();
         bgImage.color = new Color(0f, 0f, 0f, 0.72f);
         bgImage.raycastTarget = true;

         // ---- 标题 ----
         var title = CreateText("Title_Text", rootRect, "选择关卡", 30, TextAnchor.UpperCenter);
         var titleRect = (RectTransform)title.transform;
         SetAnchored(titleRect, 0f, 0.92f, 1f, 1f, new Vector2(0f, 0f), new Vector2(0f, -8f));

         // ---- 左栏关卡列表 ----
         var listRect = CreateRect("LevelList", rootRect);
         SetAnchored(listRect, 0.02f, 0.12f, 0.44f, 0.88f, new Vector2(0f, 0f), new Vector2(0f, 0f));
         var layout = listRect.gameObject.AddComponent<VerticalLayoutGroup>();
         layout.padding = new RectOffset(4, 4, 4, 4);
         layout.spacing = 10f;
         layout.childAlignment = TextAnchor.UpperCenter;
         // 子项尺寸自持：宽=模板锚点撑满、高=56（layout 只负责纵向排布，避免控件把高度压没）
         layout.childControlWidth = false;
         layout.childControlHeight = false;
         layout.childForceExpandWidth = false;
         layout.childForceExpandHeight = false;

         // 条目模板（按钮，常隐；运行时按关卡数克隆）
         var template = CreateButton("LevelItemTemplate", listRect, "关卡");
         template.gameObject.SetActive(false);
         var templateRect = (RectTransform)template.transform;
         templateRect.anchorMin = new Vector2(0f, 1f);
         templateRect.anchorMax = new Vector2(1f, 1f);
         templateRect.pivot = new Vector2(0.5f, 0.5f);
         templateRect.sizeDelta = new Vector2(0f, 56f); // 宽度由 VLG 填充，高度固定 56

         // ---- 右侧介绍 ----
         var introTitle = CreateText("IntroTitle_Text", rootRect, "请选择关卡", 24, TextAnchor.UpperLeft);
         SetAnchored((RectTransform)introTitle.transform, 0.5f, 0.78f, 0.98f, 0.9f, new Vector2(0f, 0f), new Vector2(0f, 0f));

         var introBody = CreateText("IntroBody_Text", rootRect,
            "选择左侧列表中的关卡，查看介绍后点击「进入」出战。", 16, TextAnchor.UpperLeft);
         var introBodyRect = (RectTransform)introBody.transform;
         SetAnchored(introBodyRect, 0.5f, 0.22f, 0.98f, 0.76f, new Vector2(0f, 0f), new Vector2(0f, 0f));

         // ---- 底部按钮 ----
         var enter = CreateButton("EnterButton", rootRect, "进入");
         SetAnchored((RectTransform)enter.transform, 0.52f, 0.03f, 0.74f, 0.11f, new Vector2(0f, 0f), new Vector2(0f, 0f));

         var back = CreateButton("BackButton", rootRect, "返回");
         SetAnchored((RectTransform)back.transform, 0.76f, 0.03f, 0.98f, 0.11f, new Vector2(0f, 0f), new Vector2(0f, 0f));

         // ---- 绑定 ----
         var so = new SerializedObject(panel);
         Bind(so, "Title_Text", title);
         Bind(so, "LevelList", listRect);
         Bind(so, "LevelItemTemplate", template);
         Bind(so, "IntroTitle_Text", introTitle);
         Bind(so, "IntroBody_Text", introBody);
         Bind(so, "EnterButton", enter);
         Bind(so, "BackButton", back);
         Bind(so, "catalog", catalog);
         if (so.hasModifiedProperties)
         {
            so.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
         }
      }

      private static void ApplyAssetBundle()
      {
         var importer = AssetImporter.GetAtPath(PanelPath);
         if (importer == null)
         {
            Debug.LogError($"[T3.1C] 找不到预置体 importer：{PanelPath}");
            return;
         }

         importer.SetAssetBundleNameAndVariant(LevelSelectPanel.AssetBundleName, string.Empty);
         AssetDatabase.SaveAssets();
         Debug.Log($"[T3.1C] 已设置 AB：{LevelSelectPanel.AssetBundleName}");
      }

      // ---------------- UI 构建工具 ----------------

      private static RectTransform CreateRect(string name, Transform parent)
      {
         var go = new GameObject(name, typeof(RectTransform));
         var rt = (RectTransform)go.transform;
         rt.SetParent(parent, false);
         return rt;
      }

      private static Text CreateText(string name, Transform parent, string content, int size, TextAnchor anchor)
      {
         var rt = CreateRect(name, parent);
         var text = rt.gameObject.AddComponent<Text>();
         text.text = content;
         text.fontSize = size;
         text.alignment = anchor;
         text.color = Color.white;
         text.raycastTarget = false;
         text.font = LoadDefaultFont();
         return text;
      }

      private static Button CreateButton(string name, Transform parent, string label)
      {
         var rt = CreateRect(name, parent);
         var image = rt.gameObject.AddComponent<Image>();
         image.color = new Color(0.2f, 0.45f, 0.9f, 1f);
         image.raycastTarget = true;

         var button = rt.gameObject.AddComponent<Button>();
         button.targetGraphic = image;

         var labelRt = CreateRect("Text", rt);
         Stretch(labelRt);
         var text = labelRt.gameObject.AddComponent<Text>();
         text.text = label;
         text.fontSize = 20;
         text.alignment = TextAnchor.MiddleCenter;
         text.color = Color.white;
         text.raycastTarget = false;
         text.font = LoadDefaultFont();
         return button;
      }

      private static Font LoadDefaultFont()
      {
         var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
         if (font == null)
         {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
         }

         return font;
      }

      private static void Stretch(RectTransform rt)
      {
         rt.anchorMin = Vector2.zero;
         rt.anchorMax = Vector2.one;
         rt.pivot = new Vector2(0.5f, 0.5f);
         rt.offsetMin = Vector2.zero;
         rt.offsetMax = Vector2.zero;
      }

      private static void SetAnchored(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 offsetMin, Vector2 offsetMax)
      {
         rt.anchorMin = new Vector2(minX, minY);
         rt.anchorMax = new Vector2(maxX, maxY);
         rt.pivot = new Vector2(0.5f, 0.5f);
         rt.offsetMin = offsetMin;
         rt.offsetMax = offsetMax;
      }

      private static void Bind(SerializedObject so, string field, Object value)
      {
         var prop = so.FindProperty(field);
         if (prop != null)
         {
            prop.objectReferenceValue = value;
         }
      }

      private static void EnsureFolder(string folder)
      {
         if (AssetDatabase.IsValidFolder(folder)) return;

         var parent = folder.Substring(0, folder.LastIndexOf('/'));
         EnsureFolder(parent);
         AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
      }
   }
}
