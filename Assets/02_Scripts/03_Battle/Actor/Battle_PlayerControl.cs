using System;
using System.Collections.Generic;
using QFramework;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗玩家控制器（rig，原 PlayerControl，T3.1 改名）。
   ///
   /// 场景 rig 的序列化入口，供 Host 装配 Actor；HUD 已归 Battle_MainPanel。
   /// - **不再承担**：进入战斗的数据解析、系统初始化、每帧驱动（这些已上移
   ///   到 battle_runtime（<see cref="BattleSceneRuntimeHost"/>，场景初始化 + 驱动节点）。
   ///   Input/Aim/Camera 暂保留必要的配置与相机读取；移动只通过 MovementActor 执行。
   /// - **对外身份**：实现 <see cref="IBattlePlayerTarget"/> 并**自己注册到
   ///   <see cref="BattlePlayerRegistry"/>**（`Awake` 注册 / `OnDestroy` 注销）——
   ///   敌人一侧的"找玩家"经注册表读，旧的 `BattleRuntimeBridge` + 每帧 `SyncRuntimeBridge()` 已退役（2026-09-19）。
   /// </summary>
   public partial class Battle_PlayerControl : ViewController, IController, IUnRegisterList, IBattlePlayerTarget
   {
      #region 运行时

      [Header("运行时")]
      [SerializeField][Range(0.01f, 4f)] private float scale = 1f;
      [SerializeField] private bool controlTakeOff;
      [SerializeField] private int initialRoleIndex;

      [Header("场景引用（Designer 绑定）")]
      [SerializeField] private BattleEnvironmentEffectController environmentEffectController;

      [Header("配置（ScriptableObject）")]
      [SerializeField] private BattlePlayerConfig playerConfig;

      [Header("元素子弹 prefab（T2.3.1：按当前角色元素打出对应子弹，由编辑器向导统一装配）")]
      [SerializeField] private GameObject bulletIcePrefab;
      [SerializeField] private GameObject bulletFirePrefab;
      [SerializeField] private GameObject bulletWindPrefab;
      [SerializeField] private GameObject bulletElectricPrefab;

      #endregion
      
      #region 参数变量
      /// <summary> 当前控制器注册的事件反注册列表。 </summary>
      public List<IUnRegister> UnregisterList { get; } = new();

      /// <summary>
      /// 当前是否允许玩家控制。
      /// 外部系统可以通过该属性临时接管控制权。
      /// </summary>
      public bool ControlTakeOff
      {
         get => controlTakeOff;
         set => controlTakeOff = value;
      }

      /// <summary> 初始选中的角色索引。</summary>
      public int InitialRoleIndex => initialRoleIndex;

      /// <summary>战斗环境特效控制器。</summary>
      public BattleEnvironmentEffectController EnvironmentEffectController => environmentEffectController;

      /// <summary>
      /// 运行时默认玩家配置兜底。
      /// 当场景/预制体未配置 <see cref="playerConfig"/> 时创建一次，避免空引用导致战斗初始化崩溃。
      /// </summary>
      private static BattlePlayerConfig sFallbackPlayerConfig;

      /// <summary>战斗玩家配置。</summary>
      public BattlePlayerConfig PlayerConfig
      {
         get
         {
            if (playerConfig) return playerConfig;

            if (sFallbackPlayerConfig == null)
            {
               sFallbackPlayerConfig = ScriptableObject.CreateInstance<BattlePlayerConfig>();
               sFallbackPlayerConfig.name = "DefaultBattlePlayerConfig(RuntimeFallback)";
               Debug.LogWarning($"[{name}] 未配置 BattlePlayerConfig，已创建运行时默认配置兜底。请在场景/预制体上指定配置资产。");
            }

            return sFallbackPlayerConfig;
         }
      }

      /// <summary>
      /// 暴露给 battle_runtime 的 View 表现引用：元素子弹 prefab（开火按当前角色元素取弹）。
      /// </summary>
      public GameObject BulletIcePrefab => bulletIcePrefab;
      public GameObject BulletFirePrefab => bulletFirePrefab;
      public GameObject BulletWindPrefab => bulletWindPrefab;
      public GameObject BulletElectricPrefab => bulletElectricPrefab;


      /// <summary>
      /// 当前战斗架构实例。
      /// 这里不直接依赖 `GetSystem/GetModel` 扩展方法，
      /// 这样可以规避部分 IDE 对扩展方法识别不稳定导致的"方法丢失"提示。
      /// </summary>
      private IArchitecture Architecture => BattleArchitecture.Interface;

      #endregion

      /// <summary>
      /// 初始化（View 只做最轻的时间缩放设置；战斗装配/驱动由 battle_runtime 负责）。
      /// </summary>
      private void Awake()
      {
         Time.timeScale = scale;

         // 2026-09-19「宿主瘦身」步骤 5（事件化）：控制接管改为**订阅请求事件**——
         // System 不再隔着 Model 写本组件的序列化字段，字段的唯一运行期写入者回到本组件自己
         //（另一个写入者是出生动画 `Start-Player.playable`，它直接驱动本属性）。
         Architecture.RegisterEvent<BattleEvent_ControlTakeOverRequested>(OnControlTakeOverRequested)
            .UnRegisterWhenGameObjectDestroyed(gameObject);

         // 2026-09-19 桥接层退役：玩家目标改为**自注册**（取代宿主的 SyncRuntimeBridge 每帧喂镜像）。
         // 读者：BattleStageDirector.IsPlayerWithin + 4 个 IEnemyBehaviour + BattleEnemyEntity 的两处。
         BattlePlayerRegistry.Register(this);

         // 已迁出的旧 HUD 不再参与运行；Editor 清理工具会从资产移除该节点。
         var oldHud = transform.Find("MainCanvas");
         if (oldHud) oldHud.gameObject.SetActive(false);
      }

      /// <summary>接管请求的落地：写自己的字段（同步派发，事件返回前已生效）。</summary>
      private void OnControlTakeOverRequested(BattleEvent_ControlTakeOverRequested e)
      {
         ControlTakeOff = e.TakeOver;
      }

      // 所有 HUD 字段已迁出；资产清理由 Thrud/Battle/Actor 菜单完成。

      /// <summary>玩家根节点（<see cref="IBattlePlayerTarget"/> 实现）：敌人朝向/追击/距离判定用。</summary>
      public Transform RootTransform => transform;

      /// <summary>控制器销毁时注销玩家目标、清理池与事件注册。</summary>
      private void OnDestroy()
      {
         // 身份守卫在注册表内部（只有当前注册者能清空）——等价于旧桥接的 owner 守卫
         BattlePlayerRegistry.UnRegister(this);

         // 池根为 DontDestroyOnLoad：战斗离场/重开前把仍在场的子弹/弹道清回对象池，防弹幕滞留跨场景
         BattleObjectPool.DespawnAllActive();

         BattleElementBulletLibrary.Clear();
         this.UnRegisterAll();
      }

      /// <summary>获取武器实例的父节点。</summary>
      public Transform GetWeaponParent()
      {
         return WeaponAxis;
      }

      /// <summary>计算当前锁敌插值速度。</summary>
      public float GetCurrentLockSpeed(float multiplier)
      {
         return PlayerConfig.LockEnemySpeed * Mathf.Max(1f, multiplier);
      }
   }
}
