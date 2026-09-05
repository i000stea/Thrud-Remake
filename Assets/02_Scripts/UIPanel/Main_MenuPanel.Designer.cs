using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:1c76f7ac-a24d-4712-9d8a-473b797ab2a7
	public partial class Main_MenuPanel
	{
		public const string Name = "Main_MenuPanel";
		
		
		private Main_MenuPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			
			mData = null;
		}
		
		public Main_MenuPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		Main_MenuPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new Main_MenuPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
