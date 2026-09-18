using DG.Tweening;
using QFramework;
using UnityEngine;

namespace TeaGame.Login
{
   /// <summary>
   /// 登录场景表现控制器（场景装配器，UIKit CodeGen 绑定 UI 字段）。
   /// 职责：绑定场景 UI（遮罩/音频/开始按钮）、入场演出、按钮事件转发到 LoginSystem。
   /// 逻辑与数据在 LoginSystem/LoginModel，本类只做表现层，符合 QFramework 分层。
   /// UI 字段（awakeMask/startButton/_audioSource）由 Designer 生成（CodeGenKit）。
   /// </summary>
   public partial class LoginViewController : ViewController
   {
      [Header("演出参数")]
      [SerializeField] private float lerpTime = 1f;
      [SerializeField] private float volumeTarget = 0.7f;
      [SerializeField] private AnimationCurve lerpCurve;

      [Header("登录目标")]
      [SerializeField] private string mainSceneKey = "2MainScene";

      /// <summary>
      /// 登录完成事件反注册句柄。
      /// </summary>
      private IUnRegister mCompletedUnregister;

      /// <summary>
      /// 登录模块架构（QFramework；RegisterEvent/GetSystem 均走架构）。
      /// </summary>
      private IArchitecture Architecture => AppArchitecture.Interface;

      private void Awake()
      {
         if (startButton != null)
         {
            startButton.clickEvent.AddListener(OnStartButtonClicked);
         }

         PlayEntryPerformance();
      }

      private void OnEnable()
      {
         mCompletedUnregister = Architecture.RegisterEvent<LoginCompletedEvent>(OnLoginCompleted);
      }

      private void OnDisable()
      {
         mCompletedUnregister?.UnRegister();
         mCompletedUnregister = null;
      }

      private void OnDestroy()
      {
         if (startButton != null)
         {
            startButton.clickEvent.RemoveListener(OnStartButtonClicked);
         }
      }

      /// <summary>
      /// 入场演出：黑屏遮罩渐隐 + 背景音乐淡入。
      /// </summary>
      private void PlayEntryPerformance()
      {
         if (awakeMask != null)
         {
            awakeMask.DOColor(new Color(0f, 0f, 0f, 0f), lerpTime).SetEase(lerpCurve);
         }

         if (_audioSource != null)
         {
            _audioSource.volume = 0f;
            _audioSource.Play();
            _audioSource.DOFade(volumeTarget, lerpTime).SetEase(lerpCurve);
         }
      }

      /// <summary>
      /// 开始按钮点击：转发给 LoginSystem（防重入由 System 保证）。
      /// </summary>
      private void OnStartButtonClicked()
      {
         Architecture.GetSystem<LoginSystem>().BeginLogin(mainSceneKey);
      }

      /// <summary>
      /// 登录完成：音频淡出收尾（场景切换由 SceneFlow 在 LoginSystem 中发起）。
      /// </summary> 
      private void OnLoginCompleted(LoginCompletedEvent e)
      {
         if (_audioSource != null)
         {
            _audioSource.DOFade(0f, 0.8f).SetEase(lerpCurve);
         }
      }
   }
}
