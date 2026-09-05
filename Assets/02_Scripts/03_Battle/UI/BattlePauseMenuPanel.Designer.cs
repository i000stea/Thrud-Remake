using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:595a629a-5a10-4a7f-8656-caf40c761a05
	public partial class BattlePauseMenuPanel
	{
		public const string Name = "BattlePauseMenuPanel";
		
		
		private BattlePauseMenuPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			
			mData = null;
		}
		
		public BattlePauseMenuPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		BattlePauseMenuPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new BattlePauseMenuPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
