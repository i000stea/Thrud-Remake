using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:d1ad8897-41b1-4ccf-b67d-617b9c09af20
	public partial class Battle_TouchInputPanel
	{
		public const string Name = "Battle_TouchInputPanel";
		
		
		private Battle_TouchInputPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			
			mData = null;
		}
		
		public Battle_TouchInputPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		Battle_TouchInputPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new Battle_TouchInputPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
