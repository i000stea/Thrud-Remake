using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:190ed13e-3e91-4c77-adfb-13d3e85e740a
	public partial class Main_MenuPanel
	{
		public const string Name = "Main_MenuPanel";
		
		[SerializeField]
		public TeaGame.TeaBaseButton gacha_Banner;
		[SerializeField]
		public TeaGame.TeaBaseButton btn_Internship;
		[SerializeField]
		public TeaGame.TeaBaseButton btn_season;
		[SerializeField]
		public TeaGame.TeaBaseButton btn_activities;
		[SerializeField]
		public TeaGame.TeaBaseButton btn_Map;
		[SerializeField]
		public TeaGame.TeaBaseButton btn_Res;
		[SerializeField]
		public TeaGame.TeaBaseButton btn_World;
		[SerializeField]
		public TeaGame.TeaBaseButton btn_Test;
		
		private Main_MenuPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			gacha_Banner = null;
			btn_Internship = null;
			btn_season = null;
			btn_activities = null;
			btn_Map = null;
			btn_Res = null;
			btn_World = null;
			btn_Test = null;
			
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
