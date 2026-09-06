using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 面板加载辅助：打开前经 ResKit 校验 AB 面板是否存在（与 BattleUiSystem.TryEnsurePanelAvailable 同思路）。
   /// 面板由 SupportOldQF（ResKitPanelLoaderPool）经 AssetBundle 加载到 UIRoot（UILevel.Common 层）。
   /// </summary>
   public static class BattleUiFlowPanelOpener
   {
      public static bool TryEnsureAvailable(string bundleName, string assetName)
      {
         var loader = ResLoader.Allocate();
         var prefab = loader.LoadSync<GameObject>(bundleName, assetName);
         loader.Recycle2Cache();

         if (prefab != null) return true;

         Debug.LogWarning($"[T3.1A] 未找到流程面板 prefab（bundle={bundleName}, asset={assetName}）。" +
                          "请确认新版预编队面板（04_Prefabs/Panel/Main_BattleTeamSelectPanel）已用对应 Editor 工具生成并重新打 AB；" +
                          "旧的 TeaGame → Battle → Team Flow → Setup All 已随 v1.0 重构移除。");
         return false;
      }
   }
}
