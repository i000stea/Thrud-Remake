namespace TeaGame
{
   /// <summary>
   /// T3.1 关卡状态变更事件。
   /// 每次状态机发生合法转移时派发（From≠To），供 HUD/表现层订阅做界面反馈；
   /// 灰盒阶段消费方为状态日志与（后续 T3.3 HUD）结算横幅。
   /// </summary>
   public struct BattleEvent_LevelStateChanged
   {
      /// <summary>转移前状态。</summary>
      public BattleLevelState From;

      /// <summary>转移后状态。</summary>
      public BattleLevelState To;
   }

   /// <summary>
   /// T3.1 首个站点的波次已开始（导演器首次刷怪）。
   /// 状态机据此把 Prepare → Wave。
   /// </summary>
   public struct BattleEvent_Stage_WaveStarted
   {
   }

   /// <summary>
   /// T3.1 全部站点清剿完成（导演器整场胜利信号）。
   /// 状态机据此把 Wave → Victory。
   /// </summary>
   public struct BattleEvent_Stage_Cleared
   {
   }

   /// <summary>
   /// T3.1 整场限时到（导演器判负信号）。
   /// 状态机据此把 Wave → Defeat。
   /// </summary>
   public struct BattleEvent_StageTimedOut
   {
   }

   /// <summary>
   /// 暂停/恢复切换请求事件（ESC）。由 Battle_PlayerControl 派发，
   /// BattleUiSystem 订阅后统一切换 Paused/timeScale/鼠标并显隐暂停菜单（与控制器解耦）。
   /// </summary>
   public struct BattleEvent_PauseToggleRequested
   {
   }
   /// <summary>
   /// 暂停菜单"继续"请求事件（灰盒暂停：ESC 打开、点"继续"恢复）。
   /// 由暂停菜单按钮派发，PlayerControl 订阅后恢复 timeScale 并隐藏菜单。
   /// </summary>
   public struct BattleEvent_PauseResumeRequested
   {
   }

}
