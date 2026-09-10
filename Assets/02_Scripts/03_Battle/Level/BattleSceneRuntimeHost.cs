using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T3.1C battle 场景固定引导（对象名约定：battle_runtime）。
   ///
   /// **架构定位（2026-09-08 调整）：战斗场景唯一的「装配初始化 + 每帧驱动」节点**——
   /// 1. Awake：确保战斗架构创建（幂等）、解析本场关卡、应用出生点、注入波次 Director；
   /// 2. Start：整场战斗装配（角色数据解析与注入、健康/视图/技能/关卡流程初始化、元素子弹注册、
   ///    View 侧 HUD 事件绑定），随后按条件打开触摸操作层；
   /// 3. Update/FixedUpdate/LateUpdate：每帧驱动各 System（含 ESC 暂停请求）。
   ///
   /// Battle_PlayerControl（rig）只作为 **View** 提供表现引用与工具，不再承担初始化/数据解析/驱动
   /// （历史职责已上移至本宿主；详见 Docs/09 关键坑与 Battle_PlayerControl 类注释）。
   ///
   /// 场景对象清单与放置/校验由编辑器工具 Thrud/Battle/T3.1C 负责（检查并放置，幂等）。
   /// </summary>
   [DisallowMultipleComponent]
   public sealed class BattleSceneRuntimeHost : MonoBehaviour
   {
      [Header("装配资产（编辑器工具检查并放置时回填）")]
      [Tooltip("关卡目录：提供 EnemyCatalog 与 D12 直连默认关解析。")]
      [SerializeField] private BattleLevelCatalog catalog;

      [Tooltip("出战编目（方案 B：会话草稿按 itemID 重解析角色/武器 SO 用）。" +
               "场景未挂时编辑器回落 Assets/Data 路径；真机必须挂引用并保证其所在资源不被卸载。")]
      [SerializeField] private BattleTeamCatalog teamCatalog;

      [Tooltip("场景固定 rig（Battle_PlayerControl.prefab 实例，角色控制 + 战斗 HUD，仅作 View）。")]
      [SerializeField] private Battle_PlayerControl playerControl;

      [Tooltip("场景固定敌人管理（波次/目标点/刷怪 Director，数据由本宿主注入）。")]
      [SerializeField] private BattleStageDirector stageDirector;

      [Tooltip("启动后按关卡数据应用玩家出生点/朝向（D9：数据定义出生点，场景不硬编）。")]
      [SerializeField] private bool applyPlayerSpawn = true;

      /// <summary>编辑器直连默认编队目录路径（无出战会话时的兜底数据源）。</summary>
      private const string EditorTeamCatalogPath = "Assets/Data/BattleTeamCatalog.asset";

      private IArchitecture Architecture => BattleArchitecture.Interface;

      private void Awake()
      {
         ResKit.Init();
         // 框架初始化：确保战斗架构创建（幂等）
         _ = BattleArchitecture.Interface;

         var level = ResolveLevel();
         if (level == null)
         {
            Debug.LogWarning(
               $"[battle_runtime] 场景「{gameObject.scene.name}」未解析到关卡数据" +
               "（关卡目录未登记本场景或目录缺失），battle 场景仅保底运行（无波次数据）。");
         }
         else
         {
            ApplyLevelToScene(level);
         }
      }

      private void Start()
      {
         // 1. 整场战斗装配（系统初始化 + 角色数据注入）——顺序依赖：所有对象 Awake 已完成
         SetupBattleSystems();

         // 2. 触摸层（跨场景进入须等 UIRoot 归属稳定，见 OpenTouchUiWhenSceneReady）
         StartCoroutine(OpenTouchUiWhenSceneReady()); 
      }

      private void OnDestroy()
      {
         // T1：离场关闭触摸层（幂等；场景卸载/重开/退出均触发）
         try
         {
            Battle_TouchInputPanel.CloseForBattle();
         }
         catch (System.Exception e)
         {
            Debug.LogWarning($"[battle_runtime] 离场关闭触摸层异常（可忽略）：{e.Message}");
         }
      }

      // ==================== Awake：关卡数据 → 场景固件 ====================

      private void ApplyLevelToScene(BattleLevelData level)
      {
         if (applyPlayerSpawn && playerControl != null)
         {
            playerControl.transform.SetPositionAndRotation(
               level.PlayerSpawnPosition,
               Quaternion.Euler(level.PlayerSpawnEuler));
         }

         if (stageDirector != null)
         {
            stageDirector.Setup(level, catalog != null ? catalog.EnemyCatalog : null);
         }
         else
         {
            Debug.LogWarning("[battle_runtime] 未配置 BattleStageDirector（场景固件缺失：敌人/波次不会生成）。" +
                             "请用编辑器菜单 Thrud/Battle/T3.1C/1. 检查并放置战斗固定固件。");
         }

         var siteCount = level.Sites == null ? 0 : level.Sites.Length;
         Debug.Log($"[battle_runtime] 关卡 {level.LevelId}「{level.DisplayName}」装配完成：" +
                   $"出生 {level.PlayerSpawnPosition}（朝向 {level.PlayerSpawnEuler}），站点 {siteCount} 个，限时 {level.TimeLimitSeconds}s。");
      }

      /// <summary>
      /// 解析本场关卡：BattleLevelSession 选关缓存（须指向本场景）优先；否则 D12 直连默认。
      /// </summary>
      private BattleLevelData ResolveLevel()
      {
         var selected = BattleLevelSession.Selected;
         if (selected != null)
         {
            if (selected.ArtSceneName == gameObject.scene.name)
            {
               return selected;
            }

            Debug.LogWarning($"[battle_runtime] 会话缓存关卡 {selected.LevelId} 属于场景 {selected.ArtSceneName}，" +
                             $"与本场景 {gameObject.scene.name} 不符，回落直连默认（D12）。");
         }

         if (catalog != null)
         {
            var fallback = catalog.FindFirstByArtScene(gameObject.scene.name);
            if (fallback != null)
            {
               return fallback;
            }
         }

         return null;
      }

      // ==================== Start：整场战斗装配 ====================

      private void SetupBattleSystems()
      {
         if (playerControl == null)
         {
            Debug.LogError("[battle_runtime] 未配置 rig（Battle_PlayerControl），无法装配战斗。" +
                           "请用 Thrud/Battle/T3.1C/1. 检查并放置战斗固定固件。");
            return;
         }

         var model = Architecture.GetModel<BattleStateModel>();
         model.Paused = false; // 复位暂停态（架构常驻跨场景）

         // 角色数据解析（会话优先 / Editor 目录默认 / 正式无会话退回）→ 注入角色系统
         var datas = ResolveBattleDatas();

         Architecture.GetSystem<BattleRoleSystem>().Initialize(playerControl, datas);
         Architecture.GetSystem<BattleHealthSystem>().Initialize();
         Architecture.GetSystem<BattleViewSystem>().InitializeView();
         playerControl.SyncRuntimeBridge();

         // T2.3.1：注册元素子弹（开火按角色元素取弹）
         BattleElementBulletLibrary.Register(
            playerControl.BulletIcePrefab,
            playerControl.BulletFirePrefab,
            playerControl.BulletWindPrefab,
            playerControl.BulletElectricPrefab);

         // T2.4：技能系统按本场角色数量重建冷却表、清零能量并广播首帧状态
         Architecture.GetSystem<BattleSkillSystem>().ResetForBattle();

         // T3.1：关卡流程复位进入 Prepare（终局去向由结算弹窗按钮触发）
         Architecture.GetSystem<BattleLevelFlow>().StartLevel();

         // T2.4：技能 HUD 事件绑定（View 侧反向 bind）
         playerControl.BindSkillHudEvents();

         Debug.Log($"[battle_runtime] 战斗系统装配完成：出战 {datas.Length} 人。");
      }

      /// <summary>
      /// 解析出战角色数据（T3.1A 数据管道；2026-09-08 由 rig 上移至 battle_runtime）：
      /// 1. <see cref="BattleTeamSession"/> 有选队 → 经出战编目把草稿的 itemID 重解析为 SO 后实体化；
      ///    编目缺失或成员 id 未命中（编目为空/数据更换/目录缺项）→ 清空会话回落；
      /// 2. 编辑器直连（无会话）→ 用 <see cref="BattleTeamCatalog"/>（BattleTeamCatalog.asset）默认编队兜底；
      /// 3. 正式构建且无会话/编目 → 流程错误：报错退回主场景（不开空队）。
      /// </summary>
      private BattleRoleRuntimeData[] ResolveBattleDatas()
      {
         var resolvedCatalog = ResolveTeamCatalog();

         if (BattleTeamSession.HasSelection)
         {
            if (resolvedCatalog == null)
            {
               Debug.LogWarning(
                  "[battle_runtime] 出战编队会话存在但未解析到出战编目（battle_runtime 未挂 teamCatalog，" +
                  "编辑器会自动回落 Assets/Data 路径，真机必须挂）——已清空会话。");
               BattleTeamSession.Clear();
            }
            else
            {
               var built = BattleTeamRuntimeBuilder.Build(BattleTeamSession.Members, resolvedCatalog);
               if (built.Length > 0)
               {
                  return built;
               }

               Debug.LogWarning(
                  "[battle_runtime] 出战编队会话成员未从编目解析到完整 SO（编目为空/数据更换/目录缺项）" +
                  "——已清空会话回落默认。请重新走选关 → 编队流程。");
               BattleTeamSession.Clear();
            }
         }

#if UNITY_EDITOR
         var fallback = BuildDefaultFromCatalog(resolvedCatalog);
         if (fallback != null && fallback.Length > 0)
         {
            return fallback;
         }

         Debug.LogWarning($"[battle_runtime] 编辑器直连未找到默认编队目录（{EditorTeamCatalogPath}），空队保底运行。");
         return System.Array.Empty<BattleRoleRuntimeData>();
#else
         Debug.LogError(
            "[battle_runtime] 正式环境进入战斗但缺少出战编队会话或出战编目——" +
            $"请从「{BattleTeamSession.TeamSelectSceneName}」进入，并确认 battle_runtime 已挂出战编目。" +
            $"已退回 {BattleTeamSession.ReturnSceneName}。");
         QFramework.UIKit.CloseAllPanel(); // 清面板表防跨场景残留已销毁面板
         BattleSceneGate.TransitionTo(BattleTeamSession.ReturnSceneName);
         return System.Array.Empty<BattleRoleRuntimeData>();
#endif
      }

      /// <summary>出战编目解析：场景挂引用优先；编辑器未挂时回落 Assets/Data 目录资产（直连联调用）。</summary>
      private BattleTeamCatalog ResolveTeamCatalog()
      {
         if (teamCatalog != null)
         {
            return teamCatalog;
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

      // ==================== 每帧驱动（自 Battle_PlayerControl 上移） ====================

      private void Update()
      {
         var state = Architecture.GetModel<BattleStateModel>();
         if (playerControl == null) return;

         Architecture.GetSystem<BattleRoleSystem>().SetControlTakenOver(playerControl.ControlTakeOff);
         Architecture.GetSystem<BattleInputSystem>().Tick();

         // T3.1：ESC = 暂停/恢复请求（终局由结算弹窗接管，不再响应）
         if (Input.GetKeyDown(KeyCode.Escape)
             && state.LevelState != BattleLevelState.Victory
             && state.LevelState != BattleLevelState.Defeat)
         {
            Architecture.SendEvent(new BattlePauseToggleRequestedEvent());
         }

         if (state.Paused)
         {
            playerControl.SyncRuntimeBridge();
            return;
         }

         Architecture.GetSystem<BattleMovementSystem>().TickUpdate();
         Architecture.GetSystem<BattleCameraSystem>().Tick();
         Architecture.GetSystem<BattleViewSystem>().TickUpdate();
         Architecture.GetSystem<BattleWeaponSystem>().Tick();

         // T2.4：技能管道（输入快照已含 E/Q，冷却读秒 + 施放校验在本系统内收口）
         Architecture.GetSystem<BattleSkillSystem>().Tick();

         playerControl.SyncRuntimeBridge();
      }

      private void FixedUpdate()
      {
         if (playerControl == null) return;
         if (Architecture.GetModel<BattleStateModel>().Paused) return;

         Architecture.GetSystem<BattleMovementSystem>().TickFixedUpdate();
         Architecture.GetSystem<BattleAimSystem>().TickFixedUpdate();
         playerControl.SyncRuntimeBridge();
      }

      private void LateUpdate()
      {
         if (playerControl == null) return;
         if (Architecture.GetModel<BattleStateModel>().Paused) return;

         Architecture.GetSystem<BattleAimSystem>().TickLateUpdate();
         playerControl.SyncRuntimeBridge();
      }

      // ==================== 触摸层（T1） ====================

      private System.Collections.IEnumerator OpenTouchUiWhenSceneReady()
      {
         const int maxFrames = 1200; // 上限约 20s，避免异常情况下永久等待
         for (var i = 0; i < maxFrames; i++)
         {
            if (Battle_TouchInputPanel.IsUIRootReadyForScene(gameObject.scene))
            {
               break;
            }

            yield return null;
         }

         Battle_TouchInputPanel.TryOpenForBattle();
      }
   }
}
