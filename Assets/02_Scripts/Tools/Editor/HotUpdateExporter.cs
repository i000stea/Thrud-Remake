using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TeaGame.Tools.Editor
{
   /// <summary>
   /// 热更 DLL 导出工具（HybridCLR，Docs 16 §5C 的 C2 收尾）。
   ///
   /// 把 HybridCLR 编译好的热更程序集与"AOT 补充元数据 DLL"一起复制到
   /// `Assets/StreamingAssets/HotUpdate/`，并生成 `hotfix.json` 清单——
   /// 运行时由 AOT 侧的 `TeaGame.App.HotUpdateLoader` 读取。
   ///
   /// 前置：先跑 `HybridCLR/Generate/All`（它会编译 HotUpdateDlls 并生成
   /// `Library/HybridCLR/AssembliesPostIl2CppStrip/<目标平台>` 与 `AOTGenericReferences`）。
   ///
   /// 菜单：`Thrud/HotUpdate/1. 导出热更 DLL 与清单到 StreamingAssets`
   /// </summary>
   public static class HotUpdateExporter
   {
      private const string MenuRoot = "Thrud/HotUpdate/";

      /// <summary> 输出目录（相对工程根）。 </summary>
      private const string OutputDir = "Assets/StreamingAssets/HotUpdate";

      /// <summary> 清单文件名，必须与 TeaGame.App.HotUpdateLoader.ManifestName 一致。 </summary>
      private const string ManifestName = "hotfix.json";

      /// <summary>
      /// 热更程序集名（与 HybridCLR/Settings 里配置的 6 个 asmdef 一致），按依赖顺序排列。
      /// TeaGame.App 是 AOT，**不在此列**。
      /// </summary>
      private static readonly string[] HotUpdateAssemblies =
      {
         "TeaGame.Data",
         "TeaGame.Tools",
         "TeaGame.Battle.Contracts",
         "TeaGame.Battle",
         "TeaGame.Menu",
         "TeaGame.Gacha"
      };

      [Serializable]
      private sealed class Manifest
      {
         public string[] aotMetadata = new string[0];
         public string[] hotUpdate = new string[0];
      }

      [MenuItem(MenuRoot + "1. 导出热更 DLL 与清单到 StreamingAssets", false, 10)]
      public static void ExportHotUpdateDlls()
      {
         string target = EditorUserBuildSettings.activeBuildTarget.ToString();
         string projectRoot = Directory.GetParent(Application.dataPath).FullName;
         string hotDir = Path.Combine(projectRoot, "Library/HybridCLR/HotUpdateDlls", target);
         string aotDir = Path.Combine(projectRoot, "Library/HybridCLR/AssembliesPostIl2CppStrip", target);

         if (!Directory.Exists(hotDir))
         {
            Debug.LogError("[HotUpdate] 找不到热更 DLL 目录：" + hotDir +
                           "。请先执行 HybridCLR/Generate/All（当前目标平台：" + target + "）。");
            return;
         }

         List<string> aotNames = ReadPatchedAotAssemblyList();
         if (aotNames.Count == 0)
         {
            Debug.LogError("[HotUpdate] 读不到 AOTGenericReferences.PatchedAOTAssemblyList。" +
                           "请先执行 HybridCLR/Generate/All。");
            return;
         }

         Directory.CreateDirectory(OutputDir);

         // 清掉上一轮的 dll，避免改名/删减后残留（残留会被清单之外的逻辑读到或被误打进包）
         foreach (string oldFile in Directory.GetFiles(OutputDir, "*.dll"))
         {
            File.Delete(oldFile);
         }

         long totalBytes = 0;
         var hotFiles = new List<string>();
         foreach (string asmName in HotUpdateAssemblies)
         {
            string src = Path.Combine(hotDir, asmName + ".dll");
            if (!File.Exists(src))
            {
               Debug.LogError("[HotUpdate] 缺少热更 DLL：" + src + "（该程序集可能没编译出来）");
               return;
            }

            totalBytes += CopyToOutput(src);
            hotFiles.Add(asmName + ".dll");
         }

         var aotFiles = new List<string>();
         foreach (string aotName in aotNames)
         {
            string src = Path.Combine(aotDir, aotName);
            if (!File.Exists(src))
            {
               Debug.LogWarning("[HotUpdate] 缺少 AOT 元数据 DLL，已跳过：" + src);
               continue;
            }

            totalBytes += CopyToOutput(src);
            aotFiles.Add(aotName);
         }

         var manifest = new Manifest
         {
            aotMetadata = aotFiles.ToArray(),
            hotUpdate = hotFiles.ToArray()
         };
         File.WriteAllText(Path.Combine(OutputDir, ManifestName), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
         AssetDatabase.Refresh();

         Debug.Log("[HotUpdate] 导出完成（目标平台 " + target + "）：热更程序集 " + hotFiles.Count +
                   " 个、AOT 元数据 " + aotFiles.Count + " 个，合计 " +
                   Math.Round(totalBytes / 1024.0 / 1024.0, 2) + " MB → " + OutputDir);
      }

      [MenuItem(MenuRoot + "2. 打印当前热更配置（只读）", false, 20)]
      public static void PrintHotUpdateState()
      {
         string target = EditorUserBuildSettings.activeBuildTarget.ToString();
         string projectRoot = Directory.GetParent(Application.dataPath).FullName;
         string hotDir = Path.Combine(projectRoot, "Library/HybridCLR/HotUpdateDlls", target);

         var sb = new StringBuilder();
         sb.AppendLine("[HotUpdate] 当前状态");
         sb.AppendLine("  目标平台：" + target);
         sb.AppendLine("  热更程序集（导出清单）：" + string.Join(", ", HotUpdateAssemblies));
         sb.AppendLine("  热更 DLL 源目录：" + hotDir + (Directory.Exists(hotDir) ? "（存在）" : "（缺失，先跑 Generate/All）"));
         List<string> aot = ReadPatchedAotAssemblyList();
         sb.AppendLine("  AOT 补充元数据（" + aot.Count + "）：" + (aot.Count > 0 ? string.Join(", ", aot) : "未生成"));
         sb.AppendLine("  导出目录：" + OutputDir + (Directory.Exists(OutputDir) ? "（存在）" : "（尚未导出）"));
         Debug.Log(sb.ToString());
      }

      /// <summary> 从 Assembly-CSharp 的生成类里读 AOT 补充元数据清单。 </summary>
      private static List<string> ReadPatchedAotAssemblyList()
      {
         var names = new List<string>();
         Type type = Type.GetType("AOTGenericReferences, Assembly-CSharp");
         if (type == null)
         {
            // 装配域里直接找（生成类在全局命名空间）
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length && type == null; i++)
            {
               try
               {
                  type = assemblies[i].GetType("AOTGenericReferences");
               }
               catch
               {
                  // 某些动态程序集会抛，忽略即可
               }
            }
         }

         if (type == null)
         {
            return names;
         }

         FieldInfo field = type.GetField("PatchedAOTAssemblyList",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
         if (field == null)
         {
            return names;
         }

         if (field.GetValue(null) is IEnumerable<string> list)
         {
            foreach (string item in list)
            {
               if (!string.IsNullOrEmpty(item))
               {
                  names.Add(item);
               }
            }
         }

         return names;
      }

      private static long CopyToOutput(string sourcePath)
      {
         string fileName = Path.GetFileName(sourcePath);
         string dest = Path.Combine(OutputDir, fileName);
         File.Copy(sourcePath, dest, true);
         return new FileInfo(dest).Length;
      }
   }
}
