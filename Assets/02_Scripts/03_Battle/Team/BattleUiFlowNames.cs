namespace TeaGame
{
   /// <summary>
   /// T3.1A 流程 UI 命名约定（场景链：主场景 → 选队 → 战斗）。
   /// 面板走 ResKit(AB) 加载，命名与 ResKit 代码生成规则一致：
   /// bundle = 资产名小写 + "_prefab"；asset = prefab 文件名。
   /// 对应面板 prefab 资产：Assets/Resources/Panels/BattleMainHubPanel.prefab / BattleTeamSelectPanel.prefab。
   /// 注：这些 prefab 的 AB 标记由 Editor 工具写入；QFrameworkData/QAssets.cs 由 ResKit 生成器在打 AB 时同步。
   /// </summary>
   public static class BattleUiFlowNames
   {
      public const string MainHubPanel = "BattleMainHubPanel";
      public const string MainHubBundle = "battlemainhubpanel_prefab";

      public const string TeamSelectPanel = "BattleTeamSelectPanel";
      public const string TeamSelectBundle = "battleteamselectpanel_prefab";
   }
}
