using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TeaGame
{
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
   /// 调用 <see cref="TryOpenForBattle"/>（真机平台或 Editor 调试开关）；离场经 UIKit.ClosePanel
   /// 关闭（宿主 OnDestroy + 既有离场 CloseAllPanel 出口双保险）。
   /// </summary>
   public partial class BattleTouchInputPanel : UIPanel
   {
      /// <summary>本面板 AB 名（与 prefab assetBundleName 一致；规则 = 资产名小写 + "_prefab"）。</summary>
      public const string AssetBundleName = "battletouchinputpanel_prefab";

      /// <summary>prefab 资源名（UIKit ResKit 加载用，= prefab 文件名）。</summary>
      public const string PrefabName = "BattleTouchInputPanel";

      private bool mEnabled;

      /// <summary>
      /// 战斗进场装配入口：真机平台或 Editor 启用时经 UIKit 打开触摸层（幂等：已开则跳过）。
      /// 面板 prefab 缺失（AB 未打/未含）时只警告不中断战斗。
      /// </summary>
      public static void TryOpenForBattle()
      {
         if (!BattleVirtualInput.ShouldEnableTouchUi) return;

         EnsureEventSystem();
         EnsureUIRootReady();

         var exist = UIKit.GetPanel<BattleTouchInputPanel>();
         if (exist != null) return; // MonoBehaviour 判空：已销毁面板在此返回 null，继续重建

         // 跨场景卸载可能让 UIKit 面板表残留已销毁条目（接口判空不走 Unity 判空），先清理一次
         try
         {
            UIKit.ClosePanel<BattleTouchInputPanel>();
         }
         catch (System.Exception e)
         {
            Debug.LogWarning($"[T1] 清理残留触摸面板表项失败（可忽略）：{e.Message}");
         }

         // 先经 ResKit 校验 AB，避免 UIKit 打开空面板（对齐 BattleUiSystem 纪律）
         var loader = QFramework.ResLoader.Allocate();
         var prefab = loader.LoadSync<GameObject>(AssetBundleName, PrefabName);
         loader.Recycle2Cache();
         if (prefab == null)
         {
            Debug.LogWarning("[T1] 未找到触屏操作层面板 prefab" +
                             $"(bundle={AssetBundleName}, asset={PrefabName})。" +
                             "请先运行 Thrud/Battle/T1/Create Touch Input Panel (prefab) 生成预置体并重新 Build AssetBundles。");
            return;
         }

         UIKit.OpenPanel<BattleTouchInputPanel>(
            UILevel.Common,
            null,
            AssetBundleName,
            PrefabName);
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

      /// <summary>战斗离场入口：关闭触摸层（幂等）。</summary>
      public static void CloseForBattle()
      {
         if (UIKit.GetPanel<BattleTouchInputPanel>() == null) return;
         UIKit.ClosePanel<BattleTouchInputPanel>();
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
