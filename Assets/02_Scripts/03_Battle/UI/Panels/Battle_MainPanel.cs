using QFramework;
using UnityEngine;

namespace TeaGame
{
   public class Battle_MainPanelData : UIPanelData
   {
   }

   /// <summary>
   /// 战斗主 HUD 面板（原 <see cref="Battle_PlayerControl"/> 上的 SkillHud 逻辑迁入）。
   ///
   /// **驱动方式：BindableProperty 反向绑定**——面板只负责"注册 + 渲染"，
   /// 数据变化时由 <see cref="BattleHudState"/> 反过来通知本面板刷新（View 注册、Model 推送）。
   /// 与拆分前的区别：以前是 `Battle_PlayerControl` 用 `RegisterEvent` 订阅 3 个事件再自己拼字符串，
   /// 逻辑与渲染都挂在 rig（View 载体）上；现在数据源是可绑定属性、渲染在**面板**里，
   /// rig 不再承担任何 UI 文本职责。
   ///
   /// **注册生命周期**：绑定用 `UnRegisterWhenGameObjectDestroyed(gameObject)`，
   /// 面板关闭（UIPanel.Close 会 Destroy 面板 GameObject）时自动反注册，无需手工维护句柄。
   /// `mBound` 只用于防止同一实例被重复 Open 时重复注册。
   ///
   /// **面板资源**：prefab `Assets/04_Prefabs/Panel/Battle/Battle_MainPanel.prefab`（AB = `battle_mainpanel_prefab`）。
   /// 同 <c>BattleAssetService</c> 的做法，AB 名在此**硬编码**：`QAssetBundle.Battle_mainpanel_prefab`
   /// 由 ResKit 在**构建 AB 时**生成，首次构建前引用它会导致编译不过。
   /// → **构建过一次 AB（QAssets.cs 重新生成）后可改回常量引用**，届时与其它面板写法一致。
   ///
   /// **归属**：由 battle_runtime（<see cref="BattleSceneRuntimeHost"/>）在战斗装配时打开、离场时关闭，
   /// 与触屏层（<see cref="Battle_TouchInputPanel"/>）同一时机、同一 UIRoot 归属判定。
   /// </summary>
   public partial class Battle_MainPanel : UIPanel
   {
      /// <summary>本面板 AB 名（与 prefab 的 assetBundleName 一致；规则 = 资产名小写 + "_prefab"）。</summary>
      public const string AssetBundleName = "battle_mainpanel_prefab";

      /// <summary>prefab 资源名（UIKit 经 ResKit 加载用，= prefab 文件名）。</summary>
      public const string PrefabName = "Battle_MainPanel";

      /// <summary>是否已注册过绑定（防止同一面板实例重复 Open 时重复注册）。</summary>
      private bool mBound;

      protected override void OnInit(IUIData uiData = null)
      {
         mData = uiData as Battle_MainPanelData ?? new Battle_MainPanelData();
      }

      protected override void OnOpen(IUIData uiData = null)
      {
         BindPresentation();
         BindHud();
      }

      protected override void OnClose()
      {
         ReleasePresentation();
         // 绑定句柄由 UnRegisterWhenGameObjectDestroyed 随面板销毁自动反注册，这里只复位标志。
         mBound = false;
      }

      protected override void OnBeforeDestroy()
      {
         ReleasePresentation();
         base.OnBeforeDestroy();
      }

      /// <summary>
      /// 战斗进场：打开主 HUD（幂等）。面板 prefab 缺失（AB 未打/未含）时只警告，不中断战斗。
      /// </summary>
      public static void TryOpenForBattle()
      {
         var panel = UiPanelFlow.TryOpen<Battle_MainPanel>(
            UILevel.Common,
            null,
            AssetBundleName,
            PrefabName);

         if (panel == null)
         {
            Debug.LogWarning("[Battle_MainPanel] 主 HUD 打开失败（详见上一条 [UI] 错误）。" +
                             "面板 prefab：Assets/04_Prefabs/Panel/Battle/Battle_MainPanel.prefab" +
                             $"（bundle={AssetBundleName}, asset={PrefabName}）。");
            return;
         }

         Debug.Log("[Battle_MainPanel] 主 HUD 已打开。");
      }

