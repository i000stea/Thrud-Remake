using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:a9c04d13-ca27-4f59-b933-22b0941d2273
	public partial class BattleLevelResultPanel
	{
		public const string Name = "BattleLevelResultPanel";
		
		
		private BattleLevelResultPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			
			mData = null;
		}
		
		public BattleLevelResultPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		BattleLevelResultPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new BattleLevelResultPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
