using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TeaGame
{
   public class Battle_TouchInputPanelData : UIPanelData
   {
   }
   /// <summary>
   /// T1 触屏操作层（13 Demo P3 布局：左下摇杆 / 右下普攻+E/Q+切人+暂停 / 屏中约 40% 宽拖拽转镜头）。
   ///
   /// 实现方式：**UIKit 标准 UIPanel**（与 LevelSelect/Formation/结算面板一致）——
   /// prefab 由编辑器生成器创建（Thrud/Battle/T1/Create Touch Input Panel），
   /// 根节点挂本组件；面板经 <see cref="UIKit.OpenPanel{T}"/> 打开到 UILevel.Common 层
   /// （低于暂停/结算所用的 PopUI 层：弹层打开时自然盖住触摸层，语义与 ESC 暂停一致）。
   /// 面板资源走 ResKit AssetBundle（AB = battletouchinputpanel_prefab），不依赖 Resources。
   ///
   /// 挂载与生命周期：由 battle_runtime（<see cref="BattleSceneRuntimeHost"/>）在战斗场景装配时
   /// 调用 <see cref="TryOpenForBattle"/>（真机平台或 Editor 调试开关）；离场由宿主的 OnDestroy 关闭，
   /// 随场景销毁而残留在 UIKit 面板表里的条目由 <see cref="UiPanelFlow"/> 摘下（P1，2026-09-10）。
   /// </summary>
   public partial class Battle_TouchInputPanel : UIPanel
   {
      /// <summary>本面板 AB 名（与 prefab assetBundleName 一致；规则 = 资产名小写 + "_prefab"）。</summary>
      public const string AssetBundleName = QAssetBundle.Battle_touchinputpanel_prefab.BundleName;

      /// <summary>prefab 资源名（UIKit ResKit 加载用，= prefab 文件名）。</summary>
      public const string PrefabName = QAssetBundle.Battle_touchinputpanel_prefab.Battle_TouchInputPanel;

      private bool mEnabled;

      /// <summary>
      /// 战斗进场装配入口：真机平台或 Editor 启用时经 UIKit 打开触摸层（幂等：已开则复用）。
      /// 面板 prefab 缺失（AB 未打/未含）时只警告不中断战斗。
      /// </summary>
      public static void TryOpenForBattle()
      {
         if (!BattleVirtualInput.ShouldEnableTouchUi) return;

         EnsureEventSystem();
         EnsureUIRootReady();

         // P1（2026-09-10）：改用统一入口。原实现在这里额外 Allocate 一个 ResLoader 做 AB 预检，
         // 且为避免"预检 loader 回收过早导致刚加载的 prefab 被卸载"而**永久不回收**——
         // 结果是每次进战斗泄漏一个常驻 loader（该 bundle 再也不卸载）。
         // UiPanelFlow.TryOpen 内部先清理 UIKit 表里的已销毁残留（跨场景/重开本关的必踩点），
         // 再用 OpenPanel 的返回值判定成败，失败时会打出可排查的错误。
         var panel = UiPanelFlow.TryOpen<Battle_TouchInputPanel>(
            UILevel.Common,
            null,
            AssetBundleName,
            PrefabName);

         if (panel == null)
         {
            Debug.LogWarning("[T1] 触屏操作层打开失败（详见上一条 [UI] 错误）。" +
                             "面板 prefab：Assets/04_Prefabs/Panel/Battle/Battle_TouchInputPanel.prefab" +
                             $"（bundle={AssetBundleName}, asset={PrefabName}）。");
            return;
         }

         Debug.Log("[T1] 触屏操作层已打开（移动平台或 Editor 启用）。");
      }

      /// <summary>
      /// UIRoot 归属是否已稳定（跨场景进入的关键判定）：
      /// UIKit 若复用"属于即将卸载的旧场景"的 UIRoot（<see cref="QFramework.UIRoot.Instance"/> 内部缓存），
      /// 挂在其下的面板会随旧场景一起销毁。仅当 UIKit 实际使用的 UIRoot 属于本场景、
      /// 属于 DontDestroyOnLoad，或不存在（由 UIKit 自建常驻根）时才可打开面板。
      /// 注意：必须用 <c>UIRoot.Instance</c>（UIKit 真正使用的实例）判定，不能用 FindObjectOfType。
      /// </summary>
      public static bool IsUIRootReadyForScene(Scene scene)
      {
         var root = QFramework.UIRoot.Instance;
         if (root == null) return true;

         var rootScene = root.gameObject.scene;
         if (!rootScene.IsValid() || !rootScene.isLoaded) return true;

         return rootScene == scene || rootScene.name == "DontDestroyOnLoad";
      }

      /// <summary>
      /// 确保存在 uGUI 事件系统（UIKit UIRoot 自带 EventSystem，若尚未创建则补建一次）。
      /// </summary>
      private static void EnsureEventSystem()
      {
         if (Object.FindObjectOfType<EventSystem>() != null) return;

         var es = new GameObject("EventSystem(T1)", typeof(EventSystem));
         es.AddComponent<StandaloneInputModule>();
         Debug.Log("[T1] 场景无 EventSystem，已补建（uGUI 触控依赖）。");
      }

      /// <summary>
      /// 确保 UIKit 已初始化（UIRoot/EventSystem 未加载时先触发一次创建，
      /// 避免 OpenPanel 因框架根未就绪而静默失败）。
      /// </summary>
      private static void EnsureUIRootReady()
      {
         _ = QFramework.UIRoot.Instance;
      }

      /// <summary>
      /// 战斗离场入口：关闭触摸层（幂等）。
      /// 注意：面板已随场景销毁时 <c>UIKit.GetPanel</c> 返回 Unity 判空的 null，此处会直接跳过——
      /// 遗留的面板表条目由 <see cref="UiPanelFlow.SanitizeRegistry"/> 在下次打开/场景离场时摘除。
      /// </summary>
      public static void CloseForBattle()
      {
         UiPanelFlow.CloseAlive<Battle_TouchInputPanel>();
      }

      protected override void OnOpen(IUIData uiData = null)
      {
         if (!mEnabled)
         {
            mEnabled = true;
            BattleVirtualInput.SetEnabled(true);
         }
      }

      protected override void OnClose()
      {
         if (mEnabled)
         {
            mEnabled = false;
            BattleVirtualInput.SetEnabled(false);
         }
      }
   }
}
