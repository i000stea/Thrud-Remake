using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HybridCLR;
using UnityEngine;
using UnityEngine.Networking;

namespace TeaGame.App
{
   /// <summary>
   /// AOT 侧热更装配加载器（HybridCLR，见 `Docs/03_专项设计与重构/16_热更新方案选型与落地计划.md` §5C 的 C2）。
   ///
   /// **为什么必须在 AOT 层、且必须最早执行**：热更 DLL 里的类型在被任何场景对象反序列化、
   /// 被任何代码 `new` 之前必须先完成 `Assembly.Load`；而加载器本身要调用 HybridCLR 的
   /// `RuntimeApi.LoadMetadataForAOTAssembly`，这条 API 只存在于 AOT 侧的 `HybridCLR.Runtime`。
   /// `TeaGame.App` 是本工程唯一的 AOT 程序集（17 文档的 AOT 归属决策），故加载器放这里；
   /// 0ConfigScene 已验证为**纯 AOT 场景**（只有 TeaGame.App + Unity/TMP/QFramework），
   /// 由 `AppBootstrapper.Start()` 在 ResKit 初始化与首个场景加载之前 `yield` 本方法。
   ///
   /// **两个 DLL 来源（顺序固定）**：
   /// 1. `Application.persistentDataPath/HotUpdate/` —— 将来的增量更新落点，优先；
   /// 2. `Application.streamingAssetsPath/HotUpdate/` —— 随包基线，回退。
   ///
   /// Android / WebGL 上 StreamingAssets 在 APK/包内，不能用 `File.ReadAllBytes`，
   /// 必须走 `UnityWebRequest`；PC 直连则直接读文件。
   ///
   /// 编辑器下整体跳过：Unity 用的是工程内的程序集本身，重复 `Assembly.Load` 会造成双份类型。
   /// </summary>
   public static class HotUpdateLoader
   {
      /// <summary> 热更资源子目录名（persistentDataPath 与 streamingAssetsPath 下同名）。 </summary>
      public const string DirName = "HotUpdate";

      /// <summary> 热更清单文件名。 </summary>
      public const string ManifestName = "hotfix.json";

      /// <summary> 是否已完成（含"编辑器跳过"与"清单缺失"这两种已收尾的状态）。 </summary>
      public static bool IsReady { get; private set; }

      /// <summary> 成功补充元数据的 AOT DLL 数。 </summary>
      public static int LoadedAotMetadata { get; private set; }

      /// <summary> 成功加载的热更程序集数。 </summary>
      public static int LoadedHotUpdate { get; private set; }

      /// <summary> 清单结构；字段名与 `hotfix.json` 一致（由 HotUpdateExporter 生成）。 </summary>
      [Serializable]
      private sealed class HotfixManifest
      {
         /// <summary> 需要补充元数据的 AOT DLL 文件名。 </summary>
         public string[] aotMetadata = new string[0];

         /// <summary> 热更程序集 DLL 文件名，按依赖顺序排列。 </summary>
         public string[] hotUpdate = new string[0];
      }

      private static string PersistentDir
      {
         get { return Path.Combine(Application.persistentDataPath, DirName); }
      }

      private static string StreamingDir
      {
         get { return Path.Combine(Application.streamingAssetsPath, DirName); }
      }

      /// <summary> StreamingAssets 是否为"包内压缩路径"（不能用 File API 直读）。 </summary>
      private static bool StreamingNeedsWebRequest
      {
         get
         {
            return Application.platform == RuntimePlatform.Android
                   || Application.platform == RuntimePlatform.WebGLPlayer;
         }
      }

