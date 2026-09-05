using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:64c9da7d-8e6f-43e7-a7d5-a028b1c9196b
	public partial class Main_BattleTeamSelectPanel
	{
		public const string Name = "Main_BattleTeamSelectPanel";
		
		
		private Main_BattleTeamSelectPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			
			mData = null;
		}
		
		public Main_BattleTeamSelectPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		Main_BattleTeamSelectPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new Main_BattleTeamSelectPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
