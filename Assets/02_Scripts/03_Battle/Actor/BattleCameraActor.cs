using UnityEngine;

namespace TeaGame
{
   /// <summary>相机表现的插值缓存归 Actor，Host 保持帧顺序。</summary>
   public sealed class BattleCameraActor
   {
      private readonly Camera camera;
      private float currentFov;
      public BattleCameraActor(Camera camera, float initialFov)
      { this.camera = camera; currentFov = initialFov; }

      public void BlendFov(float target, float speed, float deltaTime)
      {
         if (!camera) return;
         currentFov = Mathf.Lerp(currentFov, target, speed * deltaTime);
         camera.fieldOfView = currentFov;
      }
   }
}
