using UnityEditor;
using UnityEngine;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// T1 Editor 触屏开关：编辑器内**默认启用**触屏 UI（便于调试手机布局）。
   /// 触屏层与键鼠在编辑器下并行工作、互不冲突（输入在采集层合并；指针位于触摸 UI 上时屏蔽鼠标轴/左键）。
   /// 切换后需重新进入战斗（battle_runtime Start 时按 <see cref="BattleVirtualInput.ShouldEnableTouchUi"/> 打开）。
   /// </summary>
   public static class TouchInputDebugMenu
   {
      private const string MenuRoot = "Thrud/Battle/Touch Input/";
      private const string ToggleMenu = MenuRoot + "Show Touch UI in Editor";

      [MenuItem(ToggleMenu)]
      public static void ToggleInEditor()
      {
         BattleVirtualInput.EditorTouchEnabled = !BattleVirtualInput.EditorTouchEnabled;
         Debug.Log(BattleVirtualInput.EditorTouchEnabled
            ? "[T1] 编辑器内触屏 UI：已开启（下次进入战斗生效）。"
            : "[T1] 编辑器内触屏 UI：已关闭（键鼠路径；下次进入战斗生效）。");
      }

      [MenuItem(ToggleMenu, true)]
      public static bool ToggleInEditorValidate()
      {
         Menu.SetChecked(ToggleMenu, BattleVirtualInput.EditorTouchEnabled);
         return true;
      }
   }
}
