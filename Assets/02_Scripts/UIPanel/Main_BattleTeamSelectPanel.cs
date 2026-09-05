using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	public class Main_BattleTeamSelectPanelData : UIPanelData
	{
	}
	public partial class Main_BattleTeamSelectPanel : UIPanel
	{
		protected override void OnInit(IUIData uiData = null)
		{
			mData = uiData as Main_BattleTeamSelectPanelData ?? new Main_BattleTeamSelectPanelData();
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
