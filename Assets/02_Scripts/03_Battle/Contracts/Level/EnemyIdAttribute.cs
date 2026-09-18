using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 标记"敌人类型字符串字段"，Editor 端用 <c>EnemyIdDrawer</c> 把它重绘为枚举下拉
   /// （下拉候选来自 EnemyCatalog 资产），但底层始终存字符串，便于热更新新增敌人类型。
   /// </summary>
   public sealed class EnemyIdAttribute : PropertyAttribute
   {
   }
}
