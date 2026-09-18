using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// UIKit 面板生命周期统一入口（P1，2026-09-10）。
   ///
   /// 解决的问题（编辑器/真机均可复现的「面板完全不出现」）：
   /// 跨场景卸载或「重开本关」时，面板 GameObject 随场景销毁，但 <c>IPanel.Close</c> 从未被调用
   /// → <see cref="UIKit.Table"/> 残留「已销毁条目」→
   /// 1) <c>UIManager.OpenUI</c> 会复用该条目（<c>IPanel</c> 是接口，<c>== null</c> 不触发 Unity 判空）
   ///    → <c>Open/Show</c> 访问已销毁 MonoBehaviour 的 <c>gameObject</c> → MissingReferenceException；
   /// 2) <c>UIManager.CloseAllUI</c> 遍历时同样抛异常 → 面板表再也清不掉 → 之后所有面板都打不开。
   ///
   /// 本类所有操作都不触碰已销毁对象的 Unity 属性：<c>UIKit.Table.Remove</c> 的键 getter 会读
   /// <c>panel.Transform</c>（对已销毁对象必抛），因此这里直接操作公开的两个索引字典来摘除死条目。
   /// </summary>
   public static class UiPanelFlow
   {
      /// <summary>
      /// 摘除面板表中「已销毁」的条目，返回摘除数量（幂等，可随时调用）。
      /// </summary>
      public static int SanitizeRegistry()
      {
         var table = UIKit.Table;
         if (table == null)
         {
            return 0;
         }

         List<IPanel> dead = null;
         foreach (var panel in table)
         {
            if (!IsDestroyed(panel))
            {
               continue;
            }

            if (dead == null)
            {
               dead = new List<IPanel>();
            }

            dead.Add(panel);
         }

         if (dead == null)
         {
            return 0;
         }

         for (var i = 0; i < dead.Count; i++)
         {
            RemoveFromIndex(table.GameObjectNameIndex, dead[i]);
            RemoveFromIndex(table.TypeIndex, dead[i]);
         }

         return dead.Count;
      }

      /// <summary>
      /// 安全关闭全部面板（替代 <see cref="UIKit.CloseAllPanel"/>：后者碰到已销毁条目会抛异常而中断）。
      /// 场景离场/重开前必须调用，保证面板表随场景一起收敛。
      /// </summary>
      public static void CloseAllSafely()
      {
         var table = UIKit.Table;
         if (table == null)
         {
            return;
         }

         var removed = SanitizeRegistry();
         if (removed > 0)
         {
            Debug.Log($"[UI] 面板表清理：摘除 {removed} 个已销毁条目（跨场景/重开本关残留）。");
         }

         List<IPanel> alive = null;
         foreach (var panel in table)
         {
            if (IsDestroyed(panel))
            {
               continue;
            }

            if (alive == null)
            {
               alive = new List<IPanel>();
            }

            alive.Add(panel);
         }

         if (alive != null)
         {
            for (var i = 0; i < alive.Count; i++)
            {
               var alivePanel = alive[i];
               try
               {
                  // UIPanel.Close：Hide → Destroy(gameObject) → Loader.Unload → RecycleLoader
                  alivePanel.Close();

                  if (alivePanel.Info != null)
                  {
                     alivePanel.Info.Recycle2Cache();
                     alivePanel.Info = null;
                  }
               }
               catch (System.Exception e)
               {
                  Debug.LogWarning($"[UI] 关闭面板 {alivePanel.GetType().Name} 失败（已跳过）：{e.Message}");
               }
            }
         }

         // 兜底：确保不留残项（此后新面板才能正常打开）
         table.Clear();
      }

      /// <summary>
      /// 打开面板的统一入口：先清理已销毁残留，再 OpenPanel，并按返回值判定成败。
      /// 取代此前各处「额外 ResLoader 预检 AB」的写法——预检会重复同步加载同一个 bundle，
      /// 且其 loader 的回收时机与 ResKit 的延迟卸载耦合（曾导致刚加载的面板 prefab 被卸载、
      /// 或 loader 常驻泄漏）。
      /// </summary>
      public static T TryOpen<T>(UILevel level, IUIData uiData, string bundleName, string prefabName)
         where T : UIPanel
      {
         var removed = SanitizeRegistry();
         if (removed > 0)
         {
            Debug.Log($"[UI] 面板表清理：摘除 {removed} 个已销毁条目（否则 OpenPanel 会复用已销毁实例）。");
         }

         try
         {
            var panel = UIKit.OpenPanel<T>(level, uiData, bundleName, prefabName);
            if (panel == null)
            {
               Debug.LogError($"[UI] 面板 {typeof(T).Name} 打开失败：OpenPanel 返回 null" +
                              $"（bundle={bundleName}, prefab={prefabName}）。" +
                              "请对照启动时的 [ResKit] 初始化校验日志确认该资源是否在 AB 清单内。");
            }

            return panel;
         }
         catch (System.Exception e)
         {
            Debug.LogError($"[UI] 面板 {typeof(T).Name} 打开异常（bundle={bundleName}, prefab={prefabName}）：{e}");

            // OpenPanel 内部是"先 CreateUI（入表 + Init）再 Open"：Open 抛异常时面板会以 Opening 状态
            // 留在 UIKit 表里、GameObject 还激活着 → 必须收掉，否则下次 OpenPanel 会复用这个半成品，
            // 表现为"面板打不开 / 一直空白"（2026-09-10 实测：OnOpen 内异常导致整块编队面板打不开）。
            DiscardHalfOpened<T>();
            return null;
         }
      }

      /// <summary>收掉"已创建但 Open 失败"的半开面板（幂等；失败只记警告）。</summary>
      private static void DiscardHalfOpened<T>() where T : UIPanel
      {
         try
         {
            if (IsAlive<T>())
            {
               UIKit.ClosePanel<T>();
            }
         }
         catch (System.Exception e)
         {
            Debug.LogWarning($"[UI] 清理半开面板 {typeof(T).Name} 失败（已忽略）：{e.Message}");
         }
      }

      /// <summary>关闭指定面板（已销毁则跳过并返回 false，不会触发 MissingReference）。</summary>
      public static bool CloseAlive<T>() where T : UIPanel
      {
         if (!IsAlive<T>())
         {
            return false;
         }

         try
         {
            UIKit.ClosePanel<T>();
            return true;
         }
         catch (System.Exception e)
         {
            Debug.LogWarning($"[UI] 关闭面板 {typeof(T).Name} 失败（已跳过）：{e.Message}");
            return false;
         }
      }

      /// <summary>面板是否存在且未销毁（<c>UIKit.GetPanel</c> 返回 MonoBehaviour，判空走 Unity 重载）。</summary>
      public static bool IsAlive<T>() where T : UIPanel
      {
         return UIKit.GetPanel<T>() != null;
      }

      /// <summary>条目是否已失效；非 Unity 对象实现无法判定时按存活处理。</summary>
      private static bool IsDestroyed(IPanel panel)
      {
         if (panel == null)
         {
            return true;
         }

         var behaviour = panel as MonoBehaviour;
         if (behaviour == null)
         {
            return false;
         }

         return behaviour == null; // Unity 判空：对象已销毁时为 true
      }

      /// <summary>从某个键索引里按引用摘除条目，并清理因此变空的键（不触发键 getter）。</summary>
      private static void RemoveFromIndex<TKey>(UIKitTableIndex<TKey, IPanel> index, IPanel panel)
      {
         if (index == null)
         {
            return;
         }

         var dict = index.Dictionary;
         if (dict == null)
         {
            return;
         }

         List<TKey> emptyKeys = null;
         foreach (var pair in dict)
         {
            pair.Value.Remove(panel);

            if (pair.Value.Count != 0)
            {
               continue;
            }

            if (emptyKeys == null)
            {
               emptyKeys = new List<TKey>();
            }

            emptyKeys.Add(pair.Key);
         }

         if (emptyKeys == null)
         {
            return;
         }

         for (var i = 0; i < emptyKeys.Count; i++)
         {
            dict.Remove(emptyKeys[i]);
         }
      }
   }
}
