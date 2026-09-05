using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeaGame
{
   /// <summary>
   /// T3.1 关卡状态。
   /// Prepare=加载完成待开打；Wave=波次推进中；Victory/Defeat=终局（等待玩家操作）；
   /// Exit=离开关卡（重载/返回）。灰盒不含 Boss 状态（Boss 并作 Wave 内容，正式 Boss 关属 T3.2）。
   /// </summary>
   public enum BattleLevelState
   {
      Prepare,
      Wave,
      Victory,
      Defeat,
      Exit
   }

   /// <summary>
   /// T3.1 关卡流程状态机（灰盒）。
   ///
   /// 状态流：Prepare → Wave → (Victory | Defeat) → Exit
   /// - Prepare：关卡加载完成、首个目标点出现（等待玩家）；
   /// - Wave：玩家抵达首个站点、导演器开始刷怪（含后续站点整场波次）；
   /// - Victory：全部站点清剿（BattleStageClearedEvent）；
   /// - Defeat：玩家死亡（BattlePlayerDiedEvent）或整场限时（BattleStageTimedOutEvent）；
   /// - Exit：离开关卡（灰盒 Restart=重载当前场景；QuitToMainCity 占位日志）。
   ///
   /// 灰盒无 Boss 阶段（T2.1 取消 Boss/精英），Boss 并入 Wave 段；正式 Boss 关属 T3.2。
   /// 状态步进纪律（state-step）：每个转移都由独立事件触发并打印日志，禁止自动连跳；
   /// Defeat/Victory 后由结算弹窗按钮决定去向（点击"再来一次"→ Restart），不监听键盘、不自动跳转。
   /// </summary>
   public sealed class BattleLevelFlow : AbstractSystem
   {
      private IArchitecture Architecture => BattleArchitecture.Interface;
      private BattleStateModel State => Architecture.GetModel<BattleStateModel>();

      private IUnRegister mWaveStartedUnregister;
      private IUnRegister mClearedUnregister;
      private IUnRegister mTimedOutUnregister;
      private IUnRegister mPlayerDiedUnregister;

      /// <summary>运行时默认关卡定义兜底（数据优先：场景未绑定资产时也能跑灰盒）。</summary>
      private static BattleLevelConfig sFallbackConfig;

      /// <summary>
      /// 当前关卡定义。灰盒阶段返回运行时兜底默认值；
      /// T3.2 起由关卡引导绑定真实 ScriptableObject 资产。
      /// </summary>
      public BattleLevelConfig ActiveConfig
      {
         get
         {
            if (sFallbackConfig)
            {
               return sFallbackConfig;
            }

            sFallbackConfig = ScriptableObject.CreateInstance<BattleLevelConfig>();
            sFallbackConfig.name = "DefaultLevelConfig(RuntimeFallback)";
            return sFallbackConfig;
         }
      }

      /// <summary>当前关卡状态。</summary>
      public BattleLevelState CurrentState => State.LevelState;

      protected override void OnInit()
      {
         mWaveStartedUnregister = Architecture.RegisterEvent<BattleStageWaveStartedEvent>(_ => OnWaveStarted());
         mClearedUnregister = Architecture.RegisterEvent<BattleStageClearedEvent>(_ => OnStageCleared());
         mTimedOutUnregister = Architecture.RegisterEvent<BattleStageTimedOutEvent>(_ => OnStageTimedOut());
         mPlayerDiedUnregister = Architecture.RegisterEvent<BattlePlayerDiedEvent>(_ => OnPlayerDied());
      }

      protected override void OnDeinit()
      {
         mWaveStartedUnregister?.UnRegister();
         mWaveStartedUnregister = null;
         mClearedUnregister?.UnRegister();
         mClearedUnregister = null;
         mTimedOutUnregister?.UnRegister();
         mTimedOutUnregister = null;
         mPlayerDiedUnregister?.UnRegister();
         mPlayerDiedUnregister = null;
      }

      /// <summary>
      /// 开始关卡（PlayerControl.Start 装配时调用，每场仅一次）。
      /// 无条件复位到 Prepare：
      /// - 兼容编辑器"不重载域"时静态架构残留的终局/战斗状态（此前带守卫会直接 return，
      ///   导致首个 [T3.1] 状态日志永远不出现）；
      /// - 终局去向由结算弹窗按钮触发（T3.1 灰盒），不再监听键盘。
      /// </summary>
      public void StartLevel()
      {
         TransitionTo(BattleLevelState.Prepare);
         var config = ActiveConfig;
         Debug.Log($"[T3.1] 关卡开始：{config.displayName}（{config.levelId}），等待玩家抵达首个目标点。");
      }

      /// <summary>
      /// 首个站点波次开始 → Wave。
      /// </summary>
      private void OnWaveStarted()
      {
         if (State.LevelState != BattleLevelState.Prepare)
         {
            return;
         }

         TransitionTo(BattleLevelState.Wave);
      }

      /// <summary>
      /// 全部站点清剿 → Victory。
      /// 进入终局后接管控制权，停止战斗输入。
      /// </summary>
      private void OnStageCleared()
      {
         if (State.LevelState != BattleLevelState.Wave)
         {
            return;
         }

         TransitionTo(BattleLevelState.Victory);
         TakeOverControl();
      }

      /// <summary>
      /// 整场限时到 → Defeat。
      /// </summary>
      private void OnStageTimedOut()
      {
         if (State.LevelState != BattleLevelState.Wave)
         {
            return;
         }

         TransitionTo(BattleLevelState.Defeat);
         TakeOverControl();
      }

      /// <summary>
      /// 玩家死亡（BattlePlayerDiedEvent）→ Defeat。
      /// </summary>
      private void OnPlayerDied()
      {
         if (State.LevelState == BattleLevelState.Victory
             || State.LevelState == BattleLevelState.Defeat
             || State.LevelState == BattleLevelState.Exit)
         {
            return;
         }

         TransitionTo(BattleLevelState.Defeat);
      }

      /// <summary>
      /// 重开本关：Exit → 重载当前场景。
      /// </summary>
      public void Restart()
      {
         if (State.LevelState == BattleLevelState.Exit)
         {
            return;
         }

         TransitionTo(BattleLevelState.Exit);

         // 若从暂停菜单重开：恢复时间流速并清除暂停态，避免新场景仍冻结
         State.Paused = false;
         Time.timeScale = 1f;

         var activeScene = SceneManager.GetActiveScene();
         SceneManager.LoadScene(activeScene.buildIndex);
      }

      /// <summary>
      /// 返回主城（灰盒占位：仅日志；正式接 MainCity 场景名/引导系统后实现）。
      /// </summary>
      public void QuitToMainCity()
      {
         Debug.LogWarning("[T3.1] QuitToMainCity 灰盒占位：返回主城/场景列表待接入（先按 R 重开本关）。");
      }

      // ---------------- 内部 ----------------

      private void TransitionTo(BattleLevelState next)
      {
         var from = State.LevelState;
         if (from == next)
         {
            return;
         }

         State.LevelState = next;
         Debug.Log($"[T3.1] 关卡状态 {from} → {next}");

         // 终局释放鼠标锁定：结算弹窗需鼠标点击（Escape/左键重锁逻辑在相机系统内对终局停用）
         if (next == BattleLevelState.Victory || next == BattleLevelState.Defeat)
         {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
         }

         Architecture.SendEvent(new BattleLevelStateChangedEvent { From = from, To = next });
      }

      /// <summary>终局接管控制：停止移动/开火/技能（HealthSystem 已忽略后续伤害）。</summary>
      private void TakeOverControl()
      {
         var controller = State.Controller;
         if (controller)
         {
            controller.ControlTakeOff = true;
         }

         Architecture.GetSystem<BattleRoleSystem>().SetControlTakenOver(true);
      }
   }
}
