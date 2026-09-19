using QFramework;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 角色 3D 表现 Actor（3D 空间的 View 层）。
   ///
   /// **职责边界（2026-09-19 第 1 步拆分）**：
   /// - 只负责"已创建角色实例的挂点、显隐、动画与模型朝向"；
   /// - **不决定**选角与阵亡规则（那是 <see cref="BattleRoleSystem"/> 的职责，本类只按事件/状态显示）；
   /// - **自己订阅**演出与切人事件（<see cref="BattleEvent_RollTriggered"/> / <see cref="BattleEvent_RoleSwitched"/> /
   ///   <see cref="BattleEvent_RoleDowned"/>）刷新自身。此前由 `BattleRoleSystem` 持有本 Actor 并直接驱动
   ///   （`actor?.ShowMember(...)` / `e => actor?.PlayRoll(...)`），属 System → View 的错误耦合，已移除。
   /// - 数据流向**单向**：本类只**读** `BattleStateModel`，从不写。
   ///
   /// **生命周期**：由 battle_runtime（<see cref="BattleSceneRuntimeHost"/>）持有——
   /// `Initialize(...)` 装配 + 订阅，`Dispose()` 反注册。
   /// ⚠️ 本类是**纯 C# 类（非 MonoBehaviour）**，无法使用 `UnRegisterWhenGameObjectDestroyed`，
   /// 因此 `Dispose()` 必须由持有者显式调用，否则事件句柄会残留。
   ///
   /// 设计背景见 `Docs/03_专项设计与重构/19_表现层分层_View与Actor_设计.md`。
   /// </summary>
   public sealed class BattleRoleActor
   {
      private readonly Transform roles;
      private readonly Transform weapons;
      private readonly Transform viewAxis;
      private BattleRoleRuntimeData[] members = System.Array.Empty<BattleRoleRuntimeData>();
      private Animator animator;

      /// <summary>本 Actor 自己的事件反注册句柄（纯 C# 类，无自动反注册）。</summary>
      private IUnRegister mSwitched;
      private IUnRegister mDowned;
      private IUnRegister mRoll;

      private static IArchitecture Architecture => BattleArchitecture.Interface;

      public BattleRoleActor(Transform roles, Transform weapons, Transform viewAxis)
      { this.roles = roles; this.weapons = weapons; this.viewAxis = viewAxis; }

      /// <summary>
      /// 装配：把角色/武器实例挂到挂点、订阅自身的表现事件、按当前出战槽刷新一次显隐，
      /// 最后播出生动画（这就是原 `AttachActor` "一开始就调用一次"的原因）。
      /// </summary>
      public void Initialize(BattleRoleRuntimeData[] members)
      {
         Attach(members);

         // 自持订阅：切人 → 显示新成员；阵亡 → 隐藏该成员；翻滚 → 播翻滚动画
         mSwitched = Architecture.RegisterEvent<BattleEvent_RoleSwitched>(e => ShowMember(e.Index, true));
         mDowned = Architecture.RegisterEvent<BattleEvent_RoleDowned>(e => ShowMember(e.RoleIndex, false));
         mRoll = Architecture.RegisterEvent<BattleEvent_RollTriggered>(e => PlayRoll(e.RollForce));

         RefreshCurrent();
         PlaySpawn();
      }

      /// <summary>
      /// 反注册事件并断开引用（由 battle_runtime 在离场时调用）。
      /// 纯 C# 类没有 MonoBehaviour 的销毁钩子，必须显式调用。
      /// </summary>
      public void Dispose()
      {
         mSwitched?.UnRegister();
         mSwitched = null;
         mDowned?.UnRegister();
         mDowned = null;
         mRoll?.UnRegister();
         mRoll = null;
         animator = null;
         members = System.Array.Empty<BattleRoleRuntimeData>();
      }

      /// <summary>按当前出战槽刷新显隐（战斗初始化/重开时由持有者调用）。只读模型。</summary>
      public void RefreshCurrent()
      {
         var state = Architecture.GetModel<BattleStateModel>();
         var data = state.CurrentBattleData;
         ShowMember(state.SelectedRoleIndex.Value, data != null && !data.IsDowned);
      }

      /// <summary>把全部成员的角色/武器实例挂到对应挂点下并归零局部变换。</summary>
      public void Attach(BattleRoleRuntimeData[] members)
      {
         this.members = members ?? System.Array.Empty<BattleRoleRuntimeData>();
         foreach (var member in this.members)
         {
            AttachTo(member?.RoleInstance, roles);
            AttachTo(member?.WeaponInstance, weapons);
         }
      }

      private static void AttachTo(Transform instance, Transform parent)
      {
         if (!instance) return;
         if (parent) instance.SetParent(parent, false);
         instance.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
      }

      /// <summary>按槽位序显示/隐藏成员（同时解析出该成员的 Animator 供后续动画调用）。</summary>
      public void ShowMember(int index, bool show)
      {
         animator = null;
         for (var i = 0; i < members.Length; i++)
         {
            var member = members[i];
            if (member == null) continue;
            var visible = i == index && show;
            if (member.RoleInstance) member.RoleInstance.gameObject.SetActive(visible);
            if (member.WeaponInstance) member.WeaponInstance.gameObject.SetActive(visible);
            if (visible) animator = member.RoleAnimator;
         }
      }

      public void PlaySpawn() { if (animator) animator.SetTrigger("GameStart"); }

      public void PlayRoll(Vector2 direction)
      {
         if (!animator) return;
         animator.SetFloat("RollFront", direction.y);
         animator.SetFloat("RollSide", direction.x);
         animator.Play("RollBlend", 0, 0f);
      }

      public void SetMovement(Vector2 velocity, bool sprinting)
      {
         if (!animator) return;
         animator.SetFloat("MoveFront", velocity.x);
         animator.SetFloat("MoveSide", velocity.y);
         animator.SetBool("OnSprint", sprinting);
      }

      public void FollowView(Quaternion rotation, float speed, float deltaTime)
      {
         if (!viewAxis) return;
         var euler = Quaternion.Lerp(viewAxis.rotation, rotation, deltaTime * speed).eulerAngles;
         euler.z = 0f;
         viewAxis.rotation = Quaternion.Euler(euler);
      }

      /// <summary>由任意方向构造"只含 yaw+pitch、无 roll"的旋转，并把俯仰限制在 ±80°。</summary>
      public static Quaternion ClampLookRotation(Vector3 direction)
      {
         var normalized = direction.normalized;
         var flat = new Vector3(normalized.x, 0f, normalized.z);
         if (flat.sqrMagnitude < 0.0001f) return Quaternion.identity;
         var pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -80f, 80f);
         return Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(-pitch, 0f, 0f);
      }
   }
}