      /// <summary>
      /// 加载全部热更装配。调用方用 `yield return HotUpdateLoader.LoadAll();` 等待完成，
      /// 完成后再触碰任何热更程序集里的类型。
      /// </summary>
      public static IEnumerator LoadAll()
      {
         IsReady = false;
         LoadedAotMetadata = 0;
         LoadedHotUpdate = 0;

#if UNITY_EDITOR
         Debug.Log("[HotUpdate] 编辑器环境：跳过热更装配加载（Unity 直接使用工程内程序集）。");
         IsReady = true;
         yield break;
#else
         string manifestJson = null;
         yield return ReadText(ManifestName, text => manifestJson = text);

         if (string.IsNullOrEmpty(manifestJson))
         {
            Debug.LogError("[HotUpdate] 未找到热更清单 " + DirName + "/" + ManifestName +
                           "：热更程序集不会加载（本次运行将只剩 AOT 代码）。");
            yield break;
         }

         HotfixManifest manifest = null;
         try
         {
            manifest = JsonUtility.FromJson<HotfixManifest>(manifestJson);
         }
         catch (Exception e)
         {
            Debug.LogError("[HotUpdate] 热更清单解析失败：" + e.Message);
         }

         if (manifest == null)
         {
            yield break;
         }

         // 第一步：补 AOT 泛型元数据。必须在加载热更程序集之前完成，
         // 否则热更代码调用 AOT 泛型实例时会抛 "method not found"。
         string[] aotList = manifest.aotMetadata ?? new string[0];
         for (int i = 0; i < aotList.Length; i++)
         {
            string fileName = aotList[i];
            byte[] bytes = null;
            yield return ReadBytes(fileName, data => bytes = data);
            if (bytes == null || bytes.Length == 0)
            {
               Debug.LogWarning("[HotUpdate] 跳过缺失的 AOT DLL：" + fileName);
               continue;
            }

            LoadImageErrorCode result = RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
            if (result == LoadImageErrorCode.OK)
            {
               LoadedAotMetadata++;
            }
            else
            {
               Debug.LogError("[HotUpdate] 补充元数据失败：" + fileName + " -> " + result);
            }
         }

         // 第二步：按清单顺序加载热更程序集。
         string[] hotList = manifest.hotUpdate ?? new string[0];
         for (int i = 0; i < hotList.Length; i++)
         {
            string fileName = hotList[i];
            string assemblyName = Path.GetFileNameWithoutExtension(fileName);
            if (FindLoadedAssembly(assemblyName) != null)
            {
               Debug.LogWarning("[HotUpdate] 程序集已存在，跳过重复加载：" + assemblyName);
               continue;
            }

            byte[] bytes = null;
            yield return ReadBytes(fileName, data => bytes = data);
            if (bytes == null || bytes.Length == 0)
            {
               Debug.LogError("[HotUpdate] 热更 DLL 缺失：" + fileName);
               continue;
            }

            try
            {
               Assembly.Load(bytes);
               LoadedHotUpdate++;
            }
            catch (Exception e)
            {
               Debug.LogError("[HotUpdate] 加载失败：" + assemblyName + " -> " + e);
            }
         }

         IsReady = true;
         Debug.Log("[HotUpdate] 热更装配就绪：AOT 元数据 " + LoadedAotMetadata + "/" + aotList.Length +
                   "，热更程序集 " + LoadedHotUpdate + "/" + hotList.Length + "。");
#endif
      }

      /// <summary> 在已加载的程序集里按名字查找（用于避免重复 `Assembly.Load`）。 </summary>
      private static Assembly FindLoadedAssembly(string assemblyName)
      {
         Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
         for (int i = 0; i < assemblies.Length; i++)
         {
            Assembly assembly = assemblies[i];
            if (assembly == null)
            {
               continue;
            }

            if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
               return assembly;
            }
         }

         return null;
      }

      /// <summary> 读文本（清单）。 </summary>
      private static IEnumerator ReadText(string fileName, Action<string> onDone)
      {
         byte[] bytes = null;
         yield return ReadBytes(fileName, data => bytes = data);
         onDone(bytes == null || bytes.Length == 0 ? null : Encoding.UTF8.GetString(bytes));
      }

      /// <summary> 读字节：persistentDataPath 优先，回退 streamingAssetsPath。 </summary>
      private static IEnumerator ReadBytes(string fileName, Action<byte[]> onDone)
      {
         string persistentPath = Path.Combine(PersistentDir, fileName);
         if (File.Exists(persistentPath))
         {
            onDone(SafeReadAllBytes(persistentPath));
            yield break;
         }

         string streamingPath = Path.Combine(StreamingDir, fileName);
         if (StreamingNeedsWebRequest)
         {
            using (UnityWebRequest request = UnityWebRequest.Get(streamingPath))
            {
               yield return request.SendWebRequest();
               if (request.result != UnityWebRequest.Result.Success)
               {
                  Debug.LogWarning("[HotUpdate] 读取失败 " + streamingPath + " : " + request.error);
                  onDone(null);
               }
               else
               {
                  onDone(request.downloadHandler.data);
               }
            }

            yield break;
         }

         if (!File.Exists(streamingPath))
         {
            onDone(null);
            yield break;
         }

         onDone(SafeReadAllBytes(streamingPath));
      }

      /// <summary> 读文件，失败返回 null 并记一条错误（热更资源缺失不该以异常形式炸启动流程）。 </summary>
      private static byte[] SafeReadAllBytes(string path)
      {
         try
         {
            return File.ReadAllBytes(path);
         }
         catch (Exception e)
         {
            Debug.LogError("[HotUpdate] 读取异常 " + path + " : " + e.Message);
            return null;
         }
      }
   }
}
