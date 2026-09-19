using UnityEngine;

namespace TeaGame
{
   /// <summary>已创建角色的挂点、显隐、动画与模型朝向；不决定选角和阵亡规则。</summary>
   public sealed class BattleRoleActor
   {
      private readonly Transform roles;
      private readonly Transform weapons;
      private readonly Transform viewAxis;
      private BattleRoleRuntimeData[] members = System.Array.Empty<BattleRoleRuntimeData>();
      private Animator animator;

      public BattleRoleActor(Transform roles, Transform weapons, Transform viewAxis)
      { this.roles = roles; this.weapons = weapons; this.viewAxis = viewAxis; }

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
