using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:6faf853e-9fd8-4084-a2aa-6fc64b849064
	public partial class Battle_PauseMenuPanel
	{
		public const string Name = "Battle_PauseMenuPanel";
		
		[SerializeField]
		public UnityEngine.UI.Button ResumeButton;
		[SerializeField]
		public UnityEngine.UI.Button RestartButton;
		[SerializeField]
		public UnityEngine.UI.Button ExitButton;
		
		private Battle_PauseMenuPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			ResumeButton = null;
			RestartButton = null;
			ExitButton = null;
			
			mData = null;
		}
		
		public Battle_PauseMenuPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		Battle_PauseMenuPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new Battle_PauseMenuPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
