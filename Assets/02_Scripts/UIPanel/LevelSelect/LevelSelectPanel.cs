using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// 关卡选择面板数据（D10 注入）。
   /// </summary>
   public class LevelSelectPanelData : UIPanelData
   {
      public BattleLevelCatalog Catalog;
   }

   /// <summary>
   /// 关卡选择覆盖层（T3.1C 步骤7，D10 入口链路第一环）。
   /// Shell + View 合并单文件（对齐 Main_FormationPanel 风格）：
   /// - 数据：关卡目录（注入优先；未注入回落 prefab 序列化 catalog）；
   /// - 展示：关卡列表（模板克隆：DisplayName）→ 点选显示介绍（关卡 Intro + 模式/场景/限时/站点摘要）；
   /// - 「进入」：写 <see cref="BattleLevelSession"/> → 关闭本面板 → 打开 <see cref="Main_FormationPanel"/>（角色选择）；
   /// - 「返回」：仅关闭本面板（覆盖层语义）。
   /// 预置体由编辑器生成器创建（Thrud/Battle/T3.1C/7. Create LevelSelect Panel）。
   /// </summary>
   public partial class LevelSelectPanel : UIPanel
   {
      /// <summary>本面板 AB 名（与预置体 assetBundleName 一致；QAssetBundle 重生成后可改用生成类）。</summary>
      public const string AssetBundleName = "levelselectpanel_prefab";

      /// <summary>打开角色选择面板的 AB 名（沿用既有 main_battleteamselectpanel_prefab）。</summary>
      public const string FormationAssetBundleName = "main_battleteamselectpanel_prefab";

      [Header("关卡目录（回退：注入 Data.Catalog 优先）")]
      [SerializeField] private BattleLevelCatalog catalog;

      private BattleLevelData mSelectedLevel;
      private bool mViewWired;

      protected override void OnInit(IUIData uiData = null)
      {
         WireView();
      }

      protected override void OnOpen(IUIData uiData = null)
      {
         var data = uiData as LevelSelectPanelData ?? new LevelSelectPanelData();
         var resolvedCatalog = data.Catalog != null ? data.Catalog : catalog;
         mSelectedLevel = null;

         if (Title_Text)
         {
            Title_Text.text = "选择关卡";
         }

         if (EnterButton)
         {
            EnterButton.interactable = false;
         }

         RefreshLevelList(resolvedCatalog);
         ShowEmptyIntro();
      }

      protected override void OnClose()
      {
         mSelectedLevel = null;
      }

      // ===================== View 装配（一次性） =====================

      private void WireView()
      {
         if (mViewWired) return;
         mViewWired = true;

         if (EnterButton)
         {
            EnterButton.onClick.AddListener(OnEnterClicked);
         }

         if (BackButton)
         {
            BackButton.onClick.AddListener(() => UIKit.ClosePanel<LevelSelectPanel>());
         }
      }

      // ===================== 关卡列表 =====================

      private void RefreshLevelList(BattleLevelCatalog source)
      {
         if (LevelList == null || LevelItemTemplate == null)
         {
            Debug.LogWarning("[LevelSelectPanel] 未绑定 LevelList/LevelItemTemplate，请核对预置体绑定。");
            return;
         }

         // 模板常隐（防止被当作一个普通条目渲染/参与布局）
         LevelItemTemplate.gameObject.SetActive(false);

         // 清空历史条目（保留模板）
         for (var i = LevelList.childCount - 1; i >= 0; i--)
         {
            var child = LevelList.GetChild(i);
            if (child != null && child.gameObject != LevelItemTemplate.gameObject)
            {
               Destroy(child.gameObject);
            }
         }

         var levels = source != null ? source.Levels : null;
         if (levels == null || levels.Length == 0)
         {
            Debug.LogWarning("[LevelSelectPanel] 关卡目录为空（未登记任何 BattleLevelData）。请先跑 Thrud/Battle/Level Data 生成器。");
            return;
         }

         for (var i = 0; i < levels.Length; i++)
         {
            var level = levels[i];
            if (level == null) continue;

            var index = i;
            var item = Instantiate(LevelItemTemplate.gameObject, LevelList, false);
            item.name = $"LevelItem_{index}";
            item.SetActive(true);

            var text = item.GetComponentInChildren<Text>(true);
            if (text)
            {
               text.text = level.DisplayName;
            }

            var button = item.GetComponent<Button>();
            if (button)
            {
               button.onClick.RemoveAllListeners();
               button.onClick.AddListener(() => SelectLevel(index));
            }
         }
      }

      private void SelectLevel(int index)
      {
         var levels = ResolveCatalog()?.Levels;
         if (levels == null || index < 0 || index >= levels.Length) return;

         var level = levels[index];
         if (level == null) return;

         mSelectedLevel = level;
         if (IntroTitle_Text)
         {
            IntroTitle_Text.text = level.DisplayName;
         }

         if (IntroBody_Text)
         {
            var siteCount = level.Sites == null ? 0 : level.Sites.Length;
            var modeName = BattleLevelModeName(level.Mode);
            IntroBody_Text.text = string.IsNullOrEmpty(level.Intro)
               ? "（本关暂无介绍）"
               : level.Intro;
            IntroBody_Text.text +=
               $"\n\n模式：{modeName}\n美术场景：{level.ArtSceneName}\n" +
               $"整场限时：{(level.TimeLimitSeconds > 0 ? level.TimeLimitSeconds + " 秒" : "不限")}\n站点数量：{siteCount}";
         }

         if (EnterButton)
         {
            EnterButton.interactable = true;
         }

         Debug.Log($"[LevelSelectPanel] 已选关：{level.LevelId}「{level.DisplayName}」。");
      }

      private void ShowEmptyIntro()
      {
         if (IntroTitle_Text)
         {
            IntroTitle_Text.text = "请选择关卡";
         }

         if (IntroBody_Text)
         {
            IntroBody_Text.text = "选择左侧列表中的关卡，查看介绍后点击「进入」出战。";
         }
      }

      // ===================== 进入 / 返回 =====================

      private void OnEnterClicked()
      {
         if (mSelectedLevel == null)
         {
            return;
         }

         // D11：确认后缓存关卡 → 进入角色选择（覆盖层语义：本面板关闭，不切场景）
         BattleLevelSession.Select(mSelectedLevel);
         UIKit.ClosePanel<LevelSelectPanel>();

         UIKit.OpenPanel<Main_FormationPanel>(UILevel.PopUI, null, FormationAssetBundleName);
      }

      // ===================== 工具 =====================

      private BattleLevelCatalog ResolveCatalog()
      {
         var data = Data;
         return data != null && data.Catalog != null ? data.Catalog : catalog;
      }

      private static string BattleLevelModeName(BattleLevelMode mode)
      {
         switch (mode)
         {
            case BattleLevelMode.Clear: return "清剿";
            case BattleLevelMode.Escort: return "护送";
            case BattleLevelMode.Defend: return "防守";
            default: return "未知";
         }
      }
   }
}
