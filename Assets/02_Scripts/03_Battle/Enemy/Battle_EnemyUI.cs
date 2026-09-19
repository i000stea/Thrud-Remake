using DG.Tweening;
using QFramework;
using TeaGame;
using UnityEngine;

namespace TeaGame
{
	/// <summary>
	/// 敌人头顶 UI（QFramework View 重构版，T2.3 2026-09-04）。
	/// 挂在 EnemyEntity_UI prefab 根（敌人子节点），由 BattleEnemyEntity 实例化。
	/// 职责：
	///   1. 血条——首次受伤展开（DOTween），Health_In 即时、Health_Wait 缓动跟随；
	///   2. 元素标识 Sing——颜色 = 敌人自身元素色（none 隐藏）；
	///   3. 状态小字 Status_Text——冰/火/电/风 状态中文文案，颜色取元素色。
	/// 数据源：直接自读父宿主 BattleEnemyEntity（不依赖实体每帧回调）；
	/// 文本/颜色仅在"掩码/元素"变化时刷新，无每帧 GC 分配。
	/// </summary>
	public partial class Battle_EnemyUI : ViewController, IController
	{
		/// <summary>血条展开后的像素宽度（世界 Canvas 0.005 缩放 ≈ 2m）。</summary>
		private const float OpenedWidth = 400f;

		/// <summary>血条像素高度。</summary>
		private const float BarHeight = 40f;

		/// <summary>父宿主敌人实体（Start 缓存，Prefab 挂在敌人子级）。</summary>
		private BattleEnemyEntity mHost;

		/// <summary>当前缓动血条目标（由 RefreshHealth 写入）。</summary>
		private float mCachedHealthPercent = 1f;

		/// <summary>是否已展开过血条。</summary>
		private bool mOpened;

		/// <summary>上一次状态掩码（1冰/2火/4电/8风），避免每帧重建文本。</summary>
		private int mLastStatusMask = -1;

		/// <summary>上一次元素 id（避免每帧改色/开关）。</summary>
		private int mLastElementId = -1;

		/// <summary>世界 Canvas 的 RectTransform（血条宽度动画目标）。</summary>
		private RectTransform mCanvasRect;

		IArchitecture IBelongToArchitecture.GetArchitecture() => BattleArchitecture.Interface;

		/// <summary>
		/// 收起血条：运行时以 0 宽开局，首次受伤才展开。
		/// </summary>
		private void Awake()
		{
			if (EnemyUICanvas)
			{
				mCanvasRect = EnemyUICanvas.transform as RectTransform;
				if (mCanvasRect != null)
				{
					mCanvasRect.sizeDelta = new Vector2(0f, BarHeight);
				}
			}
		}

		/// <summary>
		/// 绑定父宿主（World Canvas 直接挂在敌人实体 transform 子级）。
		/// </summary>
		private void Start()
		{
			mHost = GetComponentInParent<BattleEnemyEntity>();
		}

		/// <summary>
		/// 销毁前清理自身 tween（T3.1 修复）：血条展开 DOSizeDelta 的 target 是 EnemyUICanvas 的
		/// RectTransform——敌人死亡销毁本体时该 RectTransform 随之销毁，若不 Kill，DOTween
		/// 下一帧访问已销毁目标会触发 SAFE MODE "missing target or field" 报错刷屏。
		/// </summary>
		private void OnDestroy()
		{
			if (mCanvasRect != null)
			{
				DOTween.Kill(mCanvasRect);
			}
		}

		/// <summary>
		/// 每帧：世界空间 UI 面向战斗相机；缓动血条跟随；从宿主拉取元素/状态刷新。
		/// </summary>
		private void LateUpdate()
		{
			var battleCamera = this.GetModel<BattleStateModel>().BattleCamera;
			if (battleCamera)
			{
				transform.rotation = battleCamera.transform.rotation;
			}

			if (Health_Wait)
			{
				Health_Wait.fillAmount = Mathf.Lerp(Health_Wait.fillAmount, mCachedHealthPercent, 0.05f);
			}

			if (mHost == null) return;

			RefreshSingIcon();
			RefreshStatusText();
		}

