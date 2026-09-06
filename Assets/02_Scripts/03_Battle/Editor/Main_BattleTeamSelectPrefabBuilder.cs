using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// Main_BattleTeamSelectPanel prefab 生成器（v1.0 A8：Editor 方式）。
   /// 依 v1.0 设计 §3.1/§6 程序化构建「4 槽卡位 + 弹窗」层级并 SaveAsPrefabAsset（产出 YAML .prefab）。
   /// 用法：TeaGame → Battle → Main_BattleTeamSelectPanel → Rebuild Prefab（可重复，覆盖同路径，GUID 不变）。
   /// 生成后请在 Unity 内确认结构，必要时对 prefab 执行 CodeGen 刷新 Designer。
   /// </summary>
   public static class Main_BattleTeamSelectPrefabBuilder
   {
      public const string PanelPrefabPath = "Assets/04_Prefabs/Panel/Main_BattleTeamSelectPanel.prefab";
      public const string CatalogAssetPath = "Assets/Data/BattleTeamCatalog.asset";
      public const string BundleName = "main_battleteamselectpanel_prefab";

      private const string FolderPath = "Assets/04_Prefabs/Panel";
      private const int SlotCount = BattleTeamSession.MaxTeamSize; // 4

      private static Font sUiFont;

      [MenuItem("TeaGame/Battle/Main_BattleTeamSelectPanel/Rebuild Prefab")]
      public static void RebuildPrefab()
      {
         EnsureFolderExists(FolderPath);

         var root = BuildHierarchy();
         if (root == null)
         {
            Debug.LogError("[MainTeamSelect] prefab 构建失败：Main_BattleTeamSelectPanel 组件未挂上（请确认脚本已编译）。");
            return;
         }

         root.SetActive(false);
         var prefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
         UnityEngine.Object.DestroyImmediate(root);
         if (prefab == null)
         {
            Debug.LogError($"[MainTeamSelect] 保存失败：{PanelPrefabPath}");
            return;
         }

         MarkAssetBundle(PanelPrefabPath, BundleName);
         AssetDatabase.SaveAssets();
         AssetDatabase.Refresh();

         Debug.Log($"[MainTeamSelect] 已重建预编队面板：{PanelPrefabPath}（AB={BundleName}）。\n" +
                   "请检查 4 槽布局与弹窗结构是否符合 v1.0；如使用 CodeGen，请对 prefab 重新生成 Designer。\n" +
                   "注意：正式打 AB 前需把编目资产 Assets/Data/BattleTeamCatalog.asset 纳入 Resources 或同 AB，编辑器灰盒可忽略。");
      }

      // ============================================================
      // 层级
      // ============================================================

      private static GameObject BuildHierarchy()
      {
         var root = CreateRect("Main_BattleTeamSelectPanel", null);
         SetStretch(root);
         var overlay = root.gameObject.AddComponent<Image>();
         overlay.color = new Color(0f, 0f, 0f, 0.55f);

         var panelComp = root.gameObject.AddComponent<Main_BattleTeamSelectPanel>();
         if (panelComp == null) return null;

         var bg = CreateRect("BG", root);
         SetCentered(bg, Vector2.zero, new Vector2(1160f, 820f));
         var bgImage = bg.gameObject.AddComponent<Image>();
         bgImage.color = new Color(0.06f, 0.06f, 0.1f, 0.97f);

         var title = CreateRect("Title", bg);
         SetCentered(title, new Vector2(0f, 350f), new Vector2(560f, 60f));
         var titleText = AddText(title, "出战编队", 44);

         var roleButtons = new Button[SlotCount];
         var roleTexts = new Text[SlotCount];
         var weaponButtons = new Button[SlotCount];
         var weaponTexts = new Text[SlotCount];

         // 横排 4 槽：上=角色 下=武器
         var slotX = new[] { -360f, -120f, 120f, 360f };
         for (var i = 0; i < SlotCount; i++)
         {
            var card = CreateRect($"Slot_{i + 1}", bg);
            SetCentered(card, new Vector2(slotX[i], 20f), new Vector2(220f, 460f));
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = new Color(0.13f, 0.14f, 0.2f, 1f);

            // 槽序号标签
            var slotLabel = CreateRect("SlotLabel", card);
            SetCentered(slotLabel, new Vector2(0f, 205f), new Vector2(180f, 34f));
            var slotLabelText = AddText(slotLabel, $"槽 {i + 1}", 22, new Color(0.7f, 0.7f, 0.75f, 1f));

            // 角色区（上半）
            var roleArea = AddButton(card, "RoleArea", new Vector2(0f, 115f), string.Empty, new Vector2(200f, 180f), 24);
            var roleTextGo = CreateRect("Text", roleArea.transform as RectTransform);
            SetStretch(roleTextGo);
            var roleText = roleTextGo.gameObject.AddComponent<Text>();
            ApplyText(roleText, "空", 24);
            roleText.raycastTarget = false;

            // 武器区（下半）
            var weaponArea = AddButton(card, "WeaponArea", new Vector2(0f, -115f), string.Empty, new Vector2(200f, 180f), 24);
            var weaponTextGo = CreateRect("Text", weaponArea.transform as RectTransform);
            SetStretch(weaponTextGo);
            var weaponText = weaponTextGo.gameObject.AddComponent<Text>();
            ApplyText(weaponText, "空", 24);
            weaponText.raycastTarget = false;

            roleButtons[i] = roleArea;
            roleTexts[i] = roleText;
            weaponButtons[i] = weaponArea;
            weaponTexts[i] = weaponText;
         }

         // 底部控制条
         var clearAll = AddButton(bg, "ClearAllButton", new Vector2(-260f, -355f), "清空全队", new Vector2(220f, 64f), 26);
         var start = AddButton(bg, "StartButton", new Vector2(0f, -355f), "开始战斗", new Vector2(220f, 64f), 26);
         start.targetGraphic.color = new Color(0.1f, 0.6f, 0.3f, 1f);
         var back = AddButton(bg, "BackButton", new Vector2(260f, -355f), "返回", new Vector2(220f, 64f), 26);
         back.targetGraphic.color = new Color(0.45f, 0.45f, 0.5f, 1f);

         var toastGo = CreateRect("Toast", bg);
         SetCentered(toastGo, new Vector2(0f, -285f), new Vector2(1000f, 44f));
         var toastText = AddText(toastGo, string.Empty, 26, new Color(1f, 0.85f, 0.3f, 1f));

         // 弹窗
         var popup = BuildPopup(root);
         if (popup == null) return null;

         // ---------------- 绑定字段 ----------------
         var so = new SerializedObject(panelComp);
         BindObject(so, "titleText", titleText);
         BindArray(so, "slotRoleButtons", roleButtons);
         BindArray(so, "slotRoleTexts", roleTexts);
         BindArray(so, "slotWeaponButtons", weaponButtons);
         BindArray(so, "slotWeaponTexts", weaponTexts);
         BindObject(so, "clearAllButton", clearAll);
         BindObject(so, "startButton", start);
         BindObject(so, "backButton", back);
         BindObject(so, "toastText", toastText);
         BindObject(so, "popupWindow", popup.Window);
         BindObject(so, "popupMask", popup.Mask);
         BindObject(so, "popupTitleText", popup.TitleText);
         BindObject(so, "itemListRoot", popup.ItemListRoot);
         BindObject(so, "itemTemplate", popup.ItemTemplate);
         BindObject(so, "detailPane", popup.DetailPane);
         BindObject(so, "detailTitleText", popup.DetailTitleText);
         BindObject(so, "detailBodyText", popup.DetailBodyText);
         BindObject(so, "popupCancelButton", popup.CancelButton);

         // 编目回退引用（v1.0：无 Data 注入时使用 prefab 序列化引用）
         var catalog = AssetDatabase.LoadAssetAtPath<BattleTeamCatalog>(CatalogAssetPath);
         if (catalog != null)
         {
            BindObject(so, "catalog", catalog);
         }
         else
         {
            Debug.LogWarning($"[MainTeamSelect] 未找到编目资产 {CatalogAssetPath}，面板 catalog 回退引用留空（需 Data 注入）。");
         }

         so.ApplyModifiedPropertiesWithoutUndo();
         return root.gameObject;
      }

      private sealed class PopupParts
      {
         public GameObject Window;
         public Button Mask;
         public Text TitleText;
         public RectTransform ItemListRoot;
         public Button ItemTemplate;
         public RectTransform DetailPane;
         public Text DetailTitleText;
         public Text DetailBodyText;
         public Button CancelButton;
      }

      private static PopupParts BuildPopup(Transform root)
      {
         var window = CreateRect("PopupWindow", root);
         SetStretch(window);
         window.gameObject.SetActive(false); // 默认隐藏

         var mask = CreateRect("PopupMask", window);
         SetStretch(mask);
         var maskImage = mask.gameObject.AddComponent<Image>();
         maskImage.color = new Color(0f, 0f, 0f, 0.35f);
         var maskButton = mask.gameObject.AddComponent<Button>();
         maskButton.targetGraphic = maskImage;
         maskButton.transition = Selectable.Transition.None;

         var panel = CreateRect("PopupPanel", window);
         SetCentered(panel, new Vector2(0f, 0f), new Vector2(1000f, 640f));
         var panelImage = panel.gameObject.AddComponent<Image>();
         panelImage.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

         var popupTitle = CreateRect("PopupTitle", panel);
         SetCentered(popupTitle, new Vector2(0f, 275f), new Vector2(600f, 50f));
         var popupTitleText = AddText(popupTitle, "选择角色", 34);

         // 条目列表（左侧）：VerticalLayoutGroup + ContentSizeFitter，无需滚动（灰盒条目 ≤9）
         var list = CreateRect("ItemList", panel);
         SetCentered(list, new Vector2(-205f, -25f), new Vector2(500f, 480f));
         var listBg = list.gameObject.AddComponent<Image>();
         listBg.color = new Color(0f, 0f, 0f, 0.25f);
         var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
         layout.spacing = 2f;
         layout.childAlignment = TextAnchor.UpperCenter;
         layout.childControlWidth = true;
         layout.childControlHeight = false; // 条目保持自身模板尺寸（470x44）
         layout.childForceExpandWidth = true;
         layout.childForceExpandHeight = false;

         // 模板（默认隐藏，运行时克隆）
         var template = AddButton(list, "ItemTemplate", Vector2.zero, "条目", new Vector2(470f, 44f), 22);
         template.gameObject.SetActive(false);

         // 右侧简介区（DetailPane —— 后续强化/微调扩展点）
         var detail = CreateRect("DetailPane", panel);
         SetCentered(detail, new Vector2(245f, -25f), new Vector2(440f, 480f));
         var detailImage = detail.gameObject.AddComponent<Image>();
         detailImage.color = new Color(0f, 0f, 0f, 0.25f);

         var detailTitleGo = CreateRect("DetailTitle", detail);
         SetCentered(detailTitleGo, new Vector2(0f, 200f), new Vector2(400f, 60f));
         var detailTitleText = AddText(detailTitleGo, string.Empty, 28, new Color(0.9f, 0.9f, 0.95f, 1f));

         var detailBodyGo = CreateRect("DetailBody", detail);
         SetCentered(detailBodyGo, new Vector2(0f, -20f), new Vector2(400f, 360f));
         var detailBodyText = AddText(detailBodyGo, string.Empty, 20, new Color(0.85f, 0.85f, 0.85f, 1f));
         detailBodyText.alignment = TextAnchor.UpperLeft;
         detailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
         detailBodyText.verticalOverflow = VerticalWrapMode.Overflow;

         var cancel = AddButton(panel, "PopupCancel", new Vector2(0f, -275f), "取消", new Vector2(180f, 52f), 24);

         return new PopupParts
         {
            Window = window.gameObject,
            Mask = maskButton,
            TitleText = popupTitleText,
            ItemListRoot = list,
            ItemTemplate = template,
            DetailPane = detail,
            DetailTitleText = detailTitleText,
            DetailBodyText = detailBodyText,
            CancelButton = cancel,
         };
      }

      // ============================================================
      // 绑定
      // ============================================================

      private static void BindObject(SerializedObject so, string fieldName, UnityEngine.Object value)
      {
         var prop = so.FindProperty(fieldName);
         if (prop == null)
         {
            Debug.LogWarning($"[MainTeamSelect] 未找到字段 {fieldName}，请检查面板脚本与生成器字段名是否一致。");
            return;
         }

         prop.objectReferenceValue = value;
      }

      private static void BindArray(SerializedObject so, string fieldName, Array values)
      {
         var prop = so.FindProperty(fieldName);
         if (prop == null || !prop.isArray)
         {
            Debug.LogWarning($"[MainTeamSelect] 未找到数组字段 {fieldName}，请检查面板脚本与生成器字段名是否一致。");
            return;
         }

         prop.arraySize = values.Length;
         for (var i = 0; i < values.Length; i++)
         {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = (UnityEngine.Object)values.GetValue(i);
         }
      }

      // ============================================================
      // UGUI 工具
      // ============================================================

      private static void EnsureFolderExists(string assetFolderPath)
      {
         if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

         var segments = assetFolderPath.Split('/');
         var current = segments[0];
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

      private static void MarkAssetBundle(string assetPath, string bundleName)
      {
         var importer = AssetImporter.GetAtPath(assetPath);
         if (importer == null)
         {
            AssetDatabase.ImportAsset(assetPath);
            importer = AssetImporter.GetAtPath(assetPath);
         }

         if (importer == null)
         {
            Debug.LogWarning($"[MainTeamSelect] 无法写入 AB 标记：{assetPath}");
            return;
         }

         importer.SetAssetBundleNameAndVariant(bundleName, string.Empty);
         importer.SaveAndReimport();
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

      private static Button AddButton(RectTransform parent, string name, Vector2 position, string label,
         Vector2 size, int fontSize)
      {
         var rect = CreateRect(name, parent);
         SetCentered(rect, position, size);
         var image = rect.gameObject.AddComponent<Image>();
         image.color = new Color(0.2f, 0.45f, 0.9f, 1f);

         var button = rect.gameObject.AddComponent<Button>();
         button.targetGraphic = image;

         var textGo = CreateRect("Text", rect);
         SetStretch(textGo);
         var text = textGo.gameObject.AddComponent<Text>();
         ApplyText(text, label, fontSize);
         text.raycastTarget = false;
         return button;
      }

      private static Text AddText(RectTransform parent, string content, int fontSize)
      {
         return AddText(parent, content, fontSize, Color.white);
      }

      private static Text AddText(RectTransform parent, string content, int fontSize, Color color)
      {
         var textGo = CreateRect("Text", parent);
         SetStretch(textGo);
         var text = textGo.gameObject.AddComponent<Text>();
         ApplyText(text, content, fontSize);
         text.color = color;
         text.raycastTarget = false;
         return text;
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
