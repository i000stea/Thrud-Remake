// Generate Id:97b5a3d8-ba20-4c41-9245-c84a158b1bd5
using UnityEngine;

namespace TeaGame.Login
{
	public partial class LoginViewController : QFramework.IController
	{
		public UnityEngine.UI.Image awakeMask;
		
		public TeaGame.TeaBaseButton startButton;
		
		public UnityEngine.Video.VideoPlayer _videoPlayer;
		
		public UnityEngine.AudioSource _audioSource;
		
		QFramework.IArchitecture QFramework.IBelongToArchitecture.GetArchitecture()=>TeaGame.AppArchitecture.Interface;
	}
}
