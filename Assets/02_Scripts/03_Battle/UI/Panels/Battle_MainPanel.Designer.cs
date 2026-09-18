using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TeaGame
{
	// Generate Id:8bdab249-ea84-41a0-9e77-f55ec5aeae0f
	public partial class Battle_MainPanel
	{
		public const string Name = "Battle_MainPanel";
		
		[SerializeField]
		public RectTransform Cross_LockEnemy;
		[SerializeField]
		public UnityEngine.UI.Image Health_Main;
		[SerializeField]
		public TMPro.TextMeshProUGUI Health_Value;
		/// <summary>
		/// 当前角色名 · 元素
		/// </summary>
		[SerializeField]
		public UnityEngine.UI.Text SkillHudRoleLine;
		/// <summary>
		/// 序曲 E 技能
		/// </summary>
		[SerializeField]
		public UnityEngine.UI.Text SkillHudELine;
		/// <summary>
		/// 极奏 Q 技能 · 能量
		/// </summary>
		[SerializeField]
		public UnityEngine.UI.Text SkillHudQLine;
		/// <summary>
		/// 切人提示
		/// </summary>
		[SerializeField]
		public UnityEngine.UI.Text SkillHudSwitchLine;
		
		private Battle_MainPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Cross_LockEnemy = null;
			Health_Main = null;
			Health_Value = null;
			SkillHudRoleLine = null;
			SkillHudELine = null;
			SkillHudQLine = null;
			SkillHudSwitchLine = null;
			
			mData = null;
		}
		
		public Battle_MainPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		Battle_MainPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new Battle_MainPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
