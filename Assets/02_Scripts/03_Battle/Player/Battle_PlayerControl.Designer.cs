// Generate Id:7a203fff-6c43-41b4-a001-284ffca87ea6
using UnityEngine;

namespace TeaGame
{
	public partial class Battle_PlayerControl : QFramework.IController
	{
		public Transform ControlXAxis;
		
		public Transform ControlYAxis;
		
		public Camera Control_Camera;
		
		public Transform ViewRotateAxis;
		
		public Transform Roles;
		
		public Transform WeaponAxis;
		
		public Transform WepLookatTarget;
		
		public Transform AwakeTimeline;
		
		public UnityEngine.Rigidbody SelfRigidbody;
		
		QFramework.IArchitecture QFramework.IBelongToArchitecture.GetArchitecture()=>TeaGame.BattleArchitecture.Interface;
	}
}
