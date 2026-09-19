using UnityEngine;

namespace TeaGame
{
   /// <summary>玩家物理执行器。规则由 MovementSystem 决定，不读取战斗 Model。</summary>
   public sealed class BattleMovementActor
   {
      private readonly Transform root;
      private readonly Rigidbody body;
      private readonly Transform yaw;
      private readonly Camera camera;

      public BattleMovementActor(Transform root, Rigidbody body, Transform yaw, Camera camera)
      {
         this.root = root;
         this.body = body;
         this.yaw = yaw ? yaw : root;
         this.camera = camera;
      }

      public bool IsAvailable => root && body;
      public Vector3 Forward => yaw ? yaw.forward : Vector3.forward;
      public Vector3 Position => root ? root.position : Vector3.zero;
      public Vector2 AnimationVelocity => body && yaw
         ? new Vector2(Vector3.Dot(body.velocity, yaw.forward), Vector3.Dot(body.velocity, yaw.right)) / 4f
         : Vector2.zero;

      public Vector3 ResolveDirection(float side, float depth, float forward, bool followView, bool worldDepth)
      {
         if (!yaw) return Vector3.zero;
         var forwardAxis = followView && camera ? camera.transform.forward : yaw.forward;
         return yaw.right * side + forwardAxis * forward + (worldDepth ? Vector3.up : yaw.up) * depth;
      }

      // 保留原 AddForce 默认 ForceMode 和调用阶段，翻滚不擅自改成 Impulse。
      public void ApplyForce(Vector3 force) { if (body) body.AddForce(force); }

      public void Teleport(Vector3 position, Quaternion rotation, bool clearVelocity)
      {
         if (!root) return;
         root.SetPositionAndRotation(position, rotation);
         if (!body) return;
         body.position = position;
         body.rotation = rotation;
         if (clearVelocity) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
      }
   }
}
