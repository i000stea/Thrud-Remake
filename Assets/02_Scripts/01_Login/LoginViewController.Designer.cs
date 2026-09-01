// Generate Id:a2cfaa0b-91d3-4950-88e0-140a69df2897
using UnityEngine;

namespace TeaGame.Login
{
	public partial class LoginViewController : QFramework.IController
	{
		public UnityEngine.UI.Image awakeMask;
		
		public TeaFramework.TeaBaseButton startButton;
		
		public UnityEngine.Video.VideoPlayer _videoPlayer;
		
		public UnityEngine.AudioSource _audioSource;
		
		QFramework.IArchitecture QFramework.IBelongToArchitecture.GetArchitecture()=>TeaGame.Login.LoginArchitecture.Interface;
	}
}
