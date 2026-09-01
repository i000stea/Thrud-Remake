using UnityEngine;

namespace TeaGame
{
   /// <summary>
   /// 战斗玩家配置。
   /// 集中管理移动、相机、表现和锁敌相关参数，便于统一调节与跨场景复用。
   /// </summary>
   [CreateAssetMenu(fileName = "BattlePlayerConfig", menuName = "Thrud/Battle/PlayerConfig")]
   public class BattlePlayerConfig : ScriptableObject
   {
      [Header("移动参数")]
      /// <summary>
      /// 玩家最大生命值。
      /// 战斗灰盒阶段的生命数值来源，后续接入角色配置表后由角色数据覆盖。
      /// </summary>
      public float PlayerMaxHealth = 100f;

      /// <summary>
      /// 是否让上下移动始终基于世界坐标。
      /// 若关闭，则会上下方向也跟随朝向参考轴进行转换。
      /// </summary>
      public bool DepthFromWorld = false;

      /// <summary>
      /// 基础移动力。
      /// </summary>
      public float MoveForce = 10f;

      /// <summary>
      /// 翻滚附加倍率。
      /// </summary>
      public float RollMultiForce = 10f;

      /// <summary>
      /// 冲刺附加倍率。
      /// </summary>
      public float SprintMultiForce = 10f;

      /// <summary>
      /// 翻滚冷却时间。
      /// </summary>
      [SerializeField] private float RollCooldown = 2f;

      /// <summary>
      /// 翻滚结束后进入冲刺状态前的缓冲时间。
      /// </summary>
      [Range(0.0001f, 1f)] public float RollAfterSprintDelay = 0.7f;

      /// <summary>
      /// 获取安全的翻滚冷却。
      /// 通过下限保护，避免配置成 0 导致逻辑抖动。
      /// </summary>
      public float GetRollCooldown() => Mathf.Max(0.01f, RollCooldown);

      [Header("旋转参数")]
      /// <summary>
      /// 鼠标灵敏度。
      /// </summary>
      public float MouseSensitivity = 100f;

      [Header("视场角与显示对象")]
      /// <summary>
      /// 默认视场角。
      /// </summary>
      public float MainFov = 60f;

      /// <summary>
      /// 冲刺时的目标视场角。
      /// </summary>
      public float SprintFov = 90f;

      /// <summary>
      /// 视场角插值速度。
      /// </summary>
      public float FovLerpSpeed = 10f;

      /// <summary>
      /// 展示模型跟随旋转的平滑速度。
      /// </summary>
      public float RotationSpeed = 5f;

      [Header("锁敌参数")]
      /// <summary>
      /// 半自动吸附判定屏幕距离（px）。
      /// 目标进入该距离内自动吸附锁定；Tab 切换候选目标也以该范围为界。
      /// </summary>
      public float LockAssistDistance = 200f;

      /// <summary>
      /// 切换锁定目标的冷却时间（秒），防止 Tab 连续切换过快。
      /// </summary>
      public float LockSwitchCooldown = 0.3f;

      /// <summary>
      /// PC 端锁定切换键（半自动：在可见目标间循环切换/取消）。
      /// </summary>
      public KeyCode LockSwitchKey = KeyCode.Tab;

      /// <summary>
      /// 锁敌准星与武器朝向的基础追踪速度。
      /// </summary>
      public float LockEnemySpeed = 10f;

      /// <summary>
      /// UI 空间下允许锁敌的最大距离（保留，旧版/兼容参考；新版吸附距离见 LockAssistDistance）。
      /// </summary>
      public float EnemyLockDistance = 175f;
   }
}
