using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame.App
{
   /// <summary>
   /// 登录场景的资源更新进度条（23 文档 §7：让 OTA 状态可视化）。
   ///
   /// **为什么放在登录场景**：启动阶段的 OTA 只做「应用已下载内容」（不联网），
   /// 真正的联网检查与下载放在这里——登录场景本来就承担「热更检查」这一步，
   /// 而且这里有画面，能看到下载到底走到哪、失败原因是什么。
   ///
   /// **下载期间只允许「暂停下载 / 停止下载」可交互**：`开始游戏` 会被禁用（组件 enabled=false +
   /// 置灰）。硬性拦截在 <c>LoginViewController.OnStartButtonClicked</c> 里还有一层，防 UI 状态竞态。
   ///
   /// **它只读 <see cref="HotUpdateOtaService.Status"/> 并调用其暂停/停止入口**，
   /// 不持有网络或下载逻辑。
   /// </summary>
   public sealed class LoginOtaProgressView : MonoBehaviour
   {
      /// <summary>进度条填充（按 x 轴缩放表示进度，pivot.x 必须为 0）。</summary>
      [SerializeField] private RectTransform fill;

      /// <summary>状态文案（检查中/下载中/已暂停/已是最新/失败原因）。</summary>
      [SerializeField] private TMP_Text statusText;

      /// <summary>版本信息（本地版本 → 远端版本）。</summary>
      [SerializeField] private TMP_Text versionText;

      /// <summary>「暂停下载 / 继续下载」按钮（仅在下载期间显示）。</summary>
      [SerializeField] private Button pauseButton;

      /// <summary>暂停按钮的文字（按钮是切换式的，文案随之变化）。</summary>
      [SerializeField] private TMP_Text pauseLabel;

      /// <summary>「停止下载」按钮（仅在下载期间显示）。</summary>
      [SerializeField] private Button stopButton;

      [Header("下载期间锁住「开始游戏」")]
      [Tooltip("登录场景的开始按钮（TeaBaseButton）。锁定期间禁用该组件，使其不再接收点击。")]
      [SerializeField] private TeaBaseButton startButton;

      [Tooltip("开始按钮的 Image（锁定期间置灰）。")]
      [SerializeField] private Image startButtonImage;

      /// <summary>进入登录场景后自动检查一次。</summary>
      [SerializeField] private bool autoCheckOnStart = true;

      /// <summary>进度为 0 时保留的最小填充宽度比例（否则 0% 时进度条完全消失，看不出边界）。</summary>
      [SerializeField] private float minFillRatio = 0.01f;

      /// <summary>开始按钮的原始颜色（解锁时还原）。</summary>
      private Color mStartButtonColor = Color.white;

      /// <summary>当前是否处于锁定态（避免每帧改组件）。</summary>
      private bool mStartLocked;

      private void Awake()
      {
         if (startButtonImage != null)
         {
            mStartButtonColor = startButtonImage.color;
         }

         if (pauseButton != null)
         {
            pauseButton.onClick.AddListener(OnPauseClicked);
         }

         if (stopButton != null)
         {
            stopButton.onClick.AddListener(OnStopClicked);
         }
      }

      private void OnDestroy()
      {
         if (pauseButton != null)
         {
            pauseButton.onClick.RemoveListener(OnPauseClicked);
         }

         if (stopButton != null)
         {
            stopButton.onClick.RemoveListener(OnStopClicked);
         }
      }

      private void Start()
      {
         Refresh();

         if (autoCheckOnStart)
         {
            // 协程跑在常驻宿主上（见 HotUpdateOtaService.Host）：本场景被卸载时下载不会中断
            HotUpdateOtaService.TryStartCheck(this);
         }
      }

      private void Update()
      {
         // 状态由协程写入，这里每帧读一次即可；UI 更新成本远低于轮询业务数据的代价
         Refresh();
      }

      private void OnPauseClicked()
      {
         if (HotUpdateOtaService.Status.IsPaused)
         {
            HotUpdateOtaService.RequestResume();
         }
         else
         {
            HotUpdateOtaService.RequestPause();
         }
      }

      private void OnStopClicked()
      {
         HotUpdateOtaService.RequestStop();
      }

      /// <summary>把当前状态刷到界面。</summary>
      private void Refresh()
      {
         var status = HotUpdateOtaService.Status;
         var running = status.IsRunning;

         if (fill != null)
         {
            var ratio = Mathf.Clamp01(status.Progress);
            fill.localScale = new Vector3(Mathf.Max(ratio, minFillRatio), 1f, 1f);
         }

         if (statusText != null)
         {
            statusText.text = BuildStatusLine(status);
         }

         if (versionText != null)
         {
            versionText.text = BuildVersionLine(status);
         }

         // 下载期间只留「暂停 / 停止」可交互
         if (pauseButton != null)
         {
            pauseButton.gameObject.SetActive(running);
         }

         if (stopButton != null)
         {
            stopButton.gameObject.SetActive(running);
         }

         if (pauseLabel != null)
         {
            pauseLabel.text = status.IsPaused ? "继续下载" : "暂停下载";
         }

         SetStartLocked(running);
      }

      /// <summary>锁定/解锁「开始游戏」。</summary>
      private void SetStartLocked(bool locked)
      {
         if (mStartLocked == locked)
         {
            return;
         }

         mStartLocked = locked;

         if (startButton != null)
         {
            // 禁用组件即可停止接收 IPointerClickHandler，比改 CanvasGroup 更干净
            // （TeaBaseButton 自己在 LateUpdate 里驱动 alpha，外部改 alpha 会被它覆盖）
            startButton.enabled = !locked;
         }

         if (startButtonImage != null)
         {
            startButtonImage.color = locked
               ? new Color(mStartButtonColor.r * 0.45f, mStartButtonColor.g * 0.45f,
                  mStartButtonColor.b * 0.45f, mStartButtonColor.a)
               : mStartButtonColor;
         }
      }

      private static string BuildStatusLine(HotUpdateOtaStatus status)
      {
         switch (status.Stage)
         {
            case HotUpdateOtaStage.Checking:
               return "正在检查资源版本…";
            case HotUpdateOtaStage.Downloading:
               return $"{status.Message}  {Mathf.FloorToInt(Mathf.Clamp01(status.Progress) * 100f)}%";
            case HotUpdateOtaStage.Paused:
               return $"已暂停（{Mathf.FloorToInt(Mathf.Clamp01(status.Progress) * 100f)}%）—— 点「继续下载」接着下";
            default:
               return status.Message;
         }
      }

      private static string BuildVersionLine(HotUpdateOtaStatus status)
      {
         var local = string.IsNullOrEmpty(status.LocalVersion) ? "包内基线" : status.LocalVersion;
         return string.IsNullOrEmpty(status.RemoteVersion)
            ? $"本地内容：{local}"
            : $"本地内容：{local}  →  服务器：{status.RemoteVersion}";
      }
   }
}
