using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitWorldUI : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI actionPointsText;

	[SerializeField]
	private Image healthBarImage;

	[SerializeField]
	private Image barrierBarImage;

	[SerializeField]
	private HealthSystem healthSystem;

	[SerializeField]
	private BarrierSystem barrierSystem;

	[SerializeField]
	private ManaSystem manaSystem;

	[SerializeField]
	private Image manaBarImage;

	[SerializeField]
	private JobPointSystem jobPointSystem; // 직업 포인트 시스템 (없는 유닛은 null)

	[SerializeField]
	private Image jobPointBarImage; // JP 바 이미지 (없으면 표시 생략)

    [SerializeField]
	private Unit unit;

	private void Start()
	{
		Unit.OnAnyActionPointsChanged += Unit_OnAnyActionPointsChanged;
		healthSystem.OnUnitDamaged += HealthSystem_OnUnitDamaged;

		if (barrierSystem != null)
			barrierSystem.OnBarrierChanged += BarrierSystem_OnBarrierChanged;

		if (manaSystem != null)
			manaSystem.OnManaChanged += ManaSystem_OnManaChanged;

		// JP 변경 이벤트 구독 — JobPointSystem이 없는 유닛은 구독하지 않는다
		if (jobPointSystem != null)
			jobPointSystem.OnJobPointsChanged += JobPointSystem_OnJobPointsChanged;

		UpdateActionPointText();
		UpdateHealthBar();
		UpdateBarrierBar();
		UpdateManaBar();
		UpdateJobPointBar();
	}

	private void OnDestroy()
	{
		if (barrierSystem != null)
			barrierSystem.OnBarrierChanged -= BarrierSystem_OnBarrierChanged;

		if (manaSystem != null)
			manaSystem.OnManaChanged -= ManaSystem_OnManaChanged;

		if (jobPointSystem != null)
			jobPointSystem.OnJobPointsChanged -= JobPointSystem_OnJobPointsChanged;
	}

	private void UpdateActionPointText()
	{
		actionPointsText.text = unit.GetCurrentActionPoint().ToString();
	}

	private void Unit_OnAnyActionPointsChanged(object sender, EventArgs empty)
	{
		UpdateActionPointText();
	}

	private void UpdateHealthBar()
	{
		healthBarImage.fillAmount = healthSystem.GetHealthNormalized();
	}

	private void HealthSystem_OnUnitDamaged(object sender, EventArgs empty)
	{
		UpdateHealthBar();
		UpdateBarrierBar();
	}

	private void UpdateBarrierBar()
	{
		if (barrierBarImage == null) return;

		if (barrierSystem == null || !barrierSystem.HasBarrier())
		{
			barrierBarImage.fillAmount = 0f;
			return;
		}

		// 배리어는 HP바 위에 겹쳐서 표시
		// fillAmount가 1을 넘을 수 있으므로 Clamp 처리
		barrierBarImage.fillAmount = Mathf.Clamp01(barrierSystem.GetBarrierNormalized());
	}

	private void BarrierSystem_OnBarrierChanged(object sender, EventArgs e)
	{
		UpdateBarrierBar();
	}

	private void UpdateManaBar()
	{
		if (manaBarImage == null) return;
		if (manaSystem == null)
		{
			manaBarImage.fillAmount = 0f;
			return;
		}
		manaBarImage.fillAmount = manaSystem.GetManaNormalized();
	}

	private void ManaSystem_OnManaChanged(object sender, EventArgs e)
	{
		UpdateManaBar();
	}

	private void UpdateJobPointBar()
	{
		if (jobPointBarImage == null) return;
		if (jobPointSystem == null)
		{
			jobPointBarImage.fillAmount = 0f;
			return;
		}
		// JP는 최대치 대비 현재 비율로 표시
		jobPointBarImage.fillAmount = (float)jobPointSystem.GetCurrentJobPoints() / jobPointSystem.GetMaxJobPoints();
	}

	private void JobPointSystem_OnJobPointsChanged(object sender, EventArgs e)
	{
		UpdateJobPointBar();
	}
}
