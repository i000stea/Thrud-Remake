using TeaGame;
using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// T2.3.1 玩家元素子弹库（2026-09-04）。
   /// PlayerControl 在战斗启动时把 4 个元素子弹 prefab（Bullet_Player_Ice/Fire/Wind/Electric）注册进来；
   /// 武器开火时按 <see cref="BattleRuntimeBridge.ActiveElement"/> 解析对应元素子弹打出——
   /// 每把武器不再单独配置元素子弹，统一走本库。
   /// 未注册 / 元素为 none 时返回 null，调用方回退武器默认子弹 prefab（Bullet_Player_Base）。
   /// </summary>
   public static class BattleElementBulletLibrary
   {
      private static GameObject sIce;
      private static GameObject sFire;
      private static GameObject sWind;
      private static GameObject sElectric;

      /// <summary>注册四元素子弹 prefab（可为空；元素 none 时无需注册）。</summary>
      public static void Register(GameObject ice, GameObject fire, GameObject wind, GameObject electric)
      {
         sIce = ice;
         sFire = fire;
         sWind = wind;
         sElectric = electric;
      }

      /// <summary>清空注册（场景销毁时调用，避免跨场景残留引用）。</summary>
      public static void Clear()
      {
         sIce = null;
         sFire = null;
         sWind = null;
         sElectric = null;
      }

      /// <summary>按元素取对应子弹 prefab；none/未注册返回 null。</summary>
      public static GameObject Resolve(DamageElement element)
      {
         switch (element)
         {
            case DamageElement.Ice: return sIce;
            case DamageElement.Fire: return sFire;
            case DamageElement.Wind: return sWind;
            case DamageElement.Electric: return sElectric;
            default: return null;
         }
      }
   }
}
