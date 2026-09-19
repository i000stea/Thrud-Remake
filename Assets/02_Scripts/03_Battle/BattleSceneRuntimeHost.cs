using QFramework;
using TeaGame.App;
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
      #region 参数变量

      [Header("装配资产（编辑器工具检查并放置时回填）")]
      [Tooltip("关卡目录：提供 EnemyCatalog 与 D12 直连默认关解析。")]
      [SerializeField] private BattleLevelCatalog catalog;

      [Tooltip("出战编目（可选兜底）。P0 起角色/武器资源走显式 AB：BattleAssetService 从 bundle=" +
               "battle_characters 解析并常驻；本字段仅在 AB 不可解析时兜底（编辑器直连联调可留空）。")]
      [SerializeField] private BattleTeamCatalog teamCatalog;

      // 这个地方有点问题，他为什么是手动拖拽的？我希望应该是通过某种方式查找当前场景中存在的该脚本，没有的话去呼叫热更新模块把相应的预制件创建然后进行获取
      [Tooltip("场景固定 rig（Battle_Rig.prefab 实例，提供 Actor 装配引用；HUD 在独立面板）。")]
      [SerializeField] private Battle_PlayerControl playerControl;

      [Tooltip("场景固定敌人管理（波次/目标点/刷怪 Director，数据由本宿主注入）。")]
      [SerializeField] private BattleStageDirector stageDirector;

      [Tooltip("启动后按关卡数据应用玩家出生点/朝向（D9：数据定义出生点，场景不硬编）。")]
      [SerializeField] private bool applyPlayerSpawn = true;

      /// <summary>
      /// 战斗中的总数据中心
      /// </summary>
      BattleStateModel StateModel
      {
         get
         {
            _sModel ??= Architecture.GetModel<BattleStateModel>();
            return _sModel;
         }
      }
      BattleStateModel _sModel;
      
      /// <summary> 移动表现 </summary>
      private BattleMovementActor movementActor;
      /// <summary> 角色表现 </summary>
      private BattleRoleActor roleActor;
      /// <summary> 武器表现 </summary>
      private BattleWeaponActor weaponActor;
      /// <summary> 相机表现 </summary>
      private BattleCameraActor cameraActor;
      /// <summary> 准备就绪 </summary>
      private bool battleReady;

      /// <summary>编辑器直连默认编队目录路径（无出战会话时的兜底数据源）。</summary>
      private const string EditorTeamCatalogPath = "Assets/Data/BattleTeamCatalog.asset";

      private IArchitecture Architecture => BattleArchitecture.Interface;

      /// <summary>
      /// 场景 rig 的**只读桥接属性**（2026-09-19 第 1 步）。
      ///
      /// 运行期唯一持有者是 <see cref="BattleStateModel.Controller"/>——序列化字段 `playerControl`
      /// 只作为**场景绑定输入**（装配前提供引用），<see cref="Awake"/> 首段把它发布进 Model。
      /// 此后本文件一律走本属性，避免"装配期用字段、运行期用 Model"的双路径。
      /// </summary>
      private Battle_PlayerControl Rig => StateModel.Controller;

      #endregion

      #region Awake：关卡数据 → 场景固件
      // ==================== Awake：关卡数据 → 场景固件 ====================
      private void Awake()
      {
         // P0：ResKit 初始化收口（已初始化则只校验 AB 清单；正常路径由 AppBootstrapper 异步初始化）
         ResKitBootstrap.EnsureInitialized();
         // 框架初始化：确保战斗架构创建（幂等）
         _ = BattleArchitecture.Interface;

         // 2026-09-19 第 1 步：rig 在 Awake 就发布进 Model——Model 成为唯一运行期持有者
         //（此前由 BattleRoleSystem.Initialize 在 Start 写入，等于同一引用的第二份拷贝）。
         StateModel.Controller = playerControl;

         if (Rig)
         {
            movementActor = new BattleMovementActor(Rig.transform, Rig.SelfRigidbody,
               Rig.ControlXAxis, Rig.Control_Camera);
            // Actor 由宿主持有；System 只接配置（2026-09-19：System 不再持有 View）
            Architecture.GetSystem<BattleMovementSystem>().Initialize(Rig.PlayerConfig);
         }

         if (!TryResolveLevel(out var level)) { return; }

         ApplyLevelToScene(level);
      }

      /// <summary>
      /// 解析本场关卡：BattleLevelSession 选关缓存（须指向本场景）优先；否则 D12 直连默认。
      /// </summary>
      private bool TryResolveLevel(out BattleLevelData selected)
      {
         selected = BattleLevelSession.Selected;

         if (selected != null)
         {
            if (selected.ArtSceneName == gameObject.scene.name)
            {
               return true;
            }

            // 这个地方的美术判断不应该放在这里？还是说这里只是做一个保底检测
            Debug.LogWarning($"[battle_runtime] 会话缓存关卡 {selected.LevelId} 属于场景 {selected.ArtSceneName}，" +
                             $"与本场景 {gameObject.scene.name} 不符，回落直连默认（D12）。");
         }

         // 关卡为主，美术场景错误不应该回滚到相同美术场景的首个关卡？还是说这个仅限于编辑器配置
         if (catalog != null)
         {
            selected = catalog.FindFirstByArtScene(gameObject.scene.name);
            if (selected != null)
            {
               return true;
            }
         }

         Debug.LogError(
            $"[battle_runtime] 场景「{gameObject.scene.name}」未解析到关卡数据" +
            "（关卡目录未登记本场景或目录缺失），battle 场景仅保底运行（无波次数据）。");
         return false;
      }

      /// <summary>
      /// 应用场景
      /// </summary>
      /// <param name="level"></param>
      private void ApplyLevelToScene(BattleLevelData level)
      {
         if (applyPlayerSpawn && Rig != null)
         {
            // 传送是 View 操作 → 直接交给 Actor（原为 MovementSystem.Teleport 透传）
            movementActor.Teleport(
               level.PlayerSpawnPosition,
               Quaternion.Euler(level.PlayerSpawnEuler));
         }

         // 敌人系统等到真正更新时再阅读
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

      #endregion

      #region Start：整场战斗装配

      // ==================== Start：整场战斗装配 ====================
      private void Start()
      {
         // 1. 整场战斗装配（系统初始化 + 角色数据注入）——顺序依赖：所有对象 Awake 已完成
         SetupBattleSystems();

         // 2. 触摸层（跨场景进入须等 UIRoot 归属稳定，见 OpenTouchUiWhenSceneReady）
         if (battleReady) StartCoroutine(OpenBattleUiWhenSceneReady());
      }
      private void SetupBattleSystems()
      {
         // 玩家控制器存续状态检测
         if (Rig == null)
         {
            Debug.LogError("[battle_runtime] 未配置 rig（Battle_PlayerControl），无法装配战斗。");
            return;
         }

         //var model = Architecture.GetModel<BattleStateModel>();
         StateModel.Paused = false; // 复位暂停态（架构常驻跨场景）

         // 角色数据解析（会话优先 / Editor 目录默认 / 正式无会话退回）→ 注入角色系统
         var datas = ResolveBattleDatas();

         // 各系统初始化

         // 角色系统（含成员级血量复位——2026-09-19 BattleHealthSystem 已并入本系统）
         Architecture.GetSystem<BattleRoleSystem>().Initialize(Rig, datas);
         
         // 角色显示
         roleActor = new BattleRoleActor(Rig.Roles, Rig.GetWeaponParent(), Rig.ViewRotateAxis);
         // 武器显示
         weaponActor = new BattleWeaponActor(Rig.WepLookatTarget);
         // 相机显示
         cameraActor = new BattleCameraActor(Rig.Control_Camera, Rig.PlayerConfig.MainFov);
         
         // Actor 自持（2026-09-19 第 1 步）：装配挂点 + 自己订阅切人/阵亡/翻滚事件 + 播出生动画。
         // 原为 `GetSystem<BattleRoleSystem>().AttachActor(roleActor)`——System 不再持有 View。
         roleActor.Initialize(datas);

         // 重置瞄准
         Architecture.GetSystem<BattleAimSystem>().ResetForBattle();

         StateModel.PublishAmmo(StateModel.CurrentBattleData);

         Rig.SyncRuntimeBridge();

         // 弹药后面要更新
         // T2.3.1：注册元素子弹（开火按角色元素取弹）
         BattleElementBulletLibrary.Register(
            Rig.BulletIcePrefab,
            Rig.BulletFirePrefab,
            Rig.BulletWindPrefab,
            Rig.BulletElectricPrefab);

         // T2.4：技能系统按本场角色数量重建冷却表、清零能量并广播首帧状态
         Architecture.GetSystem<BattleSkillSystem>().ResetForBattle();

         // T3.1：关卡流程复位进入 Prepare（终局去向由结算弹窗按钮触发）
         Architecture.GetSystem<BattleLevelFlow>().StartLevel();

         // [2026-09-18] 技能 HUD 的 View 侧绑定已迁至 Battle_MainPanel（BindableProperty 反向绑定），
         // 面板在等 UIRoot 归属稳定后由 OpenBattleUiWhenSceneReady 打开——见本类底部。

         battleReady = true;
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
                  return built;
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
            return fallback;
         }

         Debug.LogWarning($"[battle_runtime] 编辑器直连未找到默认编队目录（{EditorTeamCatalogPath}），空队保底运行。" +
                          $"原因：{failure}");
         return System.Array.Empty<BattleRoleRuntimeData>();
#else
         Debug.LogError(
            $"[battle_runtime] 正式环境无法装配出战编队：{failure}。\n" +
            $"  编目：{DescribeCatalogState(resolvedCatalog)}\n" +
            $"  入口：{BattleTeamSession.FormationEntrySceneName} 的「选关 → 编队 → 开始出战」\n" +
            $"  资源：Assets/Data/BattleTeamCatalog.asset 需标记到 AB「{BattleAssetService.RoleDataBundleName}」" +
            "并已构建（真机不会走编辑器的 Assets/Data 直读回落）。\n" +
            $"  已退回 {BattleTeamSession.ReturnSceneName}。");
         UiPanelFlow.CloseAllSafely(); // 清面板表防跨场景残留已销毁面板（CloseAllPanel 对已销毁条目会抛异常）
         BattleSceneGate.TransitionTo(BattleTeamSession.ReturnSceneName);
         return System.Array.Empty<BattleRoleRuntimeData>();
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
      private BattleTeamCatalog ResolveTeamCatalog()
      {
         var fromAb = BattleAssetService.ResolveCatalog();
         if (fromAb != null)
         {
            return fromAb;
         }

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

      #endregion

      #region 每帧驱动（自 Battle_PlayerControl 上移）

      // ==================== 每帧驱动（自 Battle_PlayerControl 上移） ====================

      private void Update()
      {
         var state = StateModel;

         // 状态检测
         if (!battleReady || Rig == null) return;

         // 接管状态？
         Architecture.GetSystem<BattleRoleSystem>().SetControlTakenOver(Rig.ControlTakeOff);
         
         // 监控输入
         Architecture.GetSystem<BattleInputSystem>().Tick();

         // T3.1：ESC = 暂停/恢复请求（终局由结算弹窗接管，不再响应）
         if (Input.GetKeyDown(KeyCode.Escape)
             && state.LevelState != BattleLevelState.Victory
             && state.LevelState != BattleLevelState.Defeat)
         {
            Architecture.SendEvent(new BattleEvent_PauseToggleRequested());
         }

         if (state.Paused)
         {
            Rig.SyncRuntimeBridge();
            return;
         }

         // 移动：System 只算，Actor 只执行（2026-09-19 第 1 步同批）
         ApplyMovementForce(Architecture.GetSystem<BattleMovementSystem>().TickUpdate(movementActor.Forward));

         // 相机tick
         Architecture.GetSystem<BattleCameraSystem>().Tick();
         
         TickActors();
         
         StateModel.PublishAmmo(StateModel.CurrentBattleData);
         
         // 武器 tick
         Architecture.GetSystem<BattleWeaponSystem>().Tick();

         // T2.4：技能管道（输入快照已含 E/Q，冷却读秒 + 施放校验在本系统内收口）
         Architecture.GetSystem<BattleSkillSystem>().Tick();

         Rig.SyncRuntimeBridge();
      }

      private void FixedUpdate()
      {
         if (!battleReady || Rig == null) return;
         if (StateModel.Paused) return;

         // 世界方向由 Actor 的基向量解析（Actor 不可用时给零向量），力再交回 Actor 施加
         var moveDirection = movementActor.IsAvailable
            ? movementActor.ResolveDirection(StateModel.Input.MoveHorizontal, StateModel.Input.MoveDepth,
               StateModel.Input.MoveVertical, Rig.PlayerConfig.MoveForwardFollowView,
               Rig.PlayerConfig.DepthFromWorld)
            : Vector3.zero;
         ApplyMovementForce(Architecture.GetSystem<BattleMovementSystem>().TickFixedUpdate(moveDirection));
         Architecture.GetSystem<BattleAimSystem>().TickFixedUpdate();
         Rig.SyncRuntimeBridge();
      }

      private void LateUpdate()
      {
         if (!battleReady || Rig == null) return;
         if (StateModel.Paused) return;

         Architecture.GetSystem<BattleAimSystem>().TickLateUpdate();
         Battle_MainPanel.TickForBattle(Time.deltaTime);
         Rig.SyncRuntimeBridge();
      }

      /// <summary>
      /// 把移动系统算出的力交给 Actor 施加（2026-09-19：System 只算、Actor 只执行）。
      /// "Actor 不可用则跳过"原先写在 <see cref="BattleMovementSystem"/> 内部——那等于把 View 状态当逻辑门控，
      /// 现移到持有 Actor 的宿主侧。
      /// </summary>
      private void ApplyMovementForce(Vector3 force)
      {
         
         if (!movementActor.IsAvailable) return;
         if (force.sqrMagnitude <= 0f) return;
         movementActor.ApplyForce(force);
      }

      // 保留原 Update 顺序：动画 → FOV → 模型 → 武器 → 武器开火。
      private void TickActors()
      {
         // 数据
         var state = StateModel;
         // 配置
         var config = Rig.PlayerConfig;
         roleActor.SetMovement(movementActor.AnimationVelocity, state.Movement.Sprinting);
         cameraActor.BlendFov(state.Movement.Sprinting ? config.SprintFov : config.MainFov,
            config.FovLerpSpeed, Time.deltaTime);
         var camera = Rig.Control_Camera;
         var axis = Rig.ControlXAxis;
         if (camera || axis)
         {
            var rotation = camera ? BattleRoleActor.ClampLookRotation(camera.transform.forward) : axis.rotation;
            roleActor.FollowView(rotation, config.RotationSpeed, Time.deltaTime);
         }
         var target = state.LockEnemy.IsAlive() ? state.LockEnemy.LockPoint : null;
         var freePoint = camera ? camera.transform.position + camera.transform.forward * 10f
            : axis ? axis.position + axis.forward * 10f : Rig.transform.position;
         weaponActor.Aim(state.CurrentBattleData, target, freePoint, Rig.GetCurrentLockSpeed(state.LockSpeedMultiplier));
      }

      #endregion

      #region 销毁周期
      private void OnDestroy()
      {
         battleReady = false;
         // Actor 自持：反注册其事件订阅（纯 C# 类无自动反注册）
         roleActor?.Dispose();
         // 2026-09-19 第 1 步：Controller 由本宿主在 Awake 发布（Model 是唯一运行期持有者），
         // 判据随之从"是否等于字段"退化为"是否已被本宿主发布过"。
         if (StateModel.Controller) StateModel.Controller = null;
         // [2026-09-18] 离场关闭战斗 UI 面板（主 HUD + 触屏层；均幂等）
         try
         {
            Battle_MainPanel.CloseForBattle();
            Battle_TouchInputPanel.CloseForBattle();
         }
         catch (System.Exception e)
         {
            Debug.LogWarning($"[battle_runtime] 离场关闭战斗 UI 面板异常（可忽略）：{e.Message}");
         }

         // P1：面板 GameObject 随本场景销毁时 IPanel.Close 不会被调用 → UIKit 表会残留已销毁条目，
         // 若不摘除，下次进战斗 OpenPanel 会复用已销毁实例并抛 MissingReferenceException（面板打不开）。
         UiPanelFlow.SanitizeRegistry();
      }
      #endregion

      #region 战斗 UI 面板 
      // ==================== 战斗 UI 面板（主 HUD / 触屏层，T1 + 2026-09-18） ====================

      /// <summary>
      /// 等 UIRoot 归属稳定后打开战斗 UI 面板：主 HUD（Battle_MainPanel）+ 触屏层（Battle_TouchInputPanel）。
      /// 两者共用同一个等待循环——跨场景进入时若 UIRoot 仍属于即将卸载的旧场景，
      /// 挂上去的面板会随旧场景销毁（见 Battle_TouchInputPanel.IsUIRootReadyForScene 的注释）。
      /// </summary>
      private System.Collections.IEnumerator OpenBattleUiWhenSceneReady()
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

         // 主 HUD 常开；触屏层按平台/调试开关自判是否启用
         Battle_MainPanel.TryOpenForBattle();
         Battle_TouchInputPanel.TryOpenForBattle();
      }
      #endregion
   }
}
