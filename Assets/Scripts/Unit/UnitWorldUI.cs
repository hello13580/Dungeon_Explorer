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
	private Unit unit;

	private void Start()
	{
		Unit.OnAnyActionPointsChanged += Unit_OnAnyActionPointsChanged;
		healthSystem.OnUnitDamaged += HealthSystem_OnUnitDamaged;

		if (barrierSystem != null)
			barrierSystem.OnBarrierChanged += BarrierSystem_OnBarrierChanged;

		UpdateActionPointText();
		UpdateHealthBar();
		UpdateBarrierBar();
	}

	private void OnDestroy()
	{
		if (barrierSystem != null)
			barrierSystem.OnBarrierChanged -= BarrierSystem_OnBarrierChanged;
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
}
