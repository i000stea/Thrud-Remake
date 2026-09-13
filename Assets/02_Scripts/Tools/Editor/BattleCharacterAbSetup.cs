using System.IO;
using QFramework;
using UnityEditor;
using UnityEngine;

namespace TeaGame.Tools.Editor
{
   /// <summary>
   /// 角色/武器资源 AB 标记与构建工具（真机资源分阶段修复指南 P0 / T0-1 的 ResKit 侧入口）。
   ///
   /// **为什么需要本工具（而不是手工在 Inspector 拖，也不是用 QFramework 自带菜单）**：
   /// - ResKit 的 AB 加载路径只有一条：资产必须有 `assetBundleName`（ResKit 以它作 bundle 名、
   ///   以资产文件名作 asset 名），运行时才能 `ResLoader.LoadSync(bundle, asset)` 取到；
   /// - QFramework 自带的 `Assets/@ResKit - AssetBundle Mark` 是**按所选目录名**当 bundle 名，
   ///   粒度到目录：标 `Assets/Data` 会把 `BattleLevels/**`（被场景引用，标了就真机缺源）和三十多个
   ///   未使用角色 SO 一起卷进去，所以不能用；
   /// - 本工具只标**一个根资产** `Assets/Data/BattleTeamCatalog.asset`：角色/武器 SO、模型 prefab、
   ///   材质贴图作为**依赖闭包自动入包**，bundle 名固定为运行时常量
   ///   <c>BattleAssetService.RoleDataBundleName</c>，不会与代码脱钩；
   /// - 标记后回读校验 + 构建后校验产物，避免"标了但没打 AB / 清单里没有"的假成功。
   ///
   /// 使用顺序：① 标记 → ② 编辑器直接 Play 验证（模拟模式不需要出包）→ ③ 构建 AB → 出包真机。
   /// </summary>
   public static class BattleCharacterAbSetup
   {
      private const string MenuRoot = "Thrud/Demo/Characters/";

      /// <summary>编目资产路径（唯一需要打标的资产）。</summary>
      private const string CatalogPath = "Assets/Data/BattleTeamCatalog.asset";

      /// <summary>与 <c>BattleAssetService.RoleDataBundleName</c> 必须完全一致。</summary>
      private const string BundleName = "battle_characters";

      /// <summary>ResKit 以"资产文件名（无扩展名、小写）"作 asset 名。</summary>
      private static string CatalogAssetName =>
         Path.GetFileNameWithoutExtension(CatalogPath).ToLower();

      // ---------------- 菜单 ----------------

      [MenuItem(MenuRoot + "1. 标记编目为 AB (battle_characters)")]
      public static void MarkCatalog()
      {
         var importer = AssetImporter.GetAtPath(CatalogPath);
         if (importer == null)
         {
            Debug.LogError($"[T0-1] 未找到资产：{CatalogPath}（请确认路径未改名/未移动）。");
            return;
         }

         if (importer.assetBundleName == BundleName)
         {
            Debug.Log($"[T0-1] 已是目标标记（{CatalogPath} → {BundleName}），无需改动。");
            Verify();
            return;
         }

         if (!string.IsNullOrEmpty(importer.assetBundleName))
         {
            Debug.LogWarning($"[T0-1] 该资产原本标在 AB「{importer.assetBundleName}」，现改为「{BundleName}」。");
         }

         importer.assetBundleName = BundleName;
         importer.SaveAndReimport(); // 写回 .meta 并重导入（不要手改 .meta）
         AssetDatabase.RemoveUnusedAssetBundleNames();
         AssetDatabase.Refresh();

         Debug.Log($"[T0-1] 已标记：{CatalogPath} → assetBundleName「{BundleName}」。" +
                   $"运行时用 ({BundleName}, {CatalogAssetName}) 取值。" +
                   "角色/武器 SO、模型 prefab、贴图会作为依赖闭包自动入包（无需逐个标记）。");
         Verify();
      }

      [MenuItem(MenuRoot + "2. 构建 AB（当前平台）并校验")]
      public static void BuildAssetBundles()
      {
         Debug.Log($"[T0-2] 开始构建 AB（当前目标平台 {EditorUserBuildSettings.activeBuildTarget}，bundle={BundleName}）…");
         ResKitEditorAPI.BuildAssetBundles(); // 与 Thrud/Demo/Android/2 同一入口（含 QAssets.cs 重新生成）
         AssetDatabase.Refresh();

         var root = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
         var hits = Directory.Exists(root)
            ? Directory.GetFiles(root, BundleName, SearchOption.AllDirectories)
            : new string[0];

         if (hits.Length == 0)
         {
            Debug.LogError($"[T0-2] 构建结束但未找到产物 {BundleName}（查找目录：{root}）。" +
                           "请先执行「1. 标记编目为 AB」并确认 Console 无打包报错。");
            return;
         }

         for (var i = 0; i < hits.Length; i++)
         {
            var sizeKb = new FileInfo(hits[i]).Length / 1024f;
            Debug.Log($"[T0-2] ✅ AB 产物：{hits[i]}（{sizeKb:F1} KB）");
         }

         Debug.Log("[T0-2] 提示：QAssets.cs 已随构建重新生成，如需可用 QAssetBundle.Battle_characters 常量" +
                   "（BattleAssetService 目前用硬编码常量，避免首次构建前编译不过）。");
         Verify();
      }

      [MenuItem(MenuRoot + "3. 校验标记与包内容（只读）")]
      public static void VerifyMenu()
      {
         Verify();
      }

      [MenuItem(MenuRoot + "9. 取消编目标记（回滚）")]
      public static void UnmarkCatalog()
      {
         var importer = AssetImporter.GetAtPath(CatalogPath);
         if (importer == null)
         {
            Debug.LogError($"[T0-1] 未找到资产：{CatalogPath}。");
            return;
         }

         if (string.IsNullOrEmpty(importer.assetBundleName))
         {
            Debug.Log("[T0-1] 该资产当前没有 AB 标记，无需回滚。");
            return;
         }

         Debug.LogWarning($"[T0-1] 已取消 {CatalogPath} 的 AB 标记（原「{importer.assetBundleName}」）。" +
                          "此时运行时将回落到场景引用/编辑器直读；请记得重新构建 AB 让清单同步。");
         importer.assetBundleName = null;
         importer.SaveAndReimport();
         AssetDatabase.RemoveUnusedAssetBundleNames();
         AssetDatabase.Refresh();
      }

      // ---------------- 校验 ----------------

      /// <summary>回读标记与包内容（只读，任何菜单项结束后都会跑一次）。</summary>
      private static void Verify()
      {
         var importer = AssetImporter.GetAtPath(CatalogPath);
         if (importer == null)
         {
            Debug.LogError($"[T0-1][校验] 未找到资产：{CatalogPath}。");
            return;
         }

         if (importer.assetBundleName != BundleName)
         {
            Debug.LogError($"[T0-1][校验] 标记不正确：当前「{importer.assetBundleName}」，" +
                           $"期望「{BundleName}」。请执行菜单「1. 标记编目为 AB」。");
            return;
         }

         var inBundle = AssetDatabase.GetAssetPathsFromAssetBundle(BundleName);
         var deps = AssetDatabase.GetDependencies(CatalogPath, true);

         Debug.Log($"[T0-1][校验] 通过：{CatalogPath} ∈ bundle「{BundleName}」；" +
                   $"该 bundle 直接包含 {inBundle.Length} 个资产，编目递归依赖 {deps.Length} 个（" +
                   "后者会作为依赖闭包随包一起去，其中被其他 bundle/场景引用的部分会保留一份在构建里，属正常）。");
      }
   }
}
