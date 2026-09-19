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
      /// 是否让上下移动始终基于世界坐标。
      /// 若关闭，则会上下方向也跟随朝向参考轴进行转换。
      /// </summary>
      public bool DepthFromWorld = false;

      [Header("视角移动模式")]
      /// <summary>
      /// 前进方向是否跟随视角正前方（默认开启）。
      /// 开启：WASD 的"前"以相机正前方（含俯仰）为基准，适合空域飞行/自由射击，
      /// 鼠标上下看时移动与射击方向一致；关闭：仅以水平 yaw 轴为基准（旧行为，移动恒水平）。
      /// </summary>
      public bool MoveForwardFollowView = true;

      /// <summary> 基础移动力。 </summary>
      public float MoveForce = 10f;

      /// <summary> 翻滚附加倍率。 </summary>
      public float RollMultiForce = 10f;

      /// <summary> 冲刺附加倍率。 </summary>
      public float SprintMultiForce = 10f;

      /// <summary> 翻滚冷却时间。 </summary>
      [SerializeField] private float RollCooldown = 2f;

      /// <summary> 翻滚结束后进入冲刺状态前的缓冲时间。 </summary>
      [Range(0.0001f, 1f)] public float RollAfterSprintDelay = 0.7f;

      /// <summary>
      /// 获取安全的翻滚冷却。
      /// 通过下限保护，避免配置成 0 导致逻辑抖动。
      /// </summary>
      public float GetRollCooldown() => Mathf.Max(0.01f, RollCooldown);

      /// <summary> 鼠标灵敏度。 </summary>
      [Header("旋转参数")]
      public float MouseSensitivity = 100f;

      /// <summary> 默认视场角。 </summary>
      [Header("视场角与显示对象")]
      public float MainFov = 60f;

      /// <summary> 冲刺时的目标视场角。  </summary>
      public float SprintFov = 90f;

      /// <summary> 视场角插值速度。 </summary>
      public float FovLerpSpeed = 10f;

      /// <summary> 展示模型跟随旋转的平滑速度。 </summary>
      public float RotationSpeed = 5f;

      [Header("武器与换弹参数")]
      /// <summary>
      /// 换弹时长（秒）。
      /// T1.2 统一换弹时长，不做武器差异；武器数据中的 reload 字段留待后续版本接入。
      /// </summary>
      public float WeaponReloadTime = 1.5f;

      /// <summary> 手动换弹键。 </summary>
      public KeyCode ReloadKey = KeyCode.R;

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

      /// <summary> PC 端锁定切换键（半自动：在可见目标间循环切换/取消）。 </summary>
      public KeyCode LockSwitchKey = KeyCode.Tab;

      /// <summary> 锁敌准星与武器朝向的基础追踪速度。 </summary>
      public float LockEnemySpeed = 10f;

      /// <summary> UI 空间下允许锁敌的最大距离（保留，旧版/兼容参考；新版吸附距离见 LockAssistDistance）。 </summary>
      public float EnemyLockDistance = 175f;
   }
}
