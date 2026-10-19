using TMPro;
using UnityEngine;

namespace TeaGame.App
{
   /// <summary>
   /// 登录场景的资源更新进度条（23 文档 §7：让 OTA 状态可视化）。
   ///
   /// **为什么放在登录场景**：启动阶段的 OTA 只做「应用已下载内容」（不联网），
   /// 真正的联网检查与下载放在这里——登录场景本来就承担「热更检查」这一步，
   /// 而且这里有画面，能看到下载到底走到哪、失败原因是什么。
   ///
   /// **它只读 <see cref="HotUpdateOtaService.Status"/>**，不持有网络或下载逻辑，
   /// 也不改登录流程：下载/失败都不会挡住「开始游戏」。
   /// </summary>
   public sealed class LoginOtaProgressView : MonoBehaviour
   {
      /// <summary>进度条填充（按 x 轴缩放表示进度，pivot.x 必须为 0）。</summary>
      [SerializeField] private RectTransform fill;

      /// <summary>状态文案（检查中/下载中/已是最新/失败原因）。</summary>
      [SerializeField] private TMP_Text statusText;

      /// <summary>版本信息（本地版本 → 远端版本）。</summary>
      [SerializeField] private TMP_Text versionText;

      /// <summary>进入登录场景后自动检查一次。</summary>
      [SerializeField] private bool autoCheckOnStart = true;

      /// <summary>进度为 0 时保留的最小填充宽度比例（否则 0% 时进度条完全消失，看不出边界）。</summary>
      [SerializeField] private float minFillRatio = 0.01f;

      private void Start()
      {
         Refresh();

         if (autoCheckOnStart && !HotUpdateOtaService.Status.IsRunning)
         {
            StartCoroutine(HotUpdateOtaService.RunCheck());
         }
      }

      private void Update()
      {
         // 状态由协程写入，这里每帧读一次即可；UI 更新成本远低于轮询业务数据的代价
         Refresh();
      }

      /// <summary>把当前状态刷到界面。</summary>
      private void Refresh()
      {
         var status = HotUpdateOtaService.Status;

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
      }

      private static string BuildStatusLine(HotUpdateOtaStatus status)
      {
         switch (status.Stage)
         {
            case HotUpdateOtaStage.Checking:
               return "正在检查资源版本…";
            case HotUpdateOtaStage.Downloading:
               return $"{status.Message}  {Mathf.FloorToInt(Mathf.Clamp01(status.Progress) * 100f)}%";
            case HotUpdateOtaStage.UpToDate:
            case HotUpdateOtaStage.Completed:
               return status.Message;
            case HotUpdateOtaStage.Failed:
               return status.Message;
            case HotUpdateOtaStage.Disabled:
               return status.Message;
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
