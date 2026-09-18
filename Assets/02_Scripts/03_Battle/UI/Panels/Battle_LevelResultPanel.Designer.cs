using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:5adaa8d9-e48a-4fec-be68-b27af876488b
	public partial class Battle_LevelResultPanel
	{
		public const string Name = "Battle_LevelResultPanel";
		
		[SerializeField]
		public UnityEngine.UI.Text ResultTitle;
		[SerializeField]
		public UnityEngine.UI.Button RestartButton;
		[SerializeField]
		public UnityEngine.UI.Button ExitButton;
		
		private Battle_LevelResultPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			ResultTitle = null;
			RestartButton = null;
			ExitButton = null;
			
			mData = null;
		}
		
		public Battle_LevelResultPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		Battle_LevelResultPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new Battle_LevelResultPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