      /// <summary>战斗离场：关闭主 HUD（幂等；面板已随场景销毁时直接跳过）。</summary>
      public static void CloseForBattle()
      {
         UiPanelFlow.CloseAlive<Battle_MainPanel>();
      }

      // ===================== 反向绑定：注册即读 + 变化推送 =====================

      private void BindHud()
      {
         if (mBound) return;
         mBound = true;

         var hud = BattleArchitecture.Interface.GetModel<BattleStateModel>().Hud;

         Bind(hud.SelectedRoleIndex); // 角色名 · 元素
         Bind(hud.Energy);            // Q 行能量
         Bind(hud.SkillERemain);      // E 行就绪/冷却
         Bind(hud.SkillQRemain);      // Q 行就绪/冷却
         Bind(hud.SkillQDeficit);     // Q 行能量不足提示
      }

      /// <summary>
      /// 绑定单个属性：`RegisterWithInitValue` = 注册后**立即用当前值渲染一次**，此后值变化再推送。
      /// 因此不需要额外的"首帧手动刷新"，也不会出现首次打开时文本为空。
      /// </summary>
      private void Bind<T>(BindableProperty<T> property)
      {
         property
            .RegisterWithInitValue(_ => RefreshHud())
            .UnRegisterWhenGameObjectDestroyed(gameObject);
      }

      // ===================== 渲染（只读绑定值 + StateModel，不写任何状态） =====================

      /// <summary>
      /// 按当前绑定值刷新 HUD 文本。
      /// 注意：本方法会被任一绑定属性触发，内部读取**全部**绑定值——因为 HUD 的四行是同一屏内容，
      /// 分开缓存各自的部分反而更容易不一致。
      /// </summary>
      private void RefreshHud()
      {
         var state = BattleArchitecture.Interface.GetModel<BattleStateModel>();
         var hud = state.Hud;

         var roleIndex = hud.SelectedRoleIndex.Value;
         var datas = state.BattleDatas;
         var data = datas != null && roleIndex >= 0 && roleIndex < datas.Length ? datas[roleIndex] : null;
         var element = data?.RoleData != null ? data.RoleData.damageElement : DamageElement.none;

         if (SkillHudRoleLine != null)
         {
            SkillHudRoleLine.text = (data?.RoleName ?? "--") + " · " + ElementChineseName(element);
         }

         // 无技能档案/执行器的角色（如 none 元素）E/Q 行只显示占位
         var hasSkill = element != DamageElement.none && BattleSkillCatalog.Get(element) != null;
         var energyText = $"能量 {(int)hud.Energy.Value}/{BattleStateModel.RoleEnergyMax:0}";

         if (SkillHudELine != null)
         {
            SkillHudELine.text = hasSkill
               ? "[E] 元素战技 · " + FormatSlot(hud.SkillERemain.Value, 0f)
               : "[E] —";
         }

         if (SkillHudQLine != null)
         {
            SkillHudQLine.text = hasSkill
               ? "[Q] 极奏 " + FormatSlot(hud.SkillQRemain.Value, hud.SkillQDeficit.Value) + " · " + energyText
               : "[Q] — · " + energyText;
         }

         if (SkillHudSwitchLine != null)
         {
            SkillHudSwitchLine.text = "[1-4] 切换角色";
         }
      }

      /// <summary>
      /// 槽位文案：先看冷却，再看能量门槛。
      /// 与拆分前 <c>Battle_PlayerControl.FormatSlotState</c> 的判定顺序保持一致
      /// （冷却中 → "冷却 x.xs"；冷却已到但能量不足 → "能量不足(n)"；否则 → "就绪"）。
      /// </summary>
      private static string FormatSlot(float remainSeconds, float energyDeficit)
      {
         if (remainSeconds > 0.05f)
         {
            return $"冷却 {remainSeconds:0.0}s";
         }

         if (energyDeficit > 0f)
         {
            return $"能量不足({energyDeficit:0})";
         }

         return "就绪";
      }

      /// <summary>元素枚举 → 中文（HUD 文案）。</summary>
      private static string ElementChineseName(DamageElement element)
      {
         switch (element)
         {
            case DamageElement.Ice: return "冰";
            case DamageElement.Fire: return "火";
            case DamageElement.Wind: return "风";
            case DamageElement.Electric: return "电";
            default: return "无";
         }
      }
   }
}
