using QFramework;
using TeaGame.App;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗场景**装配器（组合根）**——2026-09-19「宿主瘦身」第 4 步：整段自 <see cref="BattleSceneRuntimeHost"/> 搬出。
   ///
   /// **为什么要有这个类**（21 文档 §1.1 诊断）：宿主原先同时承担「生命周期 + 装配 + 调度 + 数据 + UI + 销毁」六件事，
   /// 改任何一件都要读全文件；装配是一次性的组合动作，和"每帧调度"没有共同点，应当分开。
   ///
   /// **边界（21 文档新增纪律 2、3）**：
   /// - 本类**不 tick、不持生命周期、不读 Update**——只在 <see cref="Build"/> 里跑一次；
   /// - **顺序依赖靠宿主在 Start 里调用表达**（所有对象 Awake 已完成），内部不用协程/延时掩盖；
   /// - 它**只被宿主持有**，对外只读暴露 4 个 Actor 供宿主 tick。
   ///
   /// **依赖集合（构造函数一眼看全）**：rig（场景固件）+ Model（只读绑定对象）+ 入场数据解析系统
   /// + 场景上的出战编目兜底 + 出生点开关。不缓存到 System 里——避免架构级对象持有场景级引用。
   ///
   /// **失败路径**：正式构建下编队无法装配时，由本类收口入场失败流程（清面板表 + 回到入口场景）；
   /// 解析判断本身在 <see cref="BattleSetupSystem"/>（System 只返回数据与标志，不驱动流程）。
   ///
   /// 设计与逐步验收见 `Docs/03_专项设计与重构/21_战斗场景装配与宿主瘦身_设计与落地指导.md` §4 步骤 4。
   /// </summary>
   public sealed class BattleSceneAssembler
   {
      private readonly Battle_PlayerControl rig;
      private readonly BattleStateModel state;
      private readonly BattleSetupSystem setup;
      private readonly BattleTeamCatalog teamCatalogFallback;
      private readonly bool applyPlayerSpawn;

      /// <summary>移动表现（宿主每帧用它解析方向、施力、传送）。</summary>
      public BattleMovementActor MovementActor { get; private set; }
      /// <summary>角色表现（挂点/显隐/动画/模型朝向）。</summary>
      public BattleRoleActor RoleActor { get; private set; }
      /// <summary>武器表现（瞄准朝向）。</summary>
      public BattleWeaponActor WeaponActor { get; private set; }
      /// <summary>相机表现（FOV 混合）。</summary>
      public BattleCameraActor CameraActor { get; private set; }

      public BattleSceneAssembler(Battle_PlayerControl rig, BattleStateModel state, BattleSetupSystem setup,
         BattleTeamCatalog teamCatalogFallback, bool applyPlayerSpawn)
      {
         this.rig = rig;
         this.state = state;
         this.setup = setup;
         this.teamCatalogFallback = teamCatalogFallback;
         this.applyPlayerSpawn = applyPlayerSpawn;
      }

      /// <summary>
      /// 整场战斗装配（宿主 Start 调用一次）。
      /// 顺序与搬运前逐条一致：复位暂停态 → 移动 Actor/系统 → 出战数据 → 角色系统 → 三个表现 Actor →
      /// 挂点与自订阅 → 出生点传送 → 重置瞄准 → 弹药广播 → 桥接 → 元素子弹 → 技能复位 → 关卡流程。
      /// </summary>
      /// <param name="level">宿主在 Awake 已解析并注入 Director 的本场关卡（未解析到时为 null）。</param>
      /// <returns>是否完成装配（rig 缺失时为 false，宿主据此不进入每帧驱动）。</returns>
      public bool Build(BattleLevelData level)
      {
         // 玩家控制器存续状态检测
         if (!rig)
         {
            Debug.LogError("[battle_runtime] 未配置 rig（Battle_PlayerControl），无法装配战斗。");
            return false;
         }

         state.Paused = false; // 复位暂停态（架构常驻跨场景）

         // 移动表现：Actor 由本装配器持有；System 只接配置（2026-09-19：System 不再持有 View）
         MovementActor = new BattleMovementActor(rig.transform, rig.SelfRigidbody,
            rig.ControlXAxis, rig.Control_Camera);
         Architecture.GetSystem<BattleMovementSystem>().Initialize(rig.PlayerConfig);

         // 角色数据解析（会话优先 / Editor 目录默认 / 正式无会话退回）→ 注入角色系统。
         // 解析在 BattleSetupSystem；本类只消费"数据 + 失败标志"。
         var resolution = setup.ResolveBattleDatas(teamCatalogFallback);
         var datas = resolution.Datas;
         if (resolution.RetreatToEntryScene)
         {
            // 入场失败流程：清面板表防跨场景残留已销毁面板（CloseAllPanel 对已销毁条目会抛异常），再回入口场景。
            // 与搬运前一致：此处不中断装配（空队保底运行），离场由场景切换收口。
            UiPanelFlow.CloseAllSafely();
            BattleSceneGate.TransitionTo(BattleTeamSession.ReturnSceneName);
         }

         // 角色系统（含成员级血量复位——2026-09-19 BattleHealthSystem 已并入本系统）
         Architecture.GetSystem<BattleRoleSystem>().Initialize(rig, datas);

         // 三个表现 Actor（依赖集合见各构造函数；每帧中间量在 Actor 内派生——第 3 步）
         RoleActor = new BattleRoleActor(rig.Roles, rig.GetWeaponParent(), rig.ViewRotateAxis,
            rig.Control_Camera, rig.ControlXAxis, rig.PlayerConfig);
         WeaponActor = new BattleWeaponActor(rig.WepLookatTarget, rig.Control_Camera, rig.ControlXAxis,
            rig.transform, state, rig.PlayerConfig);
         CameraActor = new BattleCameraActor(rig.Control_Camera, rig.PlayerConfig.MainFov);

         // Actor 自持（2026-09-19 第 1 步）：装配挂点 + 自己订阅切人/阵亡/翻滚事件 + 播出生动画。
         // 原为 `GetSystem<BattleRoleSystem>().AttachActor(roleActor)`——System 不再持有 View。
         RoleActor.Initialize(datas);

         // D9：出生点由关卡数据定义，场景不硬编。传送是 View 操作，直接交给 Actor
         //（原为 MovementSystem.Teleport 透传）。⚠️ 相对搬运前唯一时机变化：由宿主 Awake 推到本处（Start 阶段，
         // 仍在一切 Update 之前）——Awake→Start 之间没有任何读者依赖 rig 位置（Director 的目标点生成只用关卡数据）。
         if (applyPlayerSpawn && level != null)
         {
            MovementActor.Teleport(level.PlayerSpawnPosition, Quaternion.Euler(level.PlayerSpawnEuler));
         }

         // 重置瞄准
         Architecture.GetSystem<BattleAimSystem>().ResetForBattle();

         state.PublishAmmo(state.CurrentBattleData);

         rig.SyncRuntimeBridge();

         // 弹药后面要更新
         // T2.3.1：注册元素子弹（开火按角色元素取弹）
         BattleElementBulletLibrary.Register(
            rig.BulletIcePrefab,
            rig.BulletFirePrefab,
            rig.BulletWindPrefab,
            rig.BulletElectricPrefab);

         // T2.4：技能系统按本场角色数量重建冷却表、清零能量并广播首帧状态
         Architecture.GetSystem<BattleSkillSystem>().ResetForBattle();

         // T3.1：关卡流程复位进入 Prepare（终局去向由结算弹窗按钮触发）
         Architecture.GetSystem<BattleLevelFlow>().StartLevel();

         // [2026-09-18] 技能 HUD 的 View 侧绑定已迁至 Battle_MainPanel（BindableProperty 反向绑定），
         // 面板在宿主等 UIRoot 归属稳定后打开——见 BattleSceneRuntimeHost.OpenBattleUiWhenSceneReady。

         Debug.Log($"[battle_runtime] 战斗系统装配完成：出战 {datas.Length} 人。");
         return true;
      }

      private static IArchitecture Architecture => BattleArchitecture.Interface;
   }
}
