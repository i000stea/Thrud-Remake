using QFramework;
using TeaGame.App;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T3.1C battle 场景固定引导（对象名约定：battle_runtime）。
   ///
   /// **架构定位（2026-09-19「宿主瘦身」后的定稿）：战斗场景唯一的「场景生命周期 + 装配调用 + 相位表」节点**
   /// （21 文档 §2.2 的正确命名：本类的 Update/FixedUpdate/LateUpdate 是这套架构的 SystemGroup / PlayerLoop 集成点）：
   /// 1. <see cref="Awake"/>：确保战斗架构创建（幂等）、把 rig 发布进 Model、解析本场关卡并把数据交给场景固件（Director）；
   /// 2. <see cref="Start"/>：调一次装配器 <see cref="BattleSceneAssembler.Build"/>（组合根在装配器里），随后按条件打开战斗 UI；
   /// 3. Update/FixedUpdate/LateUpdate：**相位表**——每帧按固定顺序驱动各 System 与各 Actor（顺序即逻辑，见各方法注释）；
   /// 4. <see cref="OnDestroy"/>：场景级释放（Actor 反注册、Model 清 rig、关面板、摘 UIKit 表）。
   ///
   /// **宿主判据**（21 文档纪律 1）：「这段代码是否只在场景装配/卸载时才需要存在？」是 → 留；否 → 搬走。
   /// 因此**数据解析**在 <see cref="BattleSetupSystem"/>、**装配**在 <see cref="BattleSceneAssembler"/>、
   /// **每帧中间量**在各 Actor 内，本类不残留其中任何一项。
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
      [Tooltip("场景固定 rig（Battle_PlayerControl.prefab 实例，提供 Actor 装配引用；HUD 在独立面板）。")]
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

      /// <summary>本场战斗的装配器（组合根；只装配一次，之后只读它的 Actor）。</summary>
      private BattleSceneAssembler assembler;
      /// <summary>本场关卡数据（Awake 解析、Start 交给装配器做出生点传送）。</summary>
      private BattleLevelData level;
      /// <summary> 准备就绪 </summary>
      private bool battleReady;

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

         // 关卡解析（会话缓存优先 / D12 直连默认）由 BattleSetupSystem 负责（2026-09-19 第 2 步搬出）。
         // 解析失败（返回 false）时**必须丢弃 out 值**：False 分支里 selected 可能仍是"属于别的美术场景"的会话关卡
         // （catalog 为空、会话关卡与本场景不符时即如此），照用会把错的关卡注入 Director。
         // 置空后 battle 场景保底运行（无波次数据），错误日志已由 System 给出——与搬运前 `return` 后不注入的行为一致。
         if (!Architecture.GetSystem<BattleSetupSystem>().TryResolveLevel(gameObject.scene.name, catalog, out level))
         {
            level = null;
         }

         ApplyLevelToScene(level);
      }

      /// <summary>
      /// 把已解析的关卡数据应用到场景固件（**宿主职责：把数据交给 View**）。
      /// 数据解析本身在 <see cref="BattleSetupSystem"/>（2026-09-19 第 2 步搬出）；
      /// 本方法只做场景级注入 + 装配日志。出生点传送随装配在 Start 由装配器执行。
      /// </summary>
      /// <param name="selected">已解析出的本场关卡数据；为空表示未解析到（保底运行）。</param>
      private void ApplyLevelToScene(BattleLevelData selected)
      {
         if (selected == null) return; // 未解析到关卡：既有路径不打 Director 警告、不做注入（与搬运前一致）

         // 敌人系统等到真正更新时再阅读
         if (stageDirector != null)
         {
            stageDirector.Setup(selected, catalog != null ? catalog.EnemyCatalog : null);
         }
         else
         {
            Debug.LogWarning("[battle_runtime] 未配置 BattleStageDirector（场景固件缺失：敌人/波次不会生成）。" +
                             "请用编辑器菜单 Thrud/Battle/T3.1C/1. 检查并放置战斗固定固件。");
         }

         var siteCount = selected.Sites == null ? 0 : selected.Sites.Length;
         Debug.Log($"[battle_runtime] 关卡 {selected.LevelId}「{selected.DisplayName}」装配完成：" +
                   $"出生 {selected.PlayerSpawnPosition}（朝向 {selected.PlayerSpawnEuler}），站点 {siteCount} 个，限时 {selected.TimeLimitSeconds}s。");
      }

      #endregion

      #region Start：整场战斗装配

      // ==================== Start：整场战斗装配 ====================
      private void Start()
      {
         // 1. 整场战斗装配——组合根在 BattleSceneAssembler（2026-09-19 第 4 步）。
         //    顺序依赖（必须在所有对象 Awake 之后）由此处的"在 Start 里调用"表达，不在装配器内用协程掩盖。
         assembler = new BattleSceneAssembler(Rig, StateModel,
            Architecture.GetSystem<BattleSetupSystem>(), teamCatalog, applyPlayerSpawn);
         battleReady = assembler.Build(level);

         // 2. 触摸层（跨场景进入须等 UIRoot 归属稳定，见 OpenBattleUiWhenSceneReady）
         if (battleReady) StartCoroutine(OpenBattleUiWhenSceneReady());
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
         ApplyMovementForce(Architecture.GetSystem<BattleMovementSystem>().TickUpdate(assembler.MovementActor.Forward));

         // 相机tick
         Architecture.GetSystem<BattleCameraSystem>().Tick();

         // 3d表现层tick
         TickActors();

         // 武器 tick（弹药/装填读数由它在**变更点**发布：开火、开始换弹、换弹完成、切人；
         // 唯一连续量"装填进度"仅在装填期间按帧发布 —— 2026-09-19 用户评审后从宿主相位表移除）
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
         var movementActor = assembler.MovementActor;
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
         var movementActor = assembler.MovementActor;
         if (!movementActor.IsAvailable) return;
         if (force.sqrMagnitude <= 0f) return;
         movementActor.ApplyForce(force);
      }

      /// <summary>
      /// 表现层每帧 tick（**顺序是逻辑，不是样板**，与 21 文档 §2.2 反例 3 对应）：
      /// 动画 → FOV → 模型朝向 → 武器朝向。
      /// ⚠️ 必须在 <see cref="BattleWeaponSystem"/>.Tick() <b>之前</b>调用——后者每帧写
      /// `FreeAimRotation`/`ActiveElement` 并在同一方法内 `PressTrigger()` 开火。
      /// 中间量（目标点 / 回正点 / 朝向）已下沉到各 Actor（2026-09-19 第 3 步），本方法只传每帧值。
      /// </summary>
      private void TickActors()
      {
         var state = StateModel;
         var config = Rig.PlayerConfig;
         assembler.RoleActor.Tick(assembler.MovementActor.AnimationVelocity, state.Movement.Sprinting, Time.deltaTime);
         assembler.CameraActor.BlendFov(state.Movement.Sprinting ? config.SprintFov : config.MainFov,
            config.FovLerpSpeed, Time.deltaTime);
         assembler.WeaponActor.Tick();
      }

      #endregion

      #region 销毁周期
      private void OnDestroy()
      {
         battleReady = false;
         // Actor 自持：反注册其事件订阅（纯 C# 类无自动反注册）
         assembler?.RoleActor?.Dispose();
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
