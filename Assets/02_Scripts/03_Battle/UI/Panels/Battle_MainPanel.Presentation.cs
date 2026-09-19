using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   public partial class Battle_MainPanel
   {
      private static Battle_MainPanel activePanel;
      private readonly List<IUnRegister> presentationBindings = new();
      private BattleTeamHudView teamHud;
      private Text ammoText;
      private float fullHealthWidth;
      private Vector2 aimTarget;
      private Vector2 aimPosition;
      private float aimSpeed;
      private Canvas hudCanvas;

      private void BindPresentation()
      {
         if (activePanel == this && presentationBindings.Count > 0) return;
         activePanel = this;
         hudCanvas = GetComponentInParent<Canvas>();
         if (Health_Main && fullHealthWidth <= 0f) fullHealthWidth = Health_Main.rectTransform.sizeDelta.x;
         // prefab 已有节点；不在运行时创建或借用 rig 的 UI。
         var ammoNode = transform.Find("WeaponAmmoUI");
         ammoText = ammoNode ? ammoNode.GetComponent<Text>() : null;
         var state = BattleArchitecture.Interface.GetModel<BattleStateModel>();
         aimPosition = Vector2.zero;
         presentationBindings.Add(state.AimPosition.RegisterWithInitValue(value => aimTarget = value));
         presentationBindings.Add(state.AimLerpSpeed.RegisterWithInitValue(value => aimSpeed = value));
         presentationBindings.Add(state.CurrentMemberHealth.RegisterWithInitValue(_ => RefreshHealth()));
         presentationBindings.Add(state.CurrentMemberMaxHealth.RegisterWithInitValue(_ => RefreshHealth()));
         presentationBindings.Add(state.CurrentWeaponMagazine.RegisterWithInitValue(_ => RefreshAmmo()));
         presentationBindings.Add(state.CurrentWeaponMaxMagazine.RegisterWithInitValue(_ => RefreshAmmo()));
         presentationBindings.Add(state.CurrentWeaponReloading.RegisterWithInitValue(_ => RefreshAmmo()));
         presentationBindings.Add(state.CurrentWeaponReloadProgress.RegisterWithInitValue(_ => RefreshAmmo()));
         teamHud = BattleTeamHudView.Create(transform as RectTransform, state.BattleDatas.Length);
         RefreshTeam();
         presentationBindings.Add(BattleArchitecture.Interface.RegisterEvent<BattleEvent_RoleHealthChanged>(_ => RefreshTeam()));
         presentationBindings.Add(BattleArchitecture.Interface.RegisterEvent<BattleEvent_RoleDowned>(_ => RefreshTeam()));
         presentationBindings.Add(BattleArchitecture.Interface.RegisterEvent<BattleEvent_RoleSwitched>(_ => RefreshTeam()));
      }

      private void ReleasePresentation()
      {
         foreach (var binding in presentationBindings) binding.UnRegister();
         presentationBindings.Clear();
         teamHud?.Dispose();
         teamHud = null;
         if (activePanel == this) activePanel = null;
      }

      private void RefreshTeam() => teamHud?.Refresh(BattleArchitecture.Interface.GetModel<BattleStateModel>().BattleDatas);

      private void RefreshHealth()
      {
         var state = BattleArchitecture.Interface.GetModel<BattleStateModel>();
         if (Health_Main)
         {
            var size = Health_Main.rectTransform.sizeDelta;
            size.x = fullHealthWidth * Mathf.Clamp01(
               state.CurrentMemberHealth.Value / Mathf.Max(1f, state.CurrentMemberMaxHealth.Value));
            Health_Main.rectTransform.sizeDelta = size;
         }
         if (Health_Value)
         {
            Health_Value.text =
               $"{Mathf.RoundToInt(state.CurrentMemberHealth.Value)}/{Mathf.RoundToInt(state.CurrentMemberMaxHealth.Value)}";
         }
      }

      /// <summary>上次写入的装填百分比；用于节流（不足 1% 变化不重写文本，避免每帧字符串分配）。</summary>
      private int lastReloadPercent = -1;

      private void RefreshAmmo()
      {
         if (!ammoText) return;
         var state = BattleArchitecture.Interface.GetModel<BattleStateModel>();

         if (state.CurrentWeaponMagazine.Value < 0)
         {
            if (lastReloadPercent != -1 || ammoText.text.Length > 0) ammoText.text = "";
            lastReloadPercent = -1;
            return;
         }

         if (state.CurrentWeaponReloading.Value != 0)
         {
            // 「显示百分比 + 取整」是 View 的决定；模型只提供原始 0..1 进度
            var percent = Mathf.RoundToInt(state.CurrentWeaponReloadProgress.Value * 100f);
            if (percent == lastReloadPercent) return;
            lastReloadPercent = percent;
            ammoText.text = $"装填中… {percent}%";
            return;
         }

         var text = $"子弹 {state.CurrentWeaponMagazine.Value}/{state.CurrentWeaponMaxMagazine.Value}";
         if (text == ammoText.text) return;
         lastReloadPercent = -1;
         ammoText.text = text;
      }

      /// <summary>Host 在 Aim 发布之后驱动；插值缓存归 View，暂停时 Host 不调用。</summary>
      public static void TickForBattle(float deltaTime)
      {
         if (activePanel && activePanel.isActiveAndEnabled) activePanel.TickAim(deltaTime);
      }

      private void TickAim(float deltaTime)
      {
         if (!Cross_LockEnemy || !hudCanvas) return;
         aimPosition = Vector2.Lerp(aimPosition, aimTarget, aimSpeed * deltaTime);
         var screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            + aimPosition * (Screen.width / Battle_MainPanel.AimReferenceWidth);
         var parent = Cross_LockEnemy.parent as RectTransform;
         if (!parent) return;
         var canvas = hudCanvas.rootCanvas;
         var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
         if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera, out var local)) return;
         var anchor = Cross_LockEnemy.anchorMin
            + Vector2.Scale(Cross_LockEnemy.anchorMax - Cross_LockEnemy.anchorMin, Cross_LockEnemy.pivot);
         Cross_LockEnemy.anchoredPosition = local - (parent.rect.min + Vector2.Scale(parent.rect.size, anchor));
      }
   }
}
