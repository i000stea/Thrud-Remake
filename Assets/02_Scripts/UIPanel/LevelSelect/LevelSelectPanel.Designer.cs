using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:9dbf0a76-62ed-459a-8320-38d6c11f4ff4
	public partial class LevelSelectPanel
	{
		public const string Name = "LevelSelectPanel";
		
		[SerializeField]
		public UnityEngine.UI.Text Title_Text;
		[SerializeField]
		public RectTransform LevelList;
		[SerializeField]
		public UnityEngine.UI.Button LevelItemTemplate;
		[SerializeField]
		public UnityEngine.UI.Text IntroTitle_Text;
		[SerializeField]
		public UnityEngine.UI.Text IntroBody_Text;
		[SerializeField]
		public UnityEngine.UI.Button EnterButton;
		[SerializeField]
		public UnityEngine.UI.Button BackButton;
		
		private LevelSelectPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Title_Text = null;
			LevelList = null;
			LevelItemTemplate = null;
			IntroTitle_Text = null;
			IntroBody_Text = null;
			EnterButton = null;
			BackButton = null;
			
			mData = null;
		}
		
		public LevelSelectPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		LevelSelectPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new LevelSelectPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
