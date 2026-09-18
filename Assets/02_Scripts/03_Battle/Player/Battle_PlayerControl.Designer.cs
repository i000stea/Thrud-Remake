// Generate Id:7a203fff-6c43-41b4-a001-284ffca87ea6
using UnityEngine;

namespace TeaGame
{
	public partial class Battle_PlayerControl : QFramework.IController
	{
		public Transform ControlXAxis;
		
		public Transform ControlYAxis;
		
		public Camera Control_Camera;
		
		public Canvas MainCanvas;
		
		public RectTransform Cross_LockEnemy;
		
		public UnityEngine.UI.Image Health_Main;
		
		public TMPro.TextMeshProUGUI Health_Value;
		
		/// <summary>
		/// 当前角色名 · 元素
		/// </summary>
		public UnityEngine.UI.Text SkillHudRoleLine;
		
		/// <summary>
		/// 序曲 E 技能
		/// </summary>
		public UnityEngine.UI.Text SkillHudELine;
		
		/// <summary>
		/// 极奏 Q 技能 · 能量
		/// </summary>
		public UnityEngine.UI.Text SkillHudQLine;
		
		/// <summary>
		/// 切人提示
		/// </summary>
		public UnityEngine.UI.Text SkillHudSwitchLine;
		
		public Transform ViewRotateAxis;
		
		public Transform Roles;
		
		public Transform WeaponAxis;
		
		public Transform WepLookatTarget;
		
		public Transform AwakeTimeline;
		
		public UnityEngine.Rigidbody SelfRigidbody;
		
		QFramework.IArchitecture QFramework.IBelongToArchitecture.GetArchitecture()=>TeaGame.BattleArchitecture.Interface;
	}
}