		/// <summary>
		/// 血量刷新（由宿主伤害结算调用）。首次调用展开血条。
		/// </summary>
		public void RefreshHealth(float healthPercent)
		{
			if (!mOpened)
			{
				mOpened = true;
				if (mCanvasRect != null)
				{
					mCanvasRect.DOSizeDelta(new Vector2(OpenedWidth, BarHeight), 0.5f).SetEase(Ease.OutExpo);
				}
			}

			if (Health_In)
			{
				Health_In.fillAmount = healthPercent;
			}

			mCachedHealthPercent = healthPercent;
		}

		/// <summary>
		/// 元素标识：Sing 按敌人元素切换 UIBattle 切图（_Ice_Cyan/_Fire_Red/_Wind_Green/_Electric_Blue）。
		/// none 隐藏；对应切图缺失时回退元素色圆片（便于发现配置缺口）。
		/// </summary>
		private void RefreshSingIcon()
		{
			var elementId = (int)mHost.Element;
			if (elementId == mLastElementId) return;
			mLastElementId = elementId;

			if (!Sing) return;
			if (mHost.Element == DamageElement.none)
			{
				Sing.gameObject.SetActive(false);
				return;
			}

			Sing.gameObject.SetActive(true);
			var icon = GetSignSprite(mHost.Element);
			if (icon != null)
			{
				Sing.sprite = icon;
				Sing.color = Color.white;
			}
			else
			{
				Sing.color = BattleElementBalance.GetElementColor(mHost.Element);
			}
		}

		/// <summary>
		/// 敌人元素 → UIBattle 元素标识切图（UIBattle.png 多切图，prefab 序列化引用）。
		/// </summary>
		private Sprite GetSignSprite(DamageElement element)
		{
			switch (element)
			{
				case DamageElement.Ice: return SignIce;
				case DamageElement.Fire: return SignFire;
				case DamageElement.Wind: return SignWind;
				case DamageElement.Electric: return SignElectric;
				default: return null;
			}
		}

		/// <summary>
		/// 状态小字：由宿主状态掩码拼接中文文案；仅掩码变化才重建（无每帧分配）。
		/// 单状态时文本取该元素色，混合状态取白字。
		/// </summary>
		private void RefreshStatusText()
		{
			var mask = mHost.ActiveStateMask;
			if (mask == mLastStatusMask) return;
			mLastStatusMask = mask;

			if (Status_Text == null) return;
			if (mask == 0)
			{
				Status_Text.text = string.Empty;
				return;
			}

			Status_Text.text = BuildStatusText(mask);

			var color = Color.white;
			if (mask == 1) color = BattleElementBalance.GetElementColor(DamageElement.Ice);
			else if (mask == 2) color = BattleElementBalance.GetElementColor(DamageElement.Fire);
			else if (mask == 4) color = BattleElementBalance.GetElementColor(DamageElement.Electric);
			else if (mask == 8) color = BattleElementBalance.GetElementColor(DamageElement.Wind);
			Status_Text.color = color;
		}

		/// <summary>
		/// 掩码 → 中文状态文案（如 "冰冻·燃烧"）。仅在掩码变化时调用。
		/// </summary>
		private static string BuildStatusText(int mask)
		{
			var builder = new System.Text.StringBuilder(16);
			if ((mask & 1) != 0) builder.Append("冰冻");
			if ((mask & 2) != 0) builder.Append(builder.Length > 0 ? "·燃烧" : "燃烧");
			if ((mask & 4) != 0) builder.Append(builder.Length > 0 ? "·麻痹" : "麻痹");
			if ((mask & 8) != 0) builder.Append(builder.Length > 0 ? "·易伤" : "易伤");
			return builder.ToString();
		}
	}
}
