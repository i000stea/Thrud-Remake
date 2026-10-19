using System;

namespace TeaGame.App
{
   /// <summary>
   /// OTA 版本清单（`Docs/03_专项设计与重构/23_…` §4）。
   /// 字段名与 `manifest.json` 一一对应；**只用 public 字段**，因为 JsonUtility 不认属性。
   /// 代码 DLL、场景 AssetBundle、AB 清单同属一份 contentVersion，避免「代码新、资源旧」的跨版本组合。
   /// </summary>
   [Serializable]
   public sealed class HotUpdateManifest
   {
      /// <summary>本客户端唯一支持读取的清单结构版本。变更必须显式拒绝或兼容读取，不得静默按旧格式解析。</summary>
      public const int SupportedSchemaVersion = 1;

      /// <summary>清单结构版本。</summary>
      public int schemaVersion;

      /// <summary>内容版本号（代码与资源共同绑定到这个值）。</summary>
      public string contentVersion;

      /// <summary>渠道名（如 `lan`），必须与客户端配置一致。</summary>
      public string channel;

      /// <summary>平台名（如 `Android`），大小写不敏感。</summary>
      public string platform;

      /// <summary>
      /// 本次内容是否构成**完整** AB 集合（含 `asset_bundle_config.bin`）。
      /// 只有为 true 且清单里确实带清单文件时，才允许把 ResKit 切到 persistent 全量；
      /// 否则保持包内基线清单，仅对清单里列出的单个 bundle 做逐文件覆盖。
      /// 这是「只替换一个 bundle」与「整包替换」共存而不互相破坏的分界线。
      /// </summary>
      public bool abSetComplete;

      /// <summary>文件条目。</summary>
      public HotUpdateFileEntry[] files = new HotUpdateFileEntry[0];
   }

   /// <summary>
   /// 清单里的一个文件条目。
   /// </summary>
   [Serializable]
   public sealed class HotUpdateFileEntry
   {
      /// <summary>资源包（ResKit AssetBundle，无扩展名的构建产物）。</summary>
      public const string KindAssetBundle = "assetBundle";

      /// <summary>热更代码程序集。</summary>
      public const string KindHotUpdateDll = "hotUpdateDll";

      /// <summary>热更清单（`hotfix.json`），HotUpdateLoader 按它的顺序装配程序集。</summary>
      public const string KindHotUpdateManifest = "hotUpdateManifest";

      /// <summary>稳定标识（日志与状态里用它，而不是文件名）。</summary>
      public string id;

      /// <summary>类型，决定本地落盘目录：`assetBundle` 进 AB 目录，其余进 HotUpdate 目录。</summary>
      public string kind;

      /// <summary>
      /// 相对「`{channel}/{platform}/`」的下载路径（服务端布局，例如 `AssetBundles/Android/main_scene_0002_main`）。
      /// **只用于拼 URL**，不用来算本地落点——本地落点由 kind 决定，服务端怎么组织目录都不影响客户端。
      /// </summary>
      public string path;

      /// <summary>文件字节数（用于校验与进度）。</summary>
      public long size;

      /// <summary>文件 SHA-256（小写十六进制）。</summary>
      public string sha256;

      /// <summary>是否必需：必需文件任何一个失败，整次更新作废并保留旧版本。</summary>
      public bool required;

      /// <summary>文件名（取 <see cref="path"/> 的最后一段）。</summary>
      public string FileName
      {
         get
         {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var index = path.LastIndexOf('/');
            return index >= 0 ? path.Substring(index + 1) : path;
         }
      }

      /// <summary>是否资源包条目。</summary>
      public bool IsAssetBundle => string.Equals(kind, KindAssetBundle, StringComparison.OrdinalIgnoreCase);
   }
}
