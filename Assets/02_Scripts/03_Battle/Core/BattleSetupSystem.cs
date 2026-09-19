using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 入场数据解析系统（2026-09-19「宿主瘦身」第 2 步：自 <see cref="BattleSceneRuntimeHost"/> 整段搬出）。
   ///
   /// **为什么独立成 System，而不是并入 `BattleLevelFlow`**（21 文档 §7 未决项 1 的结论）：
   /// 入场数据解析是**一次性的**（装配期跑完即止），而 `BattleLevelFlow` 是**全程驻留的关卡状态机**——
   /// 两者生命周期不同，混在一起会让状态机多出一次性职责。
   ///
   /// **边界（21 文档新增纪律 5「System 不驱动 View」）**：
   /// - 本系统**只解析数据并返回结果**：本场关卡数据、出战成员运行时数据、以及"正式构建装配失败需退回入口"的标志；
   /// - **不驱动任何 View**：出生点传送、<c>BattleStageDirector.Setup</c>、退回入口场景（清面板表 + <c>BattleSceneGate.TransitionTo</c>）
   ///   一律由宿主／装配器执行；
   /// - **日志文案与搬运前逐字一致**——真机排查依赖这些字符串（`DescribeCatalogState` 的三个分支是 2026-09-10 教训的产物，
   ///   见 09 文档关键坑 28/30）。
   ///
   /// **无状态**：场景固件（关卡目录 / 出战编目）由宿主按次作为参数传入，本类不缓存——
   /// 架构级 System 常驻跨场景，持有场景级引用会变成"上一场战斗的悬垂引用"（21 文档 §2.2 反例 1）。
   ///
   /// 搬运来源与逐行对照见 `Docs/03_专项设计与重构/21_战斗场景装配与宿主瘦身_设计与落地指导.md` §4 步骤 2。
   /// </summary>
   public sealed class BattleSetupSystem : AbstractSystem
   {
      /// <summary>编辑器直连默认编队目录路径（无出战会话时的兜底数据源）。</summary>
      private const string EditorTeamCatalogPath = "Assets/Data/BattleTeamCatalog.asset";

      protected override void OnInit() { }

      /// <summary>
      /// 解析本场关卡：<see cref="BattleLevelSession"/> 选关缓存（须指向本场景）优先；否则 D12 直连默认。
      /// </summary>
      /// <param name="artSceneName">当前战斗场景名（宿主传入 <c>gameObject.scene.name</c>）。</param>
      /// <param name="catalog">关卡目录（宿主上的场景固件引用，可为空）。</param>
      /// <param name="selected">解析出的关卡数据。</param>
      public bool TryResolveLevel(string artSceneName, BattleLevelCatalog catalog, out BattleLevelData selected)
      {
         selected = BattleLevelSession.Selected;

         if (selected != null)
         {
            if (selected.ArtSceneName == artSceneName)
            {
               return true;
            }

            // 这个地方的美术判断不应该放在这里？还是说这里只是做一个保底检测
            Debug.LogWarning($"[battle_runtime] 会话缓存关卡 {selected.LevelId} 属于场景 {selected.ArtSceneName}，" +
                             $"与本场景 {artSceneName} 不符，回落直连默认（D12）。");
         }

         // 关卡为主，美术场景错误不应该回滚到相同美术场景的首个关卡？还是说这个仅限于编辑器配置
         if (catalog != null)
         {
            selected = catalog.FindFirstByArtScene(artSceneName);
            if (selected != null)
            {
               return true;
            }
         }

         Debug.LogError(
            $"[battle_runtime] 场景「{artSceneName}」未解析到关卡数据" +
            "（关卡目录未登记本场景或目录缺失），battle 场景仅保底运行（无波次数据）。");
         return false;
      }

      /// <summary>出战数据解析结果（数据 + 是否需要退回入口的**标志**，不是动作）。</summary>
      public struct BattleTeamResolution
      {
         /// <summary>出战成员运行时数据（解析失败时为空数组，宿主按"空队保底运行"处理）。</summary>
         public BattleRoleRuntimeData[] Datas;

         /// <summary>
         /// 正式构建且无法装配编队 → 需要退回入口场景。<b>仅标志位</b>：
         /// 清面板表与 <see cref="BattleSceneGate.TransitionTo"/> 由宿主执行（System 不驱动 View/流程）。
         /// </summary>
         public bool RetreatToEntryScene;
      }

      /// <summary>
      /// 解析出战角色数据（T3.1A 数据管道；2026-09-08 由 rig 上移至 battle_runtime，2026-09-19 由宿主搬入本系统）：
      /// 1. <see cref="BattleTeamSession"/> 有选队 → 经出战编目把草稿的 itemID 重解析为 SO 后实体化；
      ///    编目缺失或成员 id 未命中（编目为空/数据更换/目录缺项）→ 清空会话回落；
      /// 2. 编辑器直连（无会话）→ 用 <see cref="BattleTeamCatalog"/>（BattleTeamCatalog.asset）默认编队兜底；
      /// 3. 正式构建且无会话/编目 → 流程错误：报错并置"退回入口"标志（不开空队）。
      /// </summary>
      /// <param name="sceneFallbackCatalog">场景上悬挂的出战编目（可为空；AB 解析成功时用不到）。</param>
      public BattleTeamResolution ResolveBattleDatas(BattleTeamCatalog sceneFallbackCatalog)
      {
         var resolvedCatalog = ResolveTeamCatalog(sceneFallbackCatalog);
         var hadSession = BattleTeamSession.HasSelection;

         // 失败原因逐层记录（此前三种原因共用一条日志，真机排查靠猜；2026-09-10 教训）
         string failure = null;

         if (hadSession)
         {
            if (resolvedCatalog == null)
            {
               failure = "出战会话存在，但编目未解析到";
               Debug.LogWarning($"[battle_runtime] {failure}——{DescribeCatalogState(null)}；已清空会话。");
               BattleTeamSession.Clear();
            }
            else
            {
               var built = BattleTeamRuntimeBuilder.Build(BattleTeamSession.Members, resolvedCatalog);
               if (built.Length > 0)
               {
                  return new BattleTeamResolution { Datas = built };
               }

               failure = "编目已解析，但会话成员未在其中命中完整 SO";
               Debug.LogWarning(
                  $"[battle_runtime] {failure}——编目：{DescribeCatalogState(resolvedCatalog)}；" +
                  $"会话 id（角色+武器）：{DescribeSessionIds()}。已清空会话，请重新走选关 → 编队。");
               BattleTeamSession.Clear();
            }
         }
         else
         {
            failure = "出战会话为空（未走「选关 → 编队 → 开始出战」提交）";
         }

#if UNITY_EDITOR
         var fallback = BuildDefaultFromCatalog(resolvedCatalog);
         if (fallback != null && fallback.Length > 0)
         {
            return new BattleTeamResolution { Datas = fallback };
         }

         Debug.LogWarning($"[battle_runtime] 编辑器直连未找到默认编队目录（{EditorTeamCatalogPath}），空队保底运行。" +
                          $"原因：{failure}");
         return new BattleTeamResolution { Datas = System.Array.Empty<BattleRoleRuntimeData>() };
#else
         Debug.LogError(
            $"[battle_runtime] 正式环境无法装配出战编队：{failure}。\n" +
            $"  编目：{DescribeCatalogState(resolvedCatalog)}\n" +
            $"  入口：{BattleTeamSession.FormationEntrySceneName} 的「选关 → 编队 → 开始出战」\n" +
            $"  资源：Assets/Data/BattleTeamCatalog.asset 需标记到 AB「{BattleAssetService.RoleDataBundleName}」" +
            "并已构建（真机不会走编辑器的 Assets/Data 直读回落）。\n" +
            $"  已退回 {BattleTeamSession.ReturnSceneName}。");
         return new BattleTeamResolution
         {
            Datas = System.Array.Empty<BattleRoleRuntimeData>(),
            RetreatToEntryScene = true
         };
#endif
      }

      /// <summary>编目状态描述（诊断用：区分"清单里没有""清单里有但加载失败""已解析但内容不对"）。</summary>
      private static string DescribeCatalogState(BattleTeamCatalog resolved)
      {
         if (resolved != null)
         {
            var source = string.IsNullOrEmpty(BattleAssetService.ResolvedBundleName)
               ? "场景引用/编辑器直读"
               : $"AB bundle={BattleAssetService.ResolvedBundleName}";
            return $"已解析（来源 {source}，角色 {resolved.Roles.Length} / 武器 {resolved.Weapons.Length}）";
         }

         var available = BattleAssetService.ResolveAvailableBundle();
         if (available != null)
         {
            return $"未解析到：清单里有 bundle「{available}」但加载失败（见上方 [BattleAssets] 日志）";
         }

         return $"未解析到：候选 AB（{string.Join("、", BattleAssetService.RoleDataBundleCandidates)}）" +
                $"都不含资产 {BattleAssetService.CatalogAssetName}";
      }

      /// <summary>会话成员 id 摘要（诊断用）。</summary>
      private static string DescribeSessionIds()
      {
         var members = BattleTeamSession.Members;
         if (members == null || members.Count == 0)
         {
            return "（空）";
         }

         var text = string.Empty;
         for (var i = 0; i < members.Count; i++)
         {
            if (i > 0)
            {
               text += "、";
            }

            text += $"{members[i].RoleItemId}+{members[i].WeaponItemId}";
         }

         return $"{members.Count} 人[{text}]";
      }

      /// <summary>
      /// 出战编目解析（P0 起顺序）：① 常驻服务从 AB（<see cref="BattleAssetService.RoleDataBundleName"/>）解析；
      /// ② 场景挂引用兜底；③ 编辑器直读 Assets/Data 路径（仅编辑器直连联调）。
      /// 真机不再依赖"场景/prefab 里恰好有引用"——过去正是靠编队面板 prefab 的意外引用把角色资源带进构建的。
      /// </summary>
      private static BattleTeamCatalog ResolveTeamCatalog(BattleTeamCatalog sceneFallbackCatalog)
      {
         var fromAb = BattleAssetService.ResolveCatalog();
         if (fromAb != null)
         {
            return fromAb;
         }

         if (sceneFallbackCatalog != null)
         {
            return sceneFallbackCatalog;
         }

#if UNITY_EDITOR
         return UnityEditor.AssetDatabase.LoadAssetAtPath<BattleTeamCatalog>(EditorTeamCatalogPath);
#else
         return null;
#endif
      }

      /// <summary>用目录角色×武器（1:1，取前 MaxTeamSize 对）构造编辑器直连默认编队。</summary>
      private static BattleRoleRuntimeData[] BuildDefaultFromCatalog(BattleTeamCatalog teamCatalog)
      {
         if (teamCatalog == null || teamCatalog.Roles == null || teamCatalog.Weapons == null)
         {
            return System.Array.Empty<BattleRoleRuntimeData>();
         }

         var results = new System.Collections.Generic.List<BattleRoleRuntimeData>(BattleTeamSession.MaxTeamSize);
         var count = Mathf.Min(BattleTeamSession.MaxTeamSize,
            Mathf.Min(teamCatalog.Roles.Length, teamCatalog.Weapons.Length));
         for (var i = 0; i < count; i++)
         {
            var role = teamCatalog.Roles[i];
            var weapon = teamCatalog.Weapons[i];
            if (role == null || weapon == null) continue;
            results.Add(new BattleRoleRuntimeData(role, weapon));
         }

         return results.ToArray();
      }
   }
}
