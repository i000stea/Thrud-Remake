using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeaGame.App
{
   /// <summary>
   /// 场景引用表（ScriptableObject）。
   /// 用 GUID 锚定场景资产（重命名/移动场景文件不失效），
   /// 场景路径由编辑器从资产引用自动同步，运行时不写死名称/路径（用户需求）。
   /// 资产路径：Assets/Resources/SceneRefTable.asset（运行时经 Resources 或静态注入访问，热更接入后迁移）。
   /// </summary>
   [CreateAssetMenu(fileName = "SceneRefTable", menuName = "Thrud/SceneRefTable")]
   public sealed class SceneRefTable : ScriptableObject
   {
      /// <summary>
      /// 单条场景映射：逻辑枚举 ↔ 场景资产（GUID 锚定）↔ 自动同步的路径。
      /// </summary>
      [System.Serializable]
      public sealed class SceneEntry
      {
         /// <summary>逻辑场景标识（与场景文件名解耦）。</summary>
         public GameScene Scene;

#if UNITY_EDITOR
         /// <summary>
         /// 场景资产引用（编辑器专用，GUID 锚定）。
         /// 重命名/移动场景文件后引用依然有效，路径经 SyncPath 自动刷新。
         /// </summary>
         public SceneAsset Asset;
#endif

         /// <summary>
         /// 自动同步的场景路径（运行时读取；编辑器 OnValidate 时从资产更新）。
         /// </summary>
         [SerializeField, HideInInspector] private string mPath;

         /// <summary>场景路径（相对 Assets 目录）。</summary>
         public string Path => mPath;

         /// <summary>场景名称（由路径推导，不写死）。</summary>
         public string Name => string.IsNullOrEmpty(mPath) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(mPath);

#if UNITY_EDITOR
         /// <summary>
         /// 从场景资产引用同步路径。
         /// </summary>
         public void SyncPath()
         {
            mPath = Asset ? AssetDatabase.GetAssetPath(Asset) : null;
         }
#endif
      }

      /// <summary>
      /// 常驻管理场景（ConfigScene）条目。
      /// 独立于玩法场景枚举，因为它是入口/常驻层而非切换目标。
      /// </summary>
      public SceneEntry ConfigScene;

      /// <summary>
      /// 玩法场景条目列表。
      /// </summary>
      public SceneEntry[] Entries;

      /// <summary>
      /// 当前生效的表实例（由 ConfigScene 的 AppBootstrapper 注入，或 Resources 兜底加载）。
      /// </summary>
      private static SceneRefTable sCurrent;

      /// <summary>当前生效的表实例。</summary>
      public static SceneRefTable Current => sCurrent;

      /// <summary>
      /// 获取表实例：优先静态注入，兜底 Resources 加载（过渡实现，热更接入后迁移）。
      /// </summary>
      public static SceneRefTable Get()
      {
         if (sCurrent != null) return sCurrent;
         sCurrent = Resources.Load<SceneRefTable>("SceneRefTable");
         return sCurrent;
      }

      /// <summary>
      /// 注入当前表实例（由 AppBootstrapper 在 ConfigScene 初始化时调用）。
      /// </summary>
      public static void SetCurrent(SceneRefTable table)
      {
         sCurrent = table;
      }

      /// <summary>常驻管理场景路径。</summary>
      public string ConfigScenePath => ConfigScene != null ? ConfigScene.Path : null;

      /// <summary>常驻管理场景名称。</summary>
      public string ConfigSceneName => ConfigScene != null ? ConfigScene.Name : null;

      /// <summary>
      /// 获取玩法场景路径；未配置时返回 null 并警告。
      /// </summary>
      public string GetPath(GameScene scene)
      {
         if (Entries != null)
         {
            for (var i = 0; i < Entries.Length; i++)
            {
               var entry = Entries[i];
               if (entry != null && entry.Scene == scene)
               {
                  return entry.Path;
               }
            }
         }

         Debug.LogWarning($"[SceneRefTable] 未找到场景 {scene} 的引用条目，请在 SceneRefTable 资产中配置。");
         return null;
      }

      /// <summary>
      /// 获取玩法场景名称（由路径推导）。
      /// </summary>
      public string GetName(GameScene scene)
      {
         var path = GetPath(scene);
         return string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);
      }

#if UNITY_EDITOR
      /// <summary>
      /// 编辑器校验：同步所有条目的路径（场景资产变化/重命名/移动时自动刷新）。
      /// </summary>
      private void OnValidate()
      {
         ConfigScene?.SyncPath();

         if (Entries != null)
         {
            for (var i = 0; i < Entries.Length; i++)
            {
               Entries[i]?.SyncPath();
            }
         }
      }
#endif
   }
}
