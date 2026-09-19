using UnityEngine;

namespace TeaGame
{
   /// <summary>武器外观瞄准与观察点，不接管发射、弹药和投射物。</summary>
   public sealed class BattleWeaponActor
   {
      private readonly Transform lookMarker;
      public BattleWeaponActor(Transform lookMarker) { this.lookMarker = lookMarker; }

      public void Aim(BattleRoleRuntimeData member, Transform target, Vector3 freeLookPoint, float speed)
      {
         if (lookMarker) lookMarker.position = target ? target.position : freeLookPoint;
         member?.WeaponView?.RefreshAim(target, speed);
      }
   }
}
