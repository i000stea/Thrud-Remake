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
         var hud = state.Hud;
         aimPosition = Vector2.zero;
         presentationBindings.Add(hud.AimPosition.RegisterWithInitValue(value => aimTarget = value));
         presentationBindings.Add(hud.AimLerpSpeed.RegisterWithInitValue(value => aimSpeed = value));
         presentationBindings.Add(hud.Health.RegisterWithInitValue(_ => RefreshHealth()));
         presentationBindings.Add(hud.MaxHealth.RegisterWithInitValue(_ => RefreshHealth()));
         presentationBindings.Add(hud.Magazine.RegisterWithInitValue(_ => RefreshAmmo()));
         presentationBindings.Add(hud.MaxMagazine.RegisterWithInitValue(_ => RefreshAmmo()));
         presentationBindings.Add(hud.Reloading.RegisterWithInitValue(_ => RefreshAmmo()));
         presentationBindings.Add(hud.ReloadStep.RegisterWithInitValue(_ => RefreshAmmo()));
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
         var hud = BattleArchitecture.Interface.GetModel<BattleStateModel>().Hud;
         if (Health_Main)
         {
            var size = Health_Main.rectTransform.sizeDelta;
            size.x = fullHealthWidth * Mathf.Clamp01(hud.Health.Value / Mathf.Max(1f, hud.MaxHealth.Value));
            Health_Main.rectTransform.sizeDelta = size;
         }
         if (Health_Value) Health_Value.text = $"{Mathf.RoundToInt(hud.Health.Value)}/{Mathf.RoundToInt(hud.MaxHealth.Value)}";
      }

      private void RefreshAmmo()
      {
         if (!ammoText) return;
         var hud = BattleArchitecture.Interface.GetModel<BattleStateModel>().Hud;
         ammoText.text = hud.Magazine.Value < 0 ? "" : hud.Reloading.Value != 0
            ? $"装填中… {hud.ReloadStep.Value * 5}%"
            : $"子弹 {hud.Magazine.Value}/{hud.MaxMagazine.Value}";
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
            + aimPosition * (Screen.width / BattleHudState.AimReferenceWidth);
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
