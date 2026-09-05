using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeaFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// T3.1A-4b 场景链 Editor 工具：一键生成"主场景 → 选队场景 → 战斗"的流程资产。
   ///
   /// 用法：TeaGame → Battle → Team Flow → Setup All（可重复/幂等）。
   /// 生成 4 类产物：
   ///   1. 编目 asset   Assets/Data/BattleTeamCatalog.asset（默认 = 灰盒 4 角色 × 4 武器；
   ///                  已存在则复用，用户可自行在资产里增补条目）；
   ///   2. 两个面板 prefab  Assets/Resources/Panels/BattleMainHubPanel.prefab、
   ///                   BattleTeamSelectPanel.prefab（按钮数 = 编目数，按 BattleTeamSelectPanel/BattleMainHubPanel 字段名绑定，
   ///                   并写入 ResKit AB 标记：battlemainhubpanel_prefab / battleteamselectpanel_prefab）；
   ///   3. 两个场景     Assets/01_Scenes/M1_MainHub.unity、M1_TeamSelect.unity
   ///                   （空场景 + 场景对象挂 Bootstrap；选队场景 Bootstrap.catalog 绑定编目 asset）；
   ///   4. Build Settings 追加两个场景（ConfigScene 保持 [0]，去掉同路径后 Append）。
   ///
   /// 说明：
   ///   - Setup All 是幂等的：重复执行会覆盖重建两个 prefab/场景，不会误动用户手工维护的
   ///     BattlePauseMenuPanel.prefab / BattleLevelResultPanel.prefab；
   ///   - 场景为纯 UIKit 界面场景：UIRoot 由 UIKit 自动创建（DDOL，自带 Canvas/UICamera/EventSystem），
   ///     因此场景内不需要再放 Canvas/EventSystem；
   ///   - 编目 asset 已存在时不覆盖（避免清掉用户后续自行补充的条目）；如需重置到默认 4×4，
   ///     用同级菜单 TeaGame → Battle → Team Flow → Reset Catalog to Default 4×4。
   /// </summary>
   public static class BattleTeamFlowSetup
   {
      public const string CatalogAssetPath = "Assets/Data/BattleTeamCatalog.asset";
      public const string PanelsFolderPath = "Assets/Resources/Panels";
      public const string MainHubPrefabPath = "Assets/Resources/Panels/BattleMainHubPanel.prefab";
      public const string TeamSelectPrefabPath = "Assets/Resources/Panels/BattleTeamSelectPanel.prefab";
      public const string MainHubScenePath = "Assets/01_Scenes/M1_MainHub.unity";
      public const string TeamSelectScenePath = "Assets/01_Scenes/M1_TeamSelect.unity";

      /// <summary>默认编目角色 = BattleGraybox 直连默认编队的 4 名（Test0 已核实 GUID）。</summary>
      private static readonly string[] DefaultRoleGuids =
      {
         "5b84149f04e74da41add29c24f116a1c", // 2001001-Faria
         "ad3ae915e4a7138479b9953688484145", // 2004001-Willa
         "a27a63b8f76f39048996b7f95b2dfa6f", // 2008001-Yera
         "9c291e91627ba1846979e40973180dbf", // 2011001-GwyrDolyn
      };

      /// <summary>默认编目武器 = BattleGraybox 直连默认编队的 4 把（Test0 已核实 GUID）。</summary>
      private static readonly string[] DefaultWeaponGuids =
      {
         "01d1b6448a6ba434888abe289aad260b", // 5021010-变革者·改
         "20058370950d7c14abf91441865a97d5", // 5023010-险兆者·改
         "feffcc04d72a2e64f98bb19f251b9cfd", // 5024010-祈幻者·改
         "34c2c9667ebdcdb4187a36f03496daa6", // 5025010-狂热者·改
      };

      private static Font sUiFont;

      // ============================================================
      // 菜单入口
      // ============================================================

      [MenuItem("TeaGame/Battle/Team Flow/Setup All")]
      public static void SetupAll()
      {
         // 1. 编目 asset（存在即复用）
         var catalog = EnsureCatalog();
         if (catalog == null)
         {
            Debug.LogError("[T3.1A] Setup All 中止：编目 asset 生成失败。");
            return;
         }

         // 2. 两个面板 prefab（数组按钮数 = 编目数）
         var hubOk = RebuildMainHubPrefab();
         var selectOk = RebuildTeamSelectPrefab(catalog);

         // 3. 两个场景（当前活动场景有未保存修改时跳过，避免打断用户编辑）
         var mainHubSceneOk = RebuildMainHubScene();
         var teamSelectSceneOk = RebuildTeamSelectScene(catalog);

         AssetDatabase.SaveAssets();
         AssetDatabase.Refresh();

         if (!hubOk || !selectOk || !mainHubSceneOk || !teamSelectSceneOk)
         {
            Debug.LogError("[T3.1A] Setup All 部分完成（见上方逐项日志）。修复后重跑一次（幂等）。");
            return;
         }

         Debug.Log("[T3.1A] Setup All 完成：编目 " + CatalogAssetPath +
                   "；面板 " + MainHubPrefabPath + " / " + TeamSelectPrefabPath +
                   "（已写 AB 标记，需重新打 AB 生成 QAssets 常量）；" +
                   "场景 " + MainHubScenePath + " / " + TeamSelectScenePath + " 已加入 Build Settings（ConfigScene 仍为 [0]）。\n" +
                   "下一步：运行 ResKit 打 AB（自动把 battlemainhubpanel_prefab / battleteamselectpanel_prefab 纳入并同步 QAssets.cs），" +
                   "随后即可 Play M1_MainHub → 出战编队 → 开始战斗 全链验证。");
      }

      /// <summary>把编目重置为默认 4 角色 × 4 武器（覆盖现有 asset，慎重）。</summary>
      [MenuItem("TeaGame/Battle/Team Flow/Reset Catalog to Default 4×4")]
      public static void ResetCatalogToDefault()
      {
         RebuildDefaultCatalog();
      }

      [MenuItem("TeaGame/Battle/Team Flow/Rebuild Flow Prefabs (2 面板)")]
      public static void RebuildFlowPrefabs()
      {
         var catalog = EnsureCatalog();
         if (catalog == null)
         {
            Debug.LogError("[T3.1A] 面板重建中止：编目 asset 缺失（请先 Setup All 或 Reset Catalog）。");
            return;
         }

         var hubOk = RebuildMainHubPrefab();
         var selectOk = RebuildTeamSelectPrefab(catalog);
         AssetDatabase.SaveAssets();
         Debug.Log($"[T3.1A] 面板重建完成：MainHub={hubOk}，TeamSelect={selectOk}。");
      }

      [MenuItem("TeaGame/Battle/Team Flow/Rebuild Flow Scenes (2 场景)")]
      public static void RebuildFlowScenes()
      {
         var catalog = EnsureCatalog();
         if (catalog == null)
         {
            Debug.LogError("[T3.1A] 场景重建中止：编目 asset 缺失（请先 Setup All 或 Reset Catalog）。");
            return;
         }

         var mainHubOk = RebuildMainHubScene();
         var teamSelectOk = RebuildTeamSelectScene(catalog);
         AssetDatabase.SaveAssets();
         Debug.Log($"[T3.1A] 场景重建完成：MainHub={mainHubOk}，TeamSelect={teamSelectOk}。");
      }

      // ============================================================
      // 1. 编目 asset
      // ============================================================

      /// <summary>编目已存在则复用（保留用户增补），否则用默认 4×4 创建。</summary>
      private static BattleTeamCatalog EnsureCatalog()
      {
         var existing = AssetDatabase.LoadAssetAtPath<BattleTeamCatalog>(CatalogAssetPath);
         if (existing != null)
         {
            var roleCount = existing.Roles != null ? existing.Roles.Length : 0;
            var weaponCount = existing.Weapons != null ? existing.Weapons.Length : 0;
            Debug.Log($"[T3.1A] 复用已有编目 {CatalogAssetPath}（角色 {roleCount} / 武器 {weaponCount}）。" +
                      "如需重置为默认 4×4，请运行 TeaGame → Battle → Team Flow → Reset Catalog to Default 4×4。");
            return existing;
         }

         return RebuildDefaultCatalog();
      }

      private static BattleTeamCatalog RebuildDefaultCatalog()
      {
         if (AssetDatabase.LoadAssetAtPath<BattleTeamCatalog>(CatalogAssetPath) != null)
         {
            AssetDatabase.DeleteAsset(CatalogAssetPath);
         }

         var catalog = ScriptableObject.CreateInstance<BattleTeamCatalog>();
         AssetDatabase.CreateAsset(catalog, CatalogAssetPath);

         var roles = LoadAssetsByGuids<RoleItem_Data>(DefaultRoleGuids, "角色");
         var weapons = LoadAssetsByGuids<WeaponItem_Data>(DefaultWeaponGuids, "武器");
         roles = roles.OrderBy(r => r.itemID).ToArray();
         weapons = weapons.OrderBy(w => w.itemID).ToArray();

         var so = new SerializedObject(catalog);
         var rolesProp = so.FindProperty("roles");
         rolesProp.arraySize = roles.Length;
         for (var i = 0; i < roles.Length; i++)
         {
            rolesProp.GetArrayElementAtIndex(i).objectReferenceValue = roles[i];
         }

         var weaponsProp = so.FindProperty("weapons");
         weaponsProp.arraySize = weapons.Length;
         for (var i = 0; i < weapons.Length; i++)
         {
            weaponsProp.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
         }

         so.ApplyModifiedPropertiesWithoutUndo();
         EditorUtility.SetDirty(catalog);
         AssetDatabase.SaveAssets();

         Debug.Log($"[T3.1A] 已重置编目 {CatalogAssetPath}：角色 {string.Join("/", roles.Select(r => r.itemName))}，" +
                   $"武器 {string.Join("/", weapons.Select(w => w.itemName))}。\n" +
                   "灰盒口径 4 角色 × 4 武器（BattleGraybox 直连默认同集合）；如需更多条目，直接在资产 Inspector 中补充。");
         return catalog;
      }

      // ============================================================
      // 2. 面板 prefab
      // ============================================================

      private static bool RebuildMainHubPrefab()
      {
         EnsureFolderExists(PanelsFolderPath);

         var root = BuildMainHubHierarchy();
         if (root == null)
         {
            Debug.LogError("[T3.1A] 主场景面板构建失败：BattleMainHubPanel 组件未挂上（请确认脚本已编译）。");
            return false;
         }

         root.SetActive(false);
         var prefab = PrefabUtility.SaveAsPrefabAsset(root, MainHubPrefabPath);
         Object.DestroyImmediate(root);
         if (prefab == null)
         {
            Debug.LogError($"[T3.1A] 保存失败：{MainHubPrefabPath}");
            return false;
         }

         MarkAssetBundle(MainHubPrefabPath, "battlemainhubpanel_prefab");
         Debug.Log($"[T3.1A] 已重建主场景面板：{MainHubPrefabPath}（AB=battlemainhubpanel_prefab）。");
         return true;
      }

      private static bool RebuildTeamSelectPrefab(BattleTeamCatalog catalog)
      {
         EnsureFolderExists(PanelsFolderPath);

         var root = BuildTeamSelectHierarchy(catalog);
         if (root == null)
         {
            Debug.LogError("[T3.1A] 选队面板构建失败：BattleTeamSelectPanel 组件未挂上（请确认脚本已编译）。");
            return false;
         }

         root.SetActive(false);
         var prefab = PrefabUtility.SaveAsPrefabAsset(root, TeamSelectPrefabPath);
         Object.DestroyImmediate(root);
         if (prefab == null)
         {
            Debug.LogError($"[T3.1A] 保存失败：{TeamSelectPrefabPath}");
            return false;
         }

         MarkAssetBundle(TeamSelectPrefabPath, "battleteamselectpanel_prefab");
         Debug.Log($"[T3.1A] 已重建选队面板：{TeamSelectPrefabPath}（角色按钮 {GetCatalogCount(catalog?.Roles)} / 武器按钮 {GetCatalogCount(catalog?.Weapons)}，AB=battleteamselectpanel_prefab）。");
         return true;
      }

      private static int GetCatalogCount(System.Array array)
      {
         return array != null ? array.Length : 0;
      }

      /// <summary>主场景面板：标题/提示/开始按钮（契约见 BattleMainHubPanel）。</summary>
      private static GameObject BuildMainHubHierarchy()
      {
         var root = CreateRect("BattleMainHubPanel", null);
         SetStretch(root);
         var overlay = root.gameObject.AddComponent<Image>();
         overlay.color = new Color(0f, 0f, 0f, 0.55f);

         var panelComp = root.gameObject.AddComponent<BattleMainHubPanel>();
         if (panelComp == null) return null;

         var bg = CreateRect("MainHubPanel", root);
         SetCentered(bg, Vector2.zero, new Vector2(760f, 460f));
         var bgImage = bg.gameObject.AddComponent<Image>();
         bgImage.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

         var titleRect = CreateRect("Title", bg);
         SetCentered(titleRect, new Vector2(0f, 110f), new Vector2(700f, 90f));
         var titleText = titleRect.gameObject.AddComponent<Text>();
         ApplyText(titleText, "M1 · 出战编队入口", 42);
         titleText.raycastTarget = false;

         var hintRect = CreateRect("Hint", bg);
         SetCentered(hintRect, new Vector2(0f, -10f), new Vector2(700f, 140f));
         var hintText = hintRect.gameObject.AddComponent<Text>();
         ApplyText(hintText,
            "灰盒主场景：点击「出战编队」进入 1-4 人角色×武器编队选择。\n（正式主城 / 关卡选择将于 M2 接入）",
            24);
         hintText.alignment = TextAnchor.MiddleCenter;
         hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
         hintText.verticalOverflow = VerticalWrapMode.Overflow;
         hintText.raycastTarget = false;

         var start = CreateButton(bg, "StartButton", new Vector2(0f, -160f), "出战编队", new Vector2(320f, 66f), 30);

         var so = new SerializedObject(panelComp);
         so.FindProperty("titleText").objectReferenceValue = titleText;
         so.FindProperty("hintText").objectReferenceValue = hintText;
         so.FindProperty("startButton").objectReferenceValue = start;
         so.ApplyModifiedPropertiesWithoutUndo();

         return root.gameObject;
      }

      /// <summary>选队面板：角色/武器列 + 待加入摘要 + 槽位名单 + 操作按钮（契约见 BattleTeamSelectPanel）。</summary>
      private static GameObject BuildTeamSelectHierarchy(BattleTeamCatalog catalog)
      {
         var roles = catalog != null && catalog.Roles != null ? catalog.Roles : System.Array.Empty<RoleItem_Data>();
         var weapons = catalog != null && catalog.Weapons != null ? catalog.Weapons : System.Array.Empty<WeaponItem_Data>();

         var root = CreateRect("BattleTeamSelectPanel", null);
         SetStretch(root);
         var overlay = root.gameObject.AddComponent<Image>();
         overlay.color = new Color(0f, 0f, 0f, 0.55f);

         var panelComp = root.gameObject.AddComponent<BattleTeamSelectPanel>();
         if (panelComp == null) return null;

         var bg = CreateRect("TeamSelectPanel", root);
         SetCentered(bg, Vector2.zero, new Vector2(1080f, 720f));
         var bgImage = bg.gameObject.AddComponent<Image>();
         bgImage.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

         // 静态标题（不参与绑定，运行时只刷新 summaryText/rosterText）
         var titleRect = CreateRect("Title", bg);
         SetCentered(titleRect, new Vector2(0f, 310f), new Vector2(560f, 60f));
         var title = titleRect.gameObject.AddComponent<Text>();
         ApplyText(title, "出战编队", 46);
         title.raycastTarget = false;

         // 左列头：角色 / 武器
         CreateStaticLabel(bg, "RoleHeader", new Vector2(-365f, 248f), "角色", 26, new Color(0.75f, 0.75f, 0.8f, 1f));
         CreateStaticLabel(bg, "WeaponHeader", new Vector2(-155f, 248f), "武器", 26, new Color(0.75f, 0.75f, 0.8f, 1f));

         // 角色 / 武器 点选列（按钮子 Text 由运行时按编目名重写）
         var roleButtons = CreateButtonList(bg, "RoleButtons", -365f, roles.Length,
            roles.Select(r => r != null ? r.itemName : "空").ToArray(), "Role_{0}");
         var weaponButtons = CreateButtonList(bg, "WeaponButtons", -155f, weapons.Length,
            weapons.Select(w => w != null ? w.itemName : "空").ToArray(), "Weapon_{0}");

         // 待加入摘要（summaryText）：左下方
         var summaryRect = CreateRect("Summary", bg);
         SetCentered(summaryRect, new Vector2(-260f, -120f), new Vector2(460f, 130f));
         var summaryText = summaryRect.gameObject.AddComponent<Text>();
         ApplyText(summaryText, "点击 角色 + 武器 → 加入（1-4 人）", 24);
         summaryText.alignment = TextAnchor.UpperCenter;
         summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
         summaryText.verticalOverflow = VerticalWrapMode.Overflow;
         summaryText.raycastTarget = false;

         // 出战队伍名单（rosterText）：右侧面板，多行
         var rosterBg = CreateRect("RosterBox", bg);
         SetCentered(rosterBg, new Vector2(195f, 10f), new Vector2(310f, 540f));
         var rosterBgImage = rosterBg.gameObject.AddComponent<Image>();
         rosterBgImage.color = new Color(0f, 0f, 0f, 0.3f);

         var rosterRect = CreateRect("RosterText", rosterBg);
         SetStretchWithPadding(rosterRect, 12f, 12f);
         var rosterText = rosterRect.gameObject.AddComponent<Text>();
         ApplyText(rosterText, string.Empty, 20);
         rosterText.alignment = TextAnchor.UpperLeft;
         rosterText.horizontalOverflow = HorizontalWrapMode.Wrap;
         rosterText.verticalOverflow = VerticalWrapMode.Overflow;
         rosterText.raycastTarget = false;

         // 操作按钮列（右缘）
         var join = CreateButton(bg, "JoinButton", new Vector2(440f, 262f), "加入", new Vector2(180f, 50f), 24);
         var remove = CreateButton(bg, "RemoveButton", new Vector2(440f, 198f), "移除选中", new Vector2(180f, 50f), 24);
         var clear = CreateButton(bg, "ClearButton", new Vector2(440f, 134f), "清空", new Vector2(180f, 50f), 24);
         var up = CreateButton(bg, "UpButton", new Vector2(440f, 70f), "↑上移", new Vector2(180f, 50f), 24);
         var down = CreateButton(bg, "DownButton", new Vector2(440f, 6f), "↓下移", new Vector2(180f, 50f), 24);
         var start = CreateButton(bg, "StartButton", new Vector2(440f, -58f), "开始战斗", new Vector2(180f, 56f), 26);
         start.targetGraphic.color = new Color(0.1f, 0.6f, 0.3f, 1f);
         var back = CreateButton(bg, "BackButton", new Vector2(440f, -128f), "返回主场景", new Vector2(180f, 50f), 24);
         back.targetGraphic.color = new Color(0.45f, 0.45f, 0.5f, 1f);

         var so = new SerializedObject(panelComp);
         BindButtonArray(so, "roleButtons", roleButtons);
         BindButtonArray(so, "weaponButtons", weaponButtons);
         so.FindProperty("summaryText").objectReferenceValue = summaryText;
         so.FindProperty("rosterText").objectReferenceValue = rosterText;
         so.FindProperty("joinButton").objectReferenceValue = join;
         so.FindProperty("removeButton").objectReferenceValue = remove;
         so.FindProperty("clearButton").objectReferenceValue = clear;
         so.FindProperty("upButton").objectReferenceValue = up;
         so.FindProperty("downButton").objectReferenceValue = down;
         so.FindProperty("startButton").objectReferenceValue = start;
         so.FindProperty("backButton").objectReferenceValue = back;
         so.ApplyModifiedPropertiesWithoutUndo();

         return root.gameObject;
      }

      // ============================================================
      // 3. 场景
      // ============================================================

      private static bool RebuildMainHubScene()
      {
         if (!CanReplaceActiveScene(MainHubScenePath)) return false;

         var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
         scene.name = "M1_MainHub";

         var go = new GameObject("BattleMainHubBootstrap");
         go.AddComponent<BattleMainHubBootstrap>();

         EnsureScenesFolder();
         EditorSceneManager.SaveScene(scene, MainHubScenePath);
         AddToBuildSettings(MainHubScenePath);
         Debug.Log($"[T3.1A] 已重建主场景：{MainHubScenePath}（挂 BattleMainHubBootstrap，无其他内容；UIKit UIRoot 运行时自动创建）。");
         return true;
      }

      private static bool RebuildTeamSelectScene(BattleTeamCatalog catalog)
      {
         if (!CanReplaceActiveScene(TeamSelectScenePath)) return false;

         var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
         scene.name = "M1_TeamSelect";

         var go = new GameObject("BattleTeamSelectBootstrap");
         var bootstrap = go.AddComponent<BattleTeamSelectBootstrap>();

         var so = new SerializedObject(bootstrap);
         so.FindProperty("catalog").objectReferenceValue = catalog;
         so.ApplyModifiedPropertiesWithoutUndo();

         EnsureScenesFolder();
         EditorSceneManager.SaveScene(scene, TeamSelectScenePath);
         AddToBuildSettings(TeamSelectScenePath);
         Debug.Log($"[T3.1A] 已重建选队场景：{TeamSelectScenePath}（挂 BattleTeamSelectBootstrap，catalog 已绑定 {CatalogAssetPath}）。");
         return true;
      }

      /// <summary>当前活动场景有未保存修改时不重建（NewScene(Single) 会丢弃未保存内容，避免打断用户编辑）。</summary>
      private static bool CanReplaceActiveScene(string scenePath)
      {
         var active = EditorSceneManager.GetActiveScene();
         if (active.isDirty)
         {
            Debug.LogWarning($"[T3.1A] 跳过场景生成 {scenePath}：当前活动场景有未保存修改。请先保存（Ctrl+S）后重跑 Setup All（幂等，可直接重复）。");
            return false;
         }

         return true;
      }

      private static void EnsureScenesFolder()
      {
         var dir = Path.GetDirectoryName(MainHubScenePath);
         if (!string.IsNullOrEmpty(dir))
         {
            Directory.CreateDirectory(dir);
         }
      }

      /// <summary>去掉同路径后 Append（ConfigScene 作为 [0] 入口不被触碰）。</summary>
      private static void AddToBuildSettings(string scenePath)
      {
         var buildScenes = EditorBuildSettings.scenes.ToList();
         buildScenes.RemoveAll(s => s.path == scenePath);
         buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
         EditorBuildSettings.scenes = buildScenes.ToArray();
      }

      // ============================================================
      // 通用 UGUI 构建工具（与 BattleUiPrefabBuilder 同风格）
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
            Debug.LogWarning($"[T3.1A] 无法写入 AB 标记：{assetPath}（可稍后在 Inspector 手动设置 assetBundleName={bundleName}）。");
            return;
         }

         importer.SetAssetBundleNameAndVariant(bundleName, string.Empty);
         importer.SaveAndReimport();
      }

      private static void BindButtonArray(SerializedObject so, string propertyName, Button[] buttons)
      {
         var prop = so.FindProperty(propertyName);
         if (prop == null || !prop.isArray)
         {
            Debug.LogWarning($"[T3.1A] 未找到数组字段 {propertyName}，跳过绑定。");
            return;
         }

         prop.arraySize = buttons != null ? buttons.Length : 0;
         if (buttons != null)
         {
            for (var i = 0; i < buttons.Length; i++)
            {
               prop.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }
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

      private static void SetStretchWithPadding(RectTransform rect, float horizontalPadding, float verticalPadding)
      {
         rect.anchorMin = Vector2.zero;
         rect.anchorMax = Vector2.one;
         rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
         rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
      }

      private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
      {
         rect.anchorMin = new Vector2(0.5f, 0.5f);
         rect.anchorMax = new Vector2(0.5f, 0.5f);
         rect.pivot = new Vector2(0.5f, 0.5f);
         rect.sizeDelta = size;
         rect.anchoredPosition = position;
      }

      private static void CreateStaticLabel(Transform parent, string name, Vector2 position, string content,
         int fontSize, Color color)
      {
         var rect = CreateRect(name, parent);
         SetCentered(rect, position, new Vector2(220f, 40f));
         var text = rect.gameObject.AddComponent<Text>();
         ApplyText(text, content, fontSize);
         text.color = color;
         text.raycastTarget = false;
      }

      /// <summary>竖向按钮列（间距固定；数量 > 8 时允许向上多占空间，灰盒规模足够）。</summary>
      private static Button[] CreateButtonList(RectTransform parent, string containerName, float centerX, int count,
         string[] labels, string childNameFormat)
      {
         var buttons = new Button[count];
         var step = 58f;
         var startY = 196f;
         for (var i = 0; i < count; i++)
         {
            var name = string.Format(childNameFormat, i);
            var label = labels != null && i < labels.Length ? labels[i] : "空";
            var button = CreateButton(parent, name, new Vector2(centerX, startY - i * step), label, new Vector2(200f, 48f), 22);
            buttons[i] = button;
         }

         return buttons;
      }

      private static Button CreateButton(RectTransform parent, string name, Vector2 position, string label,
         Vector2 size, int fontSize)
      {
         var buttonRect = CreateRect(name, parent);
         SetCentered(buttonRect, position, size);
         var image = buttonRect.gameObject.AddComponent<Image>();
         image.color = new Color(0.2f, 0.45f, 0.9f, 1f);

         var button = buttonRect.gameObject.AddComponent<Button>();
         button.targetGraphic = image;

         var textRect = CreateRect("Text", buttonRect);
         SetStretch(textRect);
         var text = textRect.gameObject.AddComponent<Text>();
         ApplyText(text, label, fontSize);
         text.raycastTarget = false;

         return button;
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

      private static T[] LoadAssetsByGuids<T>(string[] guids, string label) where T : Object
      {
         var results = new List<T>();
         foreach (var guid in guids)
         {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
               Debug.LogError($"[T3.1A] 未找到{label}资源 GUID：{guid}");
               continue;
            }

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
               Debug.LogError($"[T3.1A] {label}资源加载失败：{path}");
               continue;
            }

            results.Add(asset);
         }

         return results.ToArray();
      }
   }
}
