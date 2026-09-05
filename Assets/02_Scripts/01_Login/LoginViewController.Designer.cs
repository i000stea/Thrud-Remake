// Generate Id:a5d7d905-d987-4d97-9106-680ac07cc622
using UnityEngine;

namespace TeaGame.Login
{
	public partial class LoginViewController : QFramework.IController
	{
		public UnityEngine.UI.Image awakeMask;
		
		public TeaGame.TeaBaseButton startButton;
		
		public UnityEngine.Video.VideoPlayer _videoPlayer;
		
		public UnityEngine.AudioSource _audioSource;
		
		QFramework.IArchitecture QFramework.IBelongToArchitecture.GetArchitecture()=>TeaGame.Login.LoginArchitecture.Interface;
	}
}
