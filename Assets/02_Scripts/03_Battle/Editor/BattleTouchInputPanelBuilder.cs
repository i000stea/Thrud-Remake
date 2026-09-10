using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// T1 Editor 生成器：创建 BattleTouchInputPanel 预置体（幂等，可重复执行）。
   /// 生成内容（对齐 13 Demo P3 首版布局）：
   /// 根 RectTransform（全屏，挂 <see cref="Battle_TouchInputPanel"/>，UIKit UIPanel）
   ///  ├─ LookZone（屏中约 40% 宽、中高区域；透明 Image + TouchDragLookZone → 拖拽转镜头）
   ///  ├─ JoystickBase（左下，Image + TouchJoystick）+ Knob（子 Image）
   ///  └─ 右侧按钮（每键 = Image + Text 标签 + TouchHoldButton/TouchTapButton）：
   ///      Fire（按住开火）、E、Q、切人 ◀/▶、暂停 II
   /// 并设置 assetBundleName = battletouchinputpanel_prefab（与 <see cref="Battle_TouchInputPanel.AssetBundleName"/> 一致）。
   /// </summary>
   public static class BattleTouchInputPanelBuilder
   {
      private const string PanelFolder = "Assets/04_Prefabs/Panel/TouchInput";
      private const string PanelPath = PanelFolder + "/BattleTouchInputPanel.prefab";

      [MenuItem("Thrud/Battle/T1/Create Touch Input Panel (prefab)")]
      public static void CreateOrRefreshTouchPanel()
      {
         if (EditorApplication.isPlaying)
         {
            Debug.LogError("[T1] 请在退出 Play Mode 后执行本菜单。");
            return;
         }

         EnsureFolder(PanelFolder);

         if (AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) != null)
         {
            RefreshExisting();
         }
         else
         {
            CreateNew();
         }
      }

      private static void CreateNew()
      {
         var root = new GameObject("BattleTouchInputPanel", typeof(RectTransform), typeof(Battle_TouchInputPanel));
         try
         {
            BuildUi(root);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            Debug.Log($"[T1] 已创建 {PanelPath}（含输入组件与 AB 标签）。");
         }
         finally
         {
            Object.DestroyImmediate(root);
         }

         ApplyAssetBundle();
      }

      private static void RefreshExisting()
      {
         var root = PrefabUtility.LoadPrefabContents(PanelPath);
         if (root == null)
         {
            Debug.LogError($"[T1] 无法打开既有预置体：{PanelPath}");
            return;
         }

         try
         {
            // 幂等重建子结构（保留根组件配置）
            BuildUi(root);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            Debug.Log("[T1] 已刷新 BattleTouchInputPanel 预置体结构。");
         }
         finally
         {
            PrefabUtility.UnloadPrefabContents(root);
         }

         ApplyAssetBundle();
      }

      private static void BuildUi(GameObject root)
      {
         // 根为全屏 stretch；先清理旧子对象再重建
         for (var i = root.transform.childCount - 1; i >= 0; i--)
         {
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
         }

         var rootRect = (RectTransform)root.transform;
         rootRect.anchorMin = Vector2.zero;
         rootRect.anchorMax = Vector2.one;
         rootRect.pivot = new Vector2(0.5f, 0.5f);
         rootRect.offsetMin = Vector2.zero;
         rootRect.offsetMax = Vector2.zero;

         BuildDragLookZone(rootRect);
         BuildJoystick(rootRect);
         BuildRightControls(rootRect);
      }

      // ---------------- 中央拖拽层 ----------------

      private static void BuildDragLookZone(RectTransform parent)
      {
         var zone = CreateRect("LookZone", parent);
         zone.anchorMin = new Vector2(0.28f, 0.14f);
         zone.anchorMax = new Vector2(0.72f, 0.9f);
         zone.offsetMin = Vector2.zero;
         zone.offsetMax = Vector2.zero;

         var image = zone.gameObject.AddComponent<Image>();
         image.color = new Color(0f, 0f, 0f, 0.01f); // 近乎透明但可 raycast
         image.raycastTarget = true;
         zone.gameObject.AddComponent<TouchDragLookZone>();
      }

      // ---------------- 左下摇杆 ----------------

      private static void BuildJoystick(RectTransform parent)
      {
         const float baseSize = 220f;

         var baseRect = CreateRect("JoystickBase", parent);
         baseRect.anchorMin = new Vector2(0f, 0f);
         baseRect.anchorMax = new Vector2(0f, 0f);
         baseRect.pivot = new Vector2(0f, 0f);
         baseRect.anchoredPosition = new Vector2(64f, 64f);
         baseRect.sizeDelta = new Vector2(baseSize, baseSize);

         var baseImage = baseRect.gameObject.AddComponent<Image>();
         baseImage.color = new Color(1f, 1f, 1f, 0.16f);
         baseImage.raycastTarget = true;

         var knobRect = CreateRect("Knob", baseRect);
         knobRect.anchorMin = new Vector2(0.5f, 0.5f);
         knobRect.anchorMax = new Vector2(0.5f, 0.5f);
         knobRect.pivot = new Vector2(0.5f, 0.5f);
         knobRect.sizeDelta = new Vector2(baseSize * 0.5f, baseSize * 0.5f);
         var knobImage = knobRect.gameObject.AddComponent<Image>();
         knobImage.color = new Color(1f, 1f, 1f, 0.6f);
         knobImage.raycastTarget = false;

         var joystick = baseRect.gameObject.AddComponent<TouchJoystick>();
         joystick.Setup(knobRect, baseSize * 0.4f);
      }

      // ---------------- 右下按钮 ----------------

      private static void BuildRightControls(RectTransform parent)
      {
         // 暂停（右上角）
         CreateButton(
            parent, "PauseButton", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-90f, -80f), new Vector2(100f, 100f), "II",
            btn => btn.GetComponent<TouchTapButton>().Setup(TouchTapKind.Pause));

         // 普攻大键（右下）
         CreateButton(
            parent, "FireButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-160f, 150f), new Vector2(200f, 200f), "开火",
            btn => btn.GetComponent<TouchHoldButton>().Setup(TouchHoldKind.Fire));

         // 冲刺/翻滚（Fire 左下方；按住=冲刺，按下瞬间=翻滚，与 Shift 键一致）
         CreateButton(
            parent, "SprintButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-350f, 90f), new Vector2(140f, 140f), "冲刺",
            btn => btn.GetComponent<TouchHoldButton>().Setup(TouchHoldKind.Sprint));

         // E / Q（普攻上方）
         CreateButton(
            parent, "SkillEButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-350f, 250f), new Vector2(140f, 140f), "E",
            btn => btn.GetComponent<TouchTapButton>().Setup(TouchTapKind.SkillE));

         CreateButton(
            parent, "SkillQButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-500f, 170f), new Vector2(140f, 140f), "Q",
            btn => btn.GetComponent<TouchTapButton>().Setup(TouchTapKind.SkillQ));

         // 切人 ◀ ▶
         CreateButton(
            parent, "RolePrevButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-260f, 430f), new Vector2(96f, 96f), "◀",
            btn => btn.GetComponent<TouchTapButton>().Setup(TouchTapKind.RolePrev));

         CreateButton(
            parent, "RoleNextButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-120f, 430f), new Vector2(96f, 96f), "▶",
            btn => btn.GetComponent<TouchTapButton>().Setup(TouchTapKind.RoleNext));
      }

      // ---------------- 按钮工具 ----------------

      private static void CreateButton(
         RectTransform parent,
         string name,
         Vector2 anchor,
         Vector2 pivot,
         Vector2 anchoredPosition,
         Vector2 size,
         string label,
         System.Action<GameObject> wireComponent)
      {
         var rect = CreateRect(name, parent);
         rect.anchorMin = anchor;
         rect.anchorMax = anchor;
         rect.pivot = pivot;
         rect.anchoredPosition = anchoredPosition;
         rect.sizeDelta = size;

         var image = rect.gameObject.AddComponent<Image>();
         image.color = new Color(0.22f, 0.5f, 0.95f, 0.6f);
         image.raycastTarget = true;

         // 按用途挂交互组件：按住型（开火/冲刺）用 TouchHoldButton，点按型用 TouchTapButton
         if (name.StartsWith("Fire") || name.StartsWith("Sprint"))
         {
            rect.gameObject.AddComponent<TouchHoldButton>();
         }
         else
         {
            rect.gameObject.AddComponent<TouchTapButton>();
         }

         var labelRect = CreateRect("Label", rect);
         Stretch(labelRect);
         var text = labelRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
         text.text = label;
         text.fontSize = 40;
         text.alignment = TMPro.TextAlignmentOptions.Center;
         text.color = Color.white;
         text.raycastTarget = false;
         text.font = LoadChineseFont();

         wireComponent?.Invoke(rect.gameObject);
      }

      // ---------------- 工具 ----------------

      private static RectTransform CreateRect(string name, Transform parent)
      {
         var go = new GameObject(name, typeof(RectTransform));
         var rect = (RectTransform)go.transform;
         rect.SetParent(parent, false);
         return rect;
      }

      private static void Stretch(RectTransform rect)
      {
         rect.anchorMin = Vector2.zero;
         rect.anchorMax = Vector2.one;
         rect.pivot = new Vector2(0.5f, 0.5f);
         rect.offsetMin = Vector2.zero;
         rect.offsetMax = Vector2.zero;
      }

      /// <summary>
      /// 按钮标签字体：优先项目中文 TMP 字体（内置 LegacyRuntime 不含中文，会显示方块）；
      /// 找不到则回落 TMP 默认字体。
      /// </summary>
      private static TMPro.TMP_FontAsset LoadChineseFont()
      {
         const string fontPath = "Assets/08_Fonts/zh_SansSerif-1 UILanTing_Small_GBK SDF.asset";
         var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
         if (font == null)
         {
            font = TMPro.TMP_Settings.defaultFontAsset;
            Debug.LogWarning($"[T1] 未找到中文 TMP 字体：{fontPath}，回落 TMP 默认字体（中文标签可能显示为方块）。");
         }

         return font;
      }

      private static void EnsureFolder(string folder)
      {
         if (AssetDatabase.IsValidFolder(folder)) return;

         var parent = folder.Substring(0, folder.LastIndexOf('/'));
         EnsureFolder(parent);
         AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
      }

      private static void ApplyAssetBundle()
      {
         var importer = AssetImporter.GetAtPath(PanelPath);
         if (importer == null)
         {
            Debug.LogError($"[T1] 找不到预置体 importer：{PanelPath}");
            return;
         }

         importer.SetAssetBundleNameAndVariant(Battle_TouchInputPanel.AssetBundleName, string.Empty);
         AssetDatabase.SaveAssets();
         Debug.Log($"[T1] 已设置 AB：{Battle_TouchInputPanel.AssetBundleName}");
      }
   }
}
