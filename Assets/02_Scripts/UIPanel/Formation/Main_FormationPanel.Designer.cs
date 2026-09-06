using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:e239d4e1-9abf-416c-94ae-318373b23b66
	public partial class Main_FormationPanel
	{
		public const string Name = "Main_FormationPanel";
		
		[SerializeField]
		public UnityEngine.UI.Text Title_Text;
		[SerializeField]
		public TeaGame.Formation_Slot Formation_Slot_01;
		[SerializeField]
		public TeaGame.Formation_Slot Formation_Slot_02;
		[SerializeField]
		public TeaGame.Formation_Slot Formation_Slot_03;
		[SerializeField]
		public TeaGame.Formation_Slot Formation_Slot_04;
		[SerializeField]
		public UnityEngine.UI.Button ClearAllButton;
		[SerializeField]
		public UnityEngine.UI.Button StartButton;
		[SerializeField]
		public UnityEngine.UI.Button BackButton;
		[SerializeField]
		public UnityEngine.UI.Text Toast_Text;
		[SerializeField]
		public RectTransform PopupWindow;
		[SerializeField]
		public UnityEngine.UI.Text PopupTitle_Text;
		[SerializeField]
		public UnityEngine.UI.Image ItemList;
		[SerializeField]
		public UnityEngine.UI.Button ItemTemplate_Base;
		[SerializeField]
		public UnityEngine.UI.Text DetailTitle_Text;
		[SerializeField]
		public UnityEngine.UI.Text DetailBody_Text;
		
		private Main_FormationPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Title_Text = null;
			Formation_Slot_01 = null;
			Formation_Slot_02 = null;
			Formation_Slot_03 = null;
			Formation_Slot_04 = null;
			ClearAllButton = null;
			StartButton = null;
			BackButton = null;
			Toast_Text = null;
			PopupWindow = null;
			PopupTitle_Text = null;
			ItemList = null;
			ItemTemplate_Base = null;
			DetailTitle_Text = null;
			DetailBody_Text = null;
			
			mData = null;
		}
		
		public Main_FormationPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		Main_FormationPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new Main_FormationPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
