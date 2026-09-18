using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	public class Main_MenuPanelData : UIPanelData
	{
	}
	public partial class Main_MenuPanel : UIPanel
	{
		protected override void OnInit(IUIData uiData = null)
		{
			mData = uiData as Main_MenuPanelData ?? new Main_MenuPanelData();
			// please add init code here
		}
		
		protected override void OnOpen(IUIData uiData = null)
		{
		}
		
		protected override void OnShow()
		{
		}
		
		protected override void OnHide()
		{
		}
		
		protected override void OnClose()
		{
		}
	}
}
