// Generate Id:e0c7ac90-0766-494a-b064-558dbd293f3c
using UnityEngine;

namespace TeaGame
{
	public partial class LoadingGroup : QFramework.IController
	{
		public Canvas LoadingCanvas;
		
		public UnityEngine.UI.Image loading_Icon;
		
		public TMPro.TextMeshProUGUI loading_value;
		
		public TMPro.TextMeshProUGUI msg_En;
		
		public TMPro.TextMeshProUGUI msg_Verson;
		
		public TMPro.TextMeshProUGUI msg_Frame;
		
		QFramework.IArchitecture QFramework.IBelongToArchitecture.GetArchitecture()=>TeaGame.AppArchitecture.Interface;
	}
}
